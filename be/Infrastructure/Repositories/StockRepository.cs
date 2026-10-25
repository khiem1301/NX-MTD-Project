using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

public sealed class StockRepository : IStockRepository
{
    private readonly NxMtdDbContext _context;

    public StockRepository(NxMtdDbContext context) => _context = context;

    public async Task<decimal> GetOnHandAsync(StockKey key, CancellationToken ct) =>
        await Transactions(key).SumAsync(t => (decimal?)t.QtyChange, ct) ?? 0m;

    public async Task<decimal> GetAvailableAsync(StockKey key, CancellationToken ct) =>
        await Balances(key).SumAsync(b => b.AvailableQty, ct) ?? 0m;

    public async Task<string> DescribeAsync(StockKey key, CancellationToken ct)
    {
        if (key.WarehouseType == WarehouseTypes.Material)
        {
            var name = await _context.Materials.AsNoTracking().Where(m => m.MaterialId == key.MaterialId)
                .Select(m => m.MaterialCode + " " + m.MaterialName).FirstOrDefaultAsync(ct);
            return string.IsNullOrEmpty(key.MaterialColor) ? name ?? "?" : $"{name} ({key.MaterialColor})";
        }
        return await _context.OrderLines.AsNoTracking().Where(l => l.OrderLineId == key.OrderLineId)
            .Select(l => l.ProductVariant.Product.ProductCode + " " + l.ProductVariant.Color + "-" + l.ProductVariant.SizeCode).FirstOrDefaultAsync(ct) ?? "?";
    }

    public async Task AddAsync(StockTransaction transaction, CancellationToken ct) =>
        await _context.StockTransactions.AddAsync(transaction, ct);

    public Task<PagedResult<StockBalanceDto>> GetBalancesAsync(StockBalanceQuery query, CancellationToken ct) =>
        query.WarehouseType == WarehouseTypes.Material ? MaterialBalances(query, ct) : PieceBalances(query, ct);

    public async Task<IReadOnlyList<StockCardEntryDto>> GetStockCardAsync(StockKey key, CancellationToken ct)
    {
        var rows = await Transactions(key).AsNoTracking().OrderBy(t => t.StockTransactionId).ToListAsync(ct);
        var codes = await LoadDocCodesAsync(rows, ct);
        return rows.Select(t => new StockCardEntryDto(t.StockTransactionId, t.TransactionDate, t.CreatedAt,
            t.SourceDocType, t.SourceDocId, codes.GetValueOrDefault((t.SourceDocType, t.SourceDocId)),
            t.QtyChange, t.BalanceAfter)).ToList();
    }

    public async Task<IReadOnlyList<TeamPieceHoldingDto>> GetTeamHoldingsAsync(int? sewingTeamId, int? orderId,
        CancellationToken ct)
    {
        var q = from h in _context.VwTeamPieceHoldings.AsNoTracking()
                join l in _context.OrderLines on h.OrderLineId equals l.OrderLineId
                join t in _context.Teams on h.SewingTeamId equals t.TeamId
                where (sewingTeamId == null || h.SewingTeamId == sewingTeamId)
                      && (orderId == null || l.OrderId == orderId)
                orderby t.TeamName, l.Order.OrderCode, l.ProductVariant.Product.ProductCode, l.ProductVariant.Color, l.ProductVariant.SizeCode
                select new TeamPieceHoldingDto(h.SewingTeamId, t.TeamName, l.OrderLineId, l.Order.OrderCode,
                    l.ProductVariant.Product.ProductCode, l.ProductVariant.Color, l.ProductVariant.SizeCode, h.ReceivedFromWarehouse ?? 0, h.HandedToWorkers,
                    h.RetrievedFromWorkers, h.HeldByTeam ?? 0);
        return await q.ToListAsync(ct);
    }

    private IQueryable<StockTransaction> Transactions(StockKey key) =>
        key.WarehouseType == WarehouseTypes.Material
            ? _context.StockTransactions.Where(t => t.WarehouseType == key.WarehouseType
                && t.MaterialId == key.MaterialId && t.MaterialColor == key.MaterialColor)
            : _context.StockTransactions.Where(t => t.WarehouseType == key.WarehouseType
                && t.OrderLineId == key.OrderLineId && t.ProductComponentId == key.ProductComponentId);

    private IQueryable<VwStockBalance> Balances(StockKey key) =>
        key.WarehouseType == WarehouseTypes.Material
            ? _context.VwStockBalances.Where(b => b.WarehouseType == key.WarehouseType
                && b.MaterialId == key.MaterialId && b.MaterialColor == key.MaterialColor)
            : _context.VwStockBalances.Where(b => b.WarehouseType == key.WarehouseType
                && b.OrderLineId == key.OrderLineId && b.ProductComponentId == key.ProductComponentId);

    private Task<PagedResult<StockBalanceDto>> MaterialBalances(StockBalanceQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var q = from b in _context.VwStockBalances.AsNoTracking()
                join m in _context.Materials on b.MaterialId equals m.MaterialId
                where b.WarehouseType == WarehouseTypes.Material && (b.OnHandQty != 0 || b.ReservedQty != 0)
                      && (string.IsNullOrEmpty(keyword) || m.MaterialCode.Contains(keyword) || m.MaterialName.Contains(keyword))
                      && (string.IsNullOrEmpty(query.Color) || b.MaterialColor == query.Color)
                      && (query.OrderId == null || _context.OrderMaterials.Any(o => o.OrderId == query.OrderId
                          && o.MaterialId == b.MaterialId && o.MaterialColor == b.MaterialColor))
                orderby m.MaterialCode, b.MaterialColor
                select new StockBalanceDto(b.WarehouseType, m.MaterialId, m.MaterialCode, m.MaterialName,
                    m.MaterialType, m.Unit, b.MaterialColor, null, null, null, null, null, null, null,
                    b.OnHandQty ?? 0, b.ReservedQty ?? 0, b.AvailableQty ?? 0);
        return q.ToPagedAsync(query, ct);
    }

    private Task<PagedResult<StockBalanceDto>> PieceBalances(StockBalanceQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var q = from b in _context.VwStockBalances.AsNoTracking()
                join l in _context.OrderLines on b.OrderLineId equals l.OrderLineId
                where b.WarehouseType == query.WarehouseType && (b.OnHandQty != 0 || b.ReservedQty != 0)
                      && (query.OrderId == null || l.OrderId == query.OrderId)
                      && (string.IsNullOrEmpty(keyword) || l.ProductVariant.Product.ProductCode.Contains(keyword)
                          || l.ProductVariant.Product.ProductName.Contains(keyword) || l.Order.OrderCode.Contains(keyword))
                      && (string.IsNullOrEmpty(query.Color) || l.ProductVariant.Color == query.Color)
                      && (string.IsNullOrEmpty(query.SizeCode) || l.ProductVariant.SizeCode == query.SizeCode)
                orderby l.Order.OrderCode, l.ProductVariant.Product.ProductCode, l.ProductVariant.Color, l.ProductVariant.SizeCode
                select new StockBalanceDto(b.WarehouseType, null, null, null, null, null, null, l.OrderLineId,
                    l.OrderId, l.Order.OrderCode, l.ProductVariant.Product.ProductCode, l.ProductVariant.Product.ProductName, l.ProductVariant.Color, l.ProductVariant.SizeCode,
                    b.OnHandQty ?? 0, b.ReservedQty ?? 0, b.AvailableQty ?? 0);
        return q.ToPagedAsync(query, ct);
    }

    /// <summary>Lấy mã chứng từ cho thẻ kho: mỗi loại chứng từ một truy vấn.</summary>
    private async Task<Dictionary<(string, int), string>> LoadDocCodesAsync(IReadOnlyList<StockTransaction> rows,
        CancellationToken ct)
    {
        var result = new Dictionary<(string, int), string>();
        foreach (var group in rows.GroupBy(r => r.SourceDocType))
        {
            var ids = group.Select(r => r.SourceDocId).Distinct().ToList();
            var pairs = group.Key switch
            {
                StockDocTypes.MaterialReceipt => await _context.MaterialReceipts.Where(x => ids.Contains(x.MaterialReceiptId))
                    .Select(x => new { Id = x.MaterialReceiptId, Code = x.ReceiptCode }).ToListAsync(ct),
                StockDocTypes.MaterialIssue => await _context.MaterialIssues.Where(x => ids.Contains(x.MaterialIssueId))
                    .Select(x => new { Id = x.MaterialIssueId, Code = x.IssueCode }).ToListAsync(ct),
                StockDocTypes.MaterialReturn => await _context.MaterialReturns.Where(x => ids.Contains(x.MaterialReturnId))
                    .Select(x => new { Id = x.MaterialReturnId, Code = x.ReturnCode }).ToListAsync(ct),
                StockDocTypes.CutHandover => await _context.CutHandovers.Where(x => ids.Contains(x.CutHandoverId))
                    .Select(x => new { Id = x.CutHandoverId, Code = x.HandoverCode }).ToListAsync(ct),
                StockDocTypes.FgHandover => await _context.FgHandovers.Where(x => ids.Contains(x.FgHandoverId))
                    .Select(x => new { Id = x.FgHandoverId, Code = x.HandoverCode }).ToListAsync(ct),
                StockDocTypes.DispatchNote => await _context.DispatchNotes.Where(x => ids.Contains(x.DispatchNoteId))
                    .Select(x => new { Id = x.DispatchNoteId, Code = x.DispatchCode }).ToListAsync(ct),
                StockDocTypes.Stocktake => await _context.Stocktakes.Where(x => ids.Contains(x.StocktakeId))
                    .Select(x => new { Id = x.StocktakeId, Code = x.StocktakeCode }).ToListAsync(ct),
                _ => new(),
            };
            foreach (var p in pairs) result[(group.Key, p.Id)] = p.Code;
        }
        return result;
    }
}
