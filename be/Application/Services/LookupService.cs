using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class LookupService : ILookupService
{
    private const int MaxOptions = 100;
    private readonly IUnitOfWork _uow;

    public LookupService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<OrderOptionDto>> GetOpenOrdersAsync(string? keyword, CancellationToken ct)
    {
        var kw = keyword?.Trim();
        return await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => !OrderStatuses.Closed.Contains(o.Status))
            .Where(o => string.IsNullOrEmpty(kw) || o.OrderCode.Contains(kw))
            .OrderByDescending(o => o.OrderId).Take(MaxOptions)
            .Select(o => new OrderOptionDto(o.OrderId, o.OrderCode, o.Status, o.MaterialStatus, o.DueDate))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OrderLineOptionDto>> GetOrderLinesAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == orderId)
            .OrderBy(l => l.Product.ProductCode).ThenBy(l => l.Color).ThenBy(l => l.SizeCode)
            .Select(l => new OrderLineOptionDto(l.OrderLineId, l.ProductId, l.Product.ProductCode, l.Product.ProductName,
                l.Color, l.SizeCode, l.OrderedQty))
            .ToListAsync(ct);

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
            .Where(m => m.Status == MaterialStatuses.Active)
            .Where(m => string.IsNullOrEmpty(kw) || m.MaterialCode.Contains(kw) || m.MaterialName.Contains(kw))
            .OrderBy(m => m.MaterialCode).Take(MaxOptions)
            .Select(m => new MaterialOptionDto(m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType, m.Unit))
            .ToListAsync(ct);
    }
}
