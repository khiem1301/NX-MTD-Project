using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Bìa cắt của đơn (DEV-ALL-85): phía trên giác theo tỉ lệ size của từng đơn. Mỗi bìa thuộc đơn × mã hàng × nhóm bìa (khai ở mã hàng)
/// (DEV-ALL-89: nhóm thuộc một dạng bìa, nên bìa dùng cho đúng các màu của dạng đó; màu cần bìa riêng thì nằm ở dạng bìa riêng),
/// có mã bìa, chiều dài (m) và tỉ lệ size. Chỉ dùng để ước số mét vải khi lập lệnh cắt (WorkOrderFabricChecker); bàn cắt không gắn bìa
/// nên bìa sửa, xóa được bất cứ lúc nào khi đơn còn chạy.
/// </summary>
public sealed class OrderMarkerService : IOrderMarkerService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;

    public OrderMarkerService(IUnitOfWork uow, IAuditLogService audit)
    {
        _uow = uow; _audit = audit;
    }

    /// <summary>Dòng đơn của một mã hàng: màu, size (để chọn màu của bìa và kiểm size của tỉ lệ).</summary>
    private sealed record LineColor(int ProductId, string ProductCode, string ProductName, int ProductColorId, string Color, string SizeCode);

    public async Task<Result<OrderMarkersDto>> GetAsync(int orderId, CancellationToken ct)
    {
        if (!await _uow.Repository<CustomerOrder>().Query().AnyAsync(o => o.OrderId == orderId, ct))
            return Fail.NotFound<OrderMarkersDto>();

        var lines = await LinesAsync(l => l.OrderId == orderId, ct);
        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var groups = await _uow.Repository<ProductMarkerFabric>().Query().AsNoTracking().Where(f => productIds.Contains(f.ProductId))
            .Select(f => new
            {
                f.ProductId,
                f.MarkerNo,
                f.ProductColorId,
                Fabric = new MarkerGroupFabricDto(f.MaterialId, f.Material.MaterialCode, f.Material.MaterialName, f.Material.Color,
                    f.Material.WidthM),
            }).ToListAsync(ct);
        var markers = await _uow.Repository<ProductMarker>().Query().AsNoTracking().Where(m => m.OrderId == orderId)
            .OrderBy(m => m.MarkerNo).ThenBy(m => m.MarkerCode)
            .Select(m => new
            {
                m.ProductId,
                Dto = new OrderMarkerDto(m.ProductMarkerId, m.MarkerNo, m.MarkerCode, m.LengthM, m.WidthM, m.Status,
                    m.ProductMarkerRatios.OrderBy(r => r.SizeCode).Select(r => new MarkerRatioDto(r.SizeCode, r.PiecesPerPly)).ToList()),
            })
            .ToListAsync(ct);

        var products = lines.GroupBy(l => l.ProductId).OrderBy(g => g.First().ProductCode)
            .Select(g => new OrderMarkerProductDto(g.Key, g.First().ProductCode, g.First().ProductName,
                SizeOrder.Sort(g.Select(l => l.SizeCode)),
                groups.Where(x => x.ProductId == g.Key).GroupBy(x => (int)x.MarkerNo).OrderBy(x => x.Key)
                    .Select(x => new OrderMarkerGroupDto(x.Key, g.Where(l => x.Any(c => c.ProductColorId == l.ProductColorId))
                        .Select(l => l.Color).Distinct().Order().ToList(),
                        // DEV-ALL-104: vải của nhóm ở các màu của đơn, để màn bìa cảnh báo bìa rộng hơn khổ vải.
                        x.Where(c => g.Any(l => l.ProductColorId == c.ProductColorId)).Select(c => c.Fabric)
                            .DistinctBy(f => f.MaterialId).OrderBy(f => f.MaterialCode).ToList()))
                    .ToList(),
                markers.Where(m => m.ProductId == g.Key).Select(m => m.Dto with { Status = CodeText.ToPascal(m.Dto.Status) }).ToList()))
            .ToList();
        return Result<OrderMarkersDto>.Success(new OrderMarkersDto(orderId, products));
    }

    private Task<List<LineColor>> LinesAsync(Expression<Func<OrderLine, bool>> filter, CancellationToken ct) =>
        _uow.Repository<OrderLine>().Query().AsNoTracking().Where(filter)
            .Select(l => new LineColor(l.ProductId, l.ProductVariant.Product.ProductCode, l.ProductVariant.Product.ProductName,
                l.ProductVariant.ProductColorId, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode))
            .ToListAsync(ct);

    public async Task<Result<OrderMarkersDto>> SaveAsync(int orderId, int productId, SaveOrderMarkersDto dto, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return Fail.NotFound<OrderMarkersDto>();
        if (OrderStatuses.Closed.Contains(order.Status)) return Invalid(Messages.OrderMarkerOrderClosed);

        var existing = await _uow.Repository<ProductMarker>().Query().Include(m => m.ProductMarkerRatios)
            .Where(m => m.OrderId == orderId && m.ProductId == productId).ToListAsync(ct);
        var lines = await LinesAsync(l => l.OrderId == orderId && l.ProductId == productId, ct);
        var check = await ValidateAsync(productId, dto, existing, lines, ct);
        if (check is not null) return check;

        var saved = await _uow.InTransactionAsync(() => WriteAsync(orderId, productId, dto, existing));
        if (!saved.IsSuccess) return Fail.As<OrderMarkersDto>(saved);
        var result = await GetAsync(orderId, ct);
        return Result<OrderMarkersDto>.Success(result.Data!, Messages.Saved);
    }

    /// <summary>Mã hàng có trong đơn, nhóm bìa đã khai vải, mã bìa không trùng trong nhóm, Id là bìa của đúng đơn và mã hàng, size hợp lệ.</summary>
    private async Task<Result<OrderMarkersDto>?> ValidateAsync(int productId, SaveOrderMarkersDto dto, List<ProductMarker> existing,
        List<LineColor> lines, CancellationToken ct)
    {
        if (lines.Count == 0) return Invalid(Messages.OrderMarkerProductNotInOrder);
        if (dto.Markers.Any(m => m.WidthM is null)) return Invalid(Messages.Required(WidthLabels.Marker.ToLowerInvariant()));

        var existingIds = existing.Select(m => m.ProductMarkerId).ToHashSet();
        if (dto.Markers.Any(m => m.ProductMarkerId is { } id && !existingIds.Contains(id))) return Fail.NotFound<OrderMarkersDto>();
        if (dto.Markers.GroupBy(m => (m.MarkerNo, Upper(m.MarkerCode))).Any(g => g.Count() > 1))
            return Invalid(Messages.OrderMarkerCodeDuplicate);

        var groupColors = (await _uow.Repository<ProductMarkerFabric>().Query().Where(f => f.ProductId == productId)
                .Select(f => new { f.MarkerNo, f.ProductColorId }).ToListAsync(ct))
            .Select(f => ((int)f.MarkerNo, f.ProductColorId)).ToHashSet();
        var noFabric = dto.Markers.Select(m => m.MarkerNo).Where(n => groupColors.All(g => g.Item1 != n)).Distinct().ToList();
        if (noFabric.Count > 0) return Invalid(string.Format(Messages.OrderMarkerGroupWithoutFabric, string.Join(", ", noFabric)));

        var problem = dto.Markers.Select(m => MarkerSizeProblem(m, lines, groupColors)).FirstOrDefault(p => p is not null);
        return problem is null ? null : Invalid(problem);
    }

    /// <summary>
    /// DEV-ALL-89: bìa dùng cho các màu của đơn dùng nhóm bìa (cùng dạng bìa), nên size trong tỉ lệ phải có trong đơn ở các màu đó.
    /// Trả về thông báo lỗi hoặc null.
    /// </summary>
    private static string? MarkerSizeProblem(SaveOrderMarkerDto marker, List<LineColor> lines, HashSet<(int, int)> groupColors)
    {
        var sizes = lines.Where(l => groupColors.Contains((marker.MarkerNo, l.ProductColorId))).Select(l => Upper(l.SizeCode)).ToHashSet();
        var wrong = marker.Ratios.Select(r => Upper(r.SizeCode)).Where(s => !sizes.Contains(s)).Distinct().ToList();
        return wrong.Count == 0 ? null : string.Format(Messages.OrderMarkerSizeNotInOrder, Upper(marker.MarkerCode), string.Join(", ", wrong));
    }

    /// <summary>Xóa bìa bỏ đi và tỉ lệ cũ trước (khóa UQ_PM), rồi cập nhật / thêm bìa và ghi lại tỉ lệ.</summary>
    private async Task<Result<int>> WriteAsync(int orderId, int productId, SaveOrderMarkersDto dto, List<ProductMarker> existing)
    {
        var keptIds = dto.Markers.Where(m => m.ProductMarkerId is not null).Select(m => m.ProductMarkerId!.Value).ToHashSet();
        _uow.Repository<ProductMarkerRatio>().RemoveRange(existing.SelectMany(m => m.ProductMarkerRatios).ToList());
        _uow.Repository<ProductMarker>().RemoveRange(existing.Where(m => !keptIds.Contains(m.ProductMarkerId)).ToList());
        await _uow.SaveChangesAsync();

        var byId = existing.ToDictionary(m => m.ProductMarkerId);
        foreach (var input in dto.Markers)
        {
            var marker = input.ProductMarkerId is { } id ? byId[id] : new ProductMarker { OrderId = orderId, ProductId = productId };
            marker.MarkerNo = (byte)input.MarkerNo;
            marker.MarkerCode = input.MarkerCode.Trim().ToUpperInvariant();
            marker.LengthM = input.LengthM;
            marker.WidthM = input.WidthM ?? 0;   // ValidateAsync đã chặn thiếu khổ (DEV-ALL-102)
            marker.Status = CodeText.ToCode(input.Status);
            marker.ProductMarkerRatios = input.Ratios.Select(r => new ProductMarkerRatio
            {
                SizeCode = Upper(r.SizeCode),
                PiecesPerPly = r.PiecesPerPly,
            }).ToList();
            if (input.ProductMarkerId is null) await _uow.Repository<ProductMarker>().AddAsync(marker);
        }
        _audit.Add(new AuditEntry(nameof(CustomerOrder), orderId, AuditActions.Update, new { Markers = existing.Count },
            new { Markers = dto.Markers.Count, ProductId = productId }, null, Messages.OrderMarkerAuditSummary));
        await _uow.SaveChangesAsync();
        return Result<int>.Success(orderId);
    }

    private static string Upper(string value) => value.Trim().ToUpperInvariant();

    private static Result<OrderMarkersDto> Invalid(string message) =>
        Result<OrderMarkersDto>.Failure(ErrorCodes.OrderMarkerInvalid, message);
}
