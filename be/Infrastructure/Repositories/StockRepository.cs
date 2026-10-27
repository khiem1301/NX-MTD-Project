using Microsoft.Data.SqlClient;
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
        await StockBalancesOf(key).AsNoTracking().Select(b => (decimal?)b.OnHandQty).FirstOrDefaultAsync(ct) ?? 0m;

    public async Task<decimal> GetAvailableAsync(StockKey key, CancellationToken ct) =>
        await Balances(key).SumAsync(b => b.AvailableQty, ct) ?? 0m;

    public async Task<string> DescribeAsync(StockKey key, CancellationToken ct)
    {
        if (key.WarehouseType == WarehouseTypes.Material)
        {
            var m = await _context.Materials.AsNoTracking().Where(x => x.MaterialId == key.MaterialId)
                .Select(x => new { x.MaterialCode, x.MaterialName, x.Color }).FirstOrDefaultAsync(ct);
            if (m is null) return "?";
            return string.IsNullOrEmpty(m.Color) ? $"{m.MaterialCode} {m.MaterialName}" : $"{m.MaterialCode} {m.MaterialName} ({m.Color})";
        }
        return await _context.OrderLines.AsNoTracking().Where(l => l.OrderLineId == key.OrderLineId)
            .Select(l => l.ProductVariant.Product.ProductCode + " " + l.ProductVariant.Color + "-" + l.ProductVariant.SizeCode).FirstOrDefaultAsync(ct) ?? "?";
    }

    public async Task PostAsync(StockMovement m, CancellationToken ct)
    {
        object Db(object? value) => value ?? DBNull.Value;
        var parameters = new SqlParameter[]
        {
            new("@WarehouseType", m.Key.WarehouseType),
            new("@MaterialId", Db(m.Key.MaterialId)),
            new("@OrderLineId", Db(m.Key.OrderLineId)),
            new("@ProductComponentMaterialId", Db(m.Key.ProductComponentMaterialId)),
            new("@SourceDocType", m.SourceDocType),
            new("@SourceDocId", m.SourceDocId),
            new("@SourceLineId", Db(m.SourceLineId)),
            new("@QtyChange", m.QtyChange),
            new("@TransactionDate", m.TransactionDate.ToDateTime(TimeOnly.MinValue)) { SqlDbType = System.Data.SqlDbType.Date },
        };
        // Thủ tục trả về 1 dòng (StockBalanceId, BalanceAfter); đọc hết để kết nối được giải phóng.
        await _context.Database.SqlQueryRaw<StockPostResult>(
            "EXEC dbo.usp_Stock_Post @WarehouseType, @MaterialId, @OrderLineId, @ProductComponentMaterialId, " +
            "NULL, @SourceDocType, @SourceDocId, @SourceLineId, @QtyChange, @TransactionDate", parameters).ToListAsync(ct);
    }

    public async Task<int> GetOrCreateBalanceIdAsync(StockKey key, CancellationToken ct)
    {
        var id = await StockBalancesOf(key).Select(b => (int?)b.StockBalanceId).FirstOrDefaultAsync(ct);
        if (id != null) return id.Value;
        var balance = new StockBalance
        {
            WarehouseType = key.WarehouseType, MaterialId = key.MaterialId,
            OrderLineId = key.OrderLineId, ProductComponentMaterialId = key.ProductComponentMaterialId,
        };
        await _context.StockBalances.AddAsync(balance, ct);
        await _context.SaveChangesAsync(ct);
        return balance.StockBalanceId;
    }

    public async Task<StockKey?> GetKeyAsync(int stockBalanceId, CancellationToken ct) =>
        await _context.StockBalances.AsNoTracking().Where(b => b.StockBalanceId == stockBalanceId)
            .Select(b => new StockKey(b.WarehouseType, b.MaterialId, b.OrderLineId, b.ProductComponentMaterialId))
            .FirstOrDefaultAsync(ct);

    private sealed record StockPostResult(int StockBalanceId, decimal BalanceAfter);

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
        var received = await _context.MaterialIssueLines.AsNoTracking().ReceivedSets(sewingTeamId)
            .Where(l => orderId == null || l.OrderLine!.OrderId == orderId)
            .GroupBy(l => new { TeamId = l.MaterialIssue.ReceivingTeamId, OrderLineId = l.OrderLineId!.Value })
            .Select(g => new { g.Key.TeamId, g.Key.OrderLineId, Qty = g.Sum(l => l.ConfirmedQty ?? 0) })
            .ToListAsync(ct);
        if (received.Count == 0) return Array.Empty<TeamPieceHoldingDto>();

        var lineIds = received.Select(r => r.OrderLineId).Distinct().ToList();
        var finished = await _context.QcBatches.AsNoTracking().Where(b => lineIds.Contains(b.OrderLineId))
            .GroupBy(b => new { b.SewingTeamId, b.OrderLineId })
            .Select(g => new { g.Key.SewingTeamId, g.Key.OrderLineId, Qty = g.Sum(b => b.FinishedQty) }).ToListAsync(ct);
        var scrap = await _context.DefectRecords.AsNoTracking()
            .Where(d => lineIds.Contains(d.OrderLineId) && d.DefectType == DefectTypes.Scrap && d.Status != DefectStatuses.Cancelled)
            .GroupBy(d => new { d.SewingTeamId, d.OrderLineId })
            .Select(g => new { g.Key.SewingTeamId, g.Key.OrderLineId, Qty = g.Sum(d => d.Quantity) }).ToListAsync(ct);
        var teamIds = received.Select(r => r.TeamId).Distinct().ToList();
        var teams = await _context.Teams.AsNoTracking().Where(t => teamIds.Contains(t.TeamId))
            .ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);
        var lines = await _context.OrderLines.AsNoTracking().Where(l => lineIds.Contains(l.OrderLineId))
            .Select(l => new { l.OrderLineId, l.Order.OrderCode, l.ProductVariant.Product.ProductCode, l.ProductVariant.Color, l.ProductVariant.SizeCode })
            .ToDictionaryAsync(l => l.OrderLineId, ct);

        return received
            .Select(r =>
            {
                var l = lines[r.OrderLineId];
                var qty = (int)r.Qty;
                var done = finished.Where(f => f.SewingTeamId == r.TeamId && f.OrderLineId == r.OrderLineId).Sum(f => f.Qty);
                var lost = scrap.Where(f => f.SewingTeamId == r.TeamId && f.OrderLineId == r.OrderLineId).Sum(f => f.Qty);
                return new TeamPieceHoldingDto(r.TeamId, teams.GetValueOrDefault(r.TeamId, "?"), r.OrderLineId, l.OrderCode,
                    l.ProductCode, l.Color, l.SizeCode, qty, done, lost, Math.Max(0, qty - done - lost));
            })
            .OrderBy(x => x.TeamName).ThenBy(x => x.OrderCode).ThenBy(x => x.ProductCode).ThenBy(x => x.Color).ThenBy(x => x.SizeCode)
            .ToList();
    }

    private IQueryable<StockBalance> StockBalancesOf(StockKey key) =>
        key.WarehouseType == WarehouseTypes.Material
            ? _context.StockBalances.Where(b => b.WarehouseType == key.WarehouseType
                && b.MaterialId == key.MaterialId)
            : _context.StockBalances.Where(b => b.WarehouseType == key.WarehouseType
                && b.OrderLineId == key.OrderLineId && b.ProductComponentMaterialId == key.ProductComponentMaterialId);

    private IQueryable<StockTransaction> Transactions(StockKey key) =>
        _context.StockTransactions.Where(t => StockBalancesOf(key).Any(b => b.StockBalanceId == t.StockBalanceId));

    private IQueryable<VwStockBalance> Balances(StockKey key) =>
        key.WarehouseType == WarehouseTypes.Material
            ? _context.VwStockBalances.Where(b => b.WarehouseType == key.WarehouseType
                && b.MaterialId == key.MaterialId)
            : _context.VwStockBalances.Where(b => b.WarehouseType == key.WarehouseType
                && b.OrderLineId == key.OrderLineId && b.ProductComponentMaterialId == key.ProductComponentMaterialId);

    private Task<PagedResult<StockBalanceDto>> MaterialBalances(StockBalanceQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var q = from b in _context.VwStockBalances.AsNoTracking()
                join m in _context.Materials on b.MaterialId equals m.MaterialId
                where b.WarehouseType == WarehouseTypes.Material && (b.OnHandQty != 0 || b.ReservedQty != 0)
                      && (string.IsNullOrEmpty(keyword) || m.MaterialCode.Contains(keyword) || m.MaterialName.Contains(keyword))
                      && (string.IsNullOrEmpty(query.Color) || m.Color == query.Color)
                      && (query.OrderId == null || _context.OrderMaterials.Any(o => o.OrderId == query.OrderId
                          && o.MaterialId == b.MaterialId))
                orderby m.MaterialCode, m.Color
                select new StockBalanceDto(b.WarehouseType, m.MaterialId, m.MaterialCode, m.MaterialName,
                    m.MaterialType, m.Unit, m.Color, null, null, null, null, null, null, null,
                    b.OnHandQty ?? 0, b.ReservedQty ?? 0, b.AvailableQty ?? 0, null, null);
        return q.ToPagedAsync(query, ct);
    }

    private Task<PagedResult<StockBalanceDto>> PieceBalances(StockBalanceQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var q = from b in _context.VwStockBalances.AsNoTracking()
                join l in _context.OrderLines on b.OrderLineId equals l.OrderLineId
                join pcm in _context.ProductComponentMaterials on b.ProductComponentMaterialId equals pcm.ProductComponentMaterialId into pcmj
                from pcm in pcmj.DefaultIfEmpty()
                let c = pcm.ProductComponent
                where b.WarehouseType == query.WarehouseType && (b.OnHandQty != 0 || b.ReservedQty != 0)
                      && (query.OrderId == null || l.OrderId == query.OrderId)
                      && (string.IsNullOrEmpty(keyword) || l.ProductVariant.Product.ProductCode.Contains(keyword)
                          || l.ProductVariant.Product.ProductName.Contains(keyword) || l.Order.OrderCode.Contains(keyword))
                      && (string.IsNullOrEmpty(query.Color) || l.ProductVariant.Color == query.Color)
                      && (string.IsNullOrEmpty(query.SizeCode) || l.ProductVariant.SizeCode == query.SizeCode)
                orderby l.Order.OrderCode, l.ProductVariant.Product.ProductCode, l.ProductVariant.Color, l.ProductVariant.SizeCode,
                    c.SortOrder
                select new StockBalanceDto(b.WarehouseType, null, null, null, null, null, null, l.OrderLineId,
                    l.OrderId, l.Order.OrderCode, l.ProductVariant.Product.ProductCode, l.ProductVariant.Product.ProductName, l.ProductVariant.Color, l.ProductVariant.SizeCode,
                    b.OnHandQty ?? 0, b.ReservedQty ?? 0, b.AvailableQty ?? 0, b.ProductComponentMaterialId,
                    pcm == null ? null : c.ComponentName + " – " + pcm.Material.MaterialName + (pcm.Material.Color == "" ? "" : " " + pcm.Material.Color));
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
