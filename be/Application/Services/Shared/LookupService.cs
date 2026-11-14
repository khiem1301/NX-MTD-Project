using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class LookupService : ILookupService
{
    private const int MaxOptions = 100;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IStockService _stock;

    public LookupService(IUnitOfWork uow, ICurrentUser user, IStockService stock)
    {
        _uow = uow; _user = user; _stock = stock;
    }

    private IQueryable<OrderLineTeam> Allotments => _uow.Repository<OrderLineTeam>().Query();

    public async Task<IReadOnlyList<OrderOptionDto>> GetOpenOrdersAsync(string? keyword, int? sewingTeamId, CancellationToken ct)
    {
        var kw = keyword?.Trim();
        var teamId = SewingTeamScope.Of(_user) ?? sewingTeamId;
        var allotments = Allotments;
        return await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => !OrderStatuses.Closed.Contains(o.Status))
            .Where(o => teamId == null || o.OrderLines.Any(l => allotments.Any(a => a.SewingTeamId == teamId && a.OrderLineId == l.OrderLineId)))
            .Where(o => string.IsNullOrEmpty(kw) || o.OrderCode.Contains(kw))
            .OrderByDescending(o => o.OrderId).Take(MaxOptions)
            .Select(o => new OrderOptionDto(o.OrderId, o.OrderCode, o.Status, o.MaterialStatus, o.DueDate, o.ReceivedDate))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OrderLineOptionDto>> GetOrderLinesAsync(int orderId, CancellationToken ct)
    {
        var teamId = SewingTeamScope.Of(_user);
        var allotments = Allotments;
        return await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == orderId)
            .Where(l => teamId == null || allotments.Any(a => a.SewingTeamId == teamId && a.OrderLineId == l.OrderLineId))
            .OrderBy(l => l.ProductVariant.Product.ProductCode).ThenBy(l => l.ProductVariant.ProductColor.Color).ThenBy(l => l.ProductVariant.SizeCode)
            .Select(l => new OrderLineOptionDto(l.OrderLineId, l.ProductId, l.ProductVariant.Product.ProductCode, l.ProductVariant.Product.ProductName,
                l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode, l.OrderedQty,
                l.WorkOrderLines.Where(w => w.ProductComponentMaterialId == null && w.WorkOrder.Status != WorkOrderStatuses.Cancelled
                                            && w.WorkOrder.SourceRequestId == null).Sum(w => (int?)w.PlannedQty) ?? 0))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ComponentOptionDto>> GetPanelsOfOrderLineAsync(int orderLineId, CancellationToken ct)
    {
        var colorId = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderLineId == orderLineId).Select(l => (int?)l.ProductVariant.ProductColorId).FirstOrDefaultAsync(ct);
        if (colorId is null) return [];
        // Mỗi dòng định mức (chi tiết vải × NVL) của đúng màu áo là một lựa chọn cắt bù (định mức theo màu, DEV-ALL-87).
        return await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => p.ProductColorId == colorId && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.ProductComponent.Status == RecordStatus.Active && p.Material.MaterialType == MaterialTypes.Fabric)
            .OrderBy(p => p.ProductComponentId).ThenBy(p => p.ProductComponentMaterialId)
            .Select(p => new ComponentOptionDto(p.ProductComponentMaterialId,
                p.ProductComponent.ComponentName + " – " + p.Material.MaterialName + (p.Material.Color == "" ? "" : " " + p.Material.Color),
                p.ProductComponent.PiecesPerGarment))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TeamOptionDto>> GetTeamsAsync(string? teamType, CancellationToken ct) =>
        await _uow.Repository<Team>().Query().AsNoTracking()
            .Where(t => t.Status == RecordStatus.Active && (string.IsNullOrEmpty(teamType) || t.TeamType == teamType))
            .OrderBy(t => t.TeamName)
            .Select(t => new TeamOptionDto(t.TeamId, t.TeamCode, t.TeamName, t.TeamType))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MaterialOptionDto>> GetActiveMaterialsAsync(string? keyword, CancellationToken ct)
    {
        var kw = keyword?.Trim();
        return await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => m.Status == MaterialStatuses.Active && m.MaterialType != MaterialTypes.Scrap)
            .Where(m => string.IsNullOrEmpty(kw) || m.MaterialCode.Contains(kw) || m.MaterialName.Contains(kw) || m.Color.Contains(kw))
            .OrderBy(m => m.MaterialCode).Take(MaxOptions)
            .Select(m => new MaterialOptionDto(m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType ?? string.Empty, m.Unit, m.Color))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaterialOptionDto>> GetScrapMaterialsAsync(CancellationToken ct) =>
        await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => m.Status == MaterialStatuses.Active && m.MaterialType == MaterialTypes.Scrap)
            .OrderBy(m => m.MaterialCode)
            .Select(m => new MaterialOptionDto(m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType ?? string.Empty, m.Unit, m.Color))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RequisitionMaterialOptionDto>> GetRequisitionMaterialsAsync(int? orderId, CancellationToken ct)
    {
        var ofOrder = _uow.Repository<OrderMaterial>().Query().Where(o => o.OrderId == orderId).Select(o => o.MaterialId);
        var query = _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => m.Status == MaterialStatuses.Active && m.MaterialType != MaterialTypes.Scrap
                        && (orderId == null || ofOrder.Contains(m.MaterialId)))
            .OrderBy(m => m.MaterialCode);
        // NVL của một đơn hàng là danh sách ngắn, lấy đủ; cả danh mục thì giới hạn như ô chọn NVL thường.
        var materials = await (orderId == null ? query.Take(MaxOptions) : query)
            .Select(m => new { m.MaterialId, m.MaterialCode, m.MaterialName, MaterialType = m.MaterialType ?? string.Empty, m.Unit, m.Color })
            .ToListAsync(ct);
        var available = await _stock.GetMaterialAvailableAsync(materials.Select(m => m.MaterialId).ToList(), ct);
        return materials.Select(m => new RequisitionMaterialOptionDto(m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType,
            m.Unit, m.Color, Math.Max(0, available.GetValueOrDefault(m.MaterialId)))).ToList();
    }

    public async Task<IReadOnlyList<RequisitionPieceOptionDto>> GetRequisitionPiecesAsync(int orderId, CancellationToken ct)
    {
        var lines = await GetOrderLinesAsync(orderId, ct);
        var available = await _stock.GetSetAvailableAsync(lines.Select(l => l.OrderLineId).ToList(), ct);
        return lines.Select(l => new RequisitionPieceOptionDto(l.OrderLineId, l.ProductCode, l.ProductName, l.Color, l.SizeCode,
            l.OrderedQty, Math.Max(0, available.GetValueOrDefault(l.OrderLineId)))).ToList();
    }
}
