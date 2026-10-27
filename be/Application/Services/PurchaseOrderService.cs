using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Đơn mua NVL: Quản đốc tạo, có thể không gắn đơn hàng (mua kim, chỉ dùng chung nhiều đơn).</summary>
public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IDocumentCodeGenerator _codes;
    private readonly IClock _clock;
    private readonly IMaterialService _materials;

    public PurchaseOrderService(IUnitOfWork uow, IDocumentCodeGenerator codes, IClock clock, IMaterialService materials)
    {
        _uow = uow; _codes = codes; _clock = clock; _materials = materials;
    }

    private IQueryable<PurchaseOrder> PurchaseOrders => _uow.Repository<PurchaseOrder>().Query();

    public async Task<Result<PagedResult<PurchaseOrderListItemDto>>> GetPagedAsync(PurchaseOrderQuery query,
        CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await PurchaseOrders.AsNoTracking()
            .Where(p => string.IsNullOrEmpty(query.GoodsStatus) || p.GoodsStatus == query.GoodsStatus)
            .Where(p => query.StockOnly == null || (p.OrderId == null) == query.StockOnly)
            .Where(p => query.Receivable != true || p.GoodsStatus == PurchaseGoodsStatuses.Ordered
                        || p.GoodsStatus == PurchaseGoodsStatuses.PartiallyReceived)
            .Where(p => string.IsNullOrEmpty(keyword) || p.PoCode.Contains(keyword)
                || (p.Order != null && p.Order.OrderCode.Contains(keyword)))
            .OrderByDescending(p => p.PurchaseOrderId)
            .Select(p => new PurchaseOrderListItemDto(p.PurchaseOrderId, p.PoCode, p.OrderId,
                p.Order == null ? null : p.Order.OrderCode, p.Supplier == null ? null : p.Supplier.SupplierName,
                p.GoodsStatus, p.PaymentStatus, p.CreatedAt, p.PurchaseOrderLines.Count))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<PurchaseOrderListItemDto>>.Success(page);
    }

    public async Task<Result<PurchaseOrderDto>> GetByIdAsync(int purchaseOrderId, CancellationToken ct)
    {
        var dto = await PurchaseOrders.AsNoTracking().Where(p => p.PurchaseOrderId == purchaseOrderId)
            .Select(p => new PurchaseOrderDto(p.PurchaseOrderId, p.PoCode, p.OrderId,
                p.Order == null ? null : p.Order.OrderCode, p.SupplierId,
                p.Supplier == null ? null : p.Supplier.SupplierName, p.GoodsStatus, p.PaymentStatus, p.Note,
                p.CreatedAt,
                p.PurchaseOrderLines.OrderBy(l => l.PurchaseOrderLineId).Select(l => new PurchaseOrderLineDto(
                    l.PurchaseOrderLineId, l.OrderMaterialId,
                    l.OrderMaterial != null ? l.OrderMaterial.MaterialId : l.MaterialId!.Value,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialCode : l.Material!.MaterialCode,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialName : l.Material!.MaterialName,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.Unit : l.Material!.Unit,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.Color : l.Material!.Color,
                    l.OrderedQty, l.UnitPrice, l.Amount, 0m, l.NotReplenishedQty)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<PurchaseOrderDto>();

        // Số đã nhận = Σ số đạt của phiếu nhập đã xác nhận (cùng cách tính với vw_PurchaseOrderLineStatus).
        var lineIds = dto.Lines.Select(l => l.PurchaseOrderLineId).ToList();
        var received = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.PurchaseOrderLineId != null && lineIds.Contains(l.PurchaseOrderLineId.Value)
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.PurchaseOrderLineId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        return Result<PurchaseOrderDto>.Success(dto with
        {
            Lines = dto.Lines.Select(l => l with { ReceivedQty = received.GetValueOrDefault(l.PurchaseOrderLineId) }).ToList(),
        });
    }

    public async Task<Result<PurchaseOrderDto>> CreateAsync(CreatePurchaseOrderDto dto, CancellationToken ct)
    {
        // Gắn đơn hàng: kiểm tra đơn trước để không tạo NVL mới vô ích.
        var orderCheck = await ValidateOrderAsync(dto.OrderId, ct);
        if (orderCheck is not null) return orderCheck;

        var resolved = await ResolveMaterialsAsync(dto.Lines, ct);
        if (!resolved.IsSuccess) return WarehouseFailures.From<PurchaseOrderDto>(resolved);
        var lines = dto.Lines.Select((l, i) => (MaterialId: resolved.Data![i],
            l.OrderedQty, l.UnitPrice)).ToList();
        // Hai dòng nhập NVL mới trùng một NVL đã có (cùng tên + màu) → gộp sẽ vi phạm UQ_POL_Stock / UQ_POL.
        if (lines.Select(l => l.MaterialId).Distinct().Count() != lines.Count)
            return Result<PurchaseOrderDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DuplicateLine);

        var materialIds = lines.Select(l => l.MaterialId).Distinct().ToList();
        var activeCount = await _uow.Repository<Material>().Query()
            .CountAsync(m => materialIds.Contains(m.MaterialId) && m.Status == MaterialStatuses.Active, ct);
        if (activeCount != materialIds.Count)
            return Result<PurchaseOrderDto>.Failure(ErrorCodes.WarehouseMaterialInactive, WarehouseMessages.MaterialInactive);

        // Gắn đơn hàng: dòng phải trỏ đúng NVL đã khai báo của đơn (UC09). Không gắn: mua dự trữ.
        Dictionary<int, int> orderMaterials = new();
        if (dto.OrderId is { } orderId)
        {
            var rows = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
                .Where(om => om.OrderId == orderId && materialIds.Contains(om.MaterialId))
                .Select(om => new { om.MaterialId, om.OrderMaterialId }).ToListAsync(ct);
            orderMaterials = rows.ToDictionary(r => r.MaterialId, r => r.OrderMaterialId);
            if (lines.Any(l => !orderMaterials.ContainsKey(l.MaterialId)))
                return Result<PurchaseOrderDto>.Failure(ErrorCodes.WarehouseInvalidReference,
                    WarehouseMessages.PurchaseOrderMaterialNotInOrder);
        }

        var created = await _uow.InTransactionAsync(async () =>
        {
            var po = new PurchaseOrder
            {
                PoCode = await _codes.NextAsync<PurchaseOrder>(DocumentPrefixes.PurchaseOrder, p => p.PoCode, ct),
                OrderId = dto.OrderId,
                GoodsStatus = PurchaseOrderStatuses.PendingFinalisation,
                PaymentStatus = PurchaseOrderStatuses.Unpaid,
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                CreatedAt = _clock.Now,
                PurchaseOrderLines = lines.Select(l => orderMaterials.TryGetValue(l.MaterialId, out var omId)
                    ? new PurchaseOrderLine { OrderMaterialId = omId, OrderedQty = l.OrderedQty, UnitPrice = l.UnitPrice }
                    : new PurchaseOrderLine
                    {
                        MaterialId = l.MaterialId,
                        OrderedQty = l.OrderedQty,
                        UnitPrice = l.UnitPrice,
                    }).ToList(),
            };
            await _uow.Repository<PurchaseOrder>().AddAsync(po);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(po.PurchaseOrderId);
        });
        return await GetByIdAsync(created.Data, ct);
    }

    private async Task<Result<PurchaseOrderDto>?> ValidateOrderAsync(int? orderId, CancellationToken ct)
    {
        if (orderId is null) return null;
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return WarehouseFailures.NotFound<PurchaseOrderDto>();
        return OrderStatuses.Closed.Contains(order.Status)
            ? Result<PurchaseOrderDto>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed)
            : null;
    }

    /// <summary>Đổi mỗi dòng thành MaterialId; NVL nhập tay chưa có thì thêm vào danh mục (phụ liệu, nhóm OTHER).</summary>
    private async Task<Result<List<int>>> ResolveMaterialsAsync(IReadOnlyList<SavePurchaseOrderLineDto> lines,
        CancellationToken ct)
    {
        var ids = new List<int>();
        foreach (var line in lines)
        {
            if (line.MaterialId is { } existing) { ids.Add(existing); continue; }

            var name = line.NewMaterialName!.Trim();
            var color = line.NewMaterialColor?.Trim() ?? string.Empty;
            var found = await _uow.Repository<Material>().Query().AsNoTracking()
                .FirstOrDefaultAsync(m => m.MaterialName == name && m.MaterialType == MaterialTypes.Trim && m.Color == color, ct);
            if (found is not null)
            {
                if (found.Status != MaterialStatuses.Active)
                    return Result<List<int>>.Failure(ErrorCodes.WarehouseMaterialInactive, WarehouseMessages.MaterialInactive);
                ids.Add(found.MaterialId);
                continue;
            }

            var created = await _materials.CreateAsync(
                new CreateMaterialDto(null, name, MaterialTypes.Trim, "OTHER", line.NewMaterialUnit!.Trim(), color), ct);
            if (!created.IsSuccess)
                return Result<List<int>>.Failure(created.ErrorCode!, created.Message!, created.Kind, created.Errors);
            ids.Add(created.Data!.MaterialId);
        }
        return Result<List<int>>.Success(ids);
    }
}
