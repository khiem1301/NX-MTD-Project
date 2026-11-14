using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Đơn mua NVL: Quản đốc tạo là đặt hàng luôn (nhà cung cấp, số lượng, đơn giá, ngày đặt, số lần giao), có thể không gắn đơn hàng
/// (mua kim, chỉ dùng chung nhiều đơn). Đơn do luồng NVL lỗi dựng sẵn mới ở trạng thái chờ chốt và đặt hàng ở PurchaseOrderFlowService.
/// </summary>
public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IDocumentCodeGenerator _codes;
    private readonly IClock _clock;
    private readonly IMaterialService _materials;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly DeliveryScheduleStore _schedule;

    private const string PaymentLink = "/purchase-payments?id={0}";
    private const string IncomingLink = "/warehouse/incoming-materials";

    public PurchaseOrderService(IUnitOfWork uow, IDocumentCodeGenerator codes, IClock clock, IMaterialService materials,
        INotificationService notifier, IAuditLogService audit)
    {
        _uow = uow; _codes = codes; _clock = clock; _materials = materials; _notifier = notifier; _audit = audit;
        _schedule = new DeliveryScheduleStore(uow);
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
                    l.OrderedQty, l.UnitPrice, l.Amount, 0m, l.NotReplenishedQty)).ToList(),
                p.ExpectedDeliveryCount, 0))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<PurchaseOrderDto>();

        // Số đã nhận = Σ số đạt của phiếu nhập đã xác nhận (cùng cách tính với vw_PurchaseOrderLineStatus).
        var lineIds = dto.Lines.Select(l => l.PurchaseOrderLineId).ToList();
        var received = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.PurchaseOrderLineId != null && lineIds.Contains(l.PurchaseOrderLineId.Value)
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.PurchaseOrderLineId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        // Số lần đơn mua đã giao (dùng chung cả đơn) = số phiếu thường đã xác nhận (DEV-ALL-95).
        var deliveries = await _uow.Repository<VwPurchaseOrderDelivery>().Query().AsNoTracking()
            .Where(v => v.PurchaseOrderId == purchaseOrderId).Select(v => v.ReceivedCount).FirstOrDefaultAsync(ct);
        return Result<PurchaseOrderDto>.Success(dto with
        {
            ReceivedCount = deliveries,
            Lines = dto.Lines.Select(l => l with { ReceivedQty = received.GetValueOrDefault(l.PurchaseOrderLineId) }).ToList(),
        });
    }

    public async Task<Result<PurchaseOrderDto>> CreateAsync(CreatePurchaseOrderDto dto, CancellationToken ct)
    {
        // Gắn đơn hàng: kiểm tra đơn trước để không tạo NVL mới vô ích.
        var orderCheck = await ValidateOrderAsync(dto.OrderId, ct);
        if (orderCheck is not null) return orderCheck;
        var supplierCheck = await ValidateSupplierAsync(dto.SupplierId, ct);
        if (supplierCheck is not null) return supplierCheck;
        if (dto.OrderDate > _clock.Today)
            return Result<PurchaseOrderDto>.Failure(ErrorCodes.PurchaseOrderDateInvalid, Messages.PurchaseOrderOrderDateFuture,
                ResultKind.Validation);
        var deliveryError = await PurchaseDeliveryDate.CheckAsync(_uow, _clock, dto.ExpectedDeliveryDates, dto.OrderDate, dto.OrderId, ct);
        if (deliveryError is not null)
            return Result<PurchaseOrderDto>.Failure(ErrorCodes.PurchaseOrderDateInvalid, deliveryError, ResultKind.Validation);

        var resolved = await ResolveMaterialsAsync(dto.Lines, ct);
        if (!resolved.IsSuccess) return WarehouseFailures.From<PurchaseOrderDto>(resolved);
        var lines = dto.Lines.Select((l, i) => new NewLine(resolved.Data![i], l.OrderedQty, l.UnitPrice)).ToList();
        var materialIds = lines.Select(l => l.MaterialId).Distinct().ToList();
        // Hai dòng nhập NVL mới trùng một NVL đã có (cùng tên + màu) → gộp sẽ vi phạm UQ_POL_Stock / UQ_POL.
        if (materialIds.Count != lines.Count)
            return Result<PurchaseOrderDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DuplicateLine);

        var activeCount = await _uow.Repository<Material>().Query()
            .CountAsync(m => materialIds.Contains(m.MaterialId) && m.Status == MaterialStatuses.Active, ct);
        if (activeCount != materialIds.Count)
            return Result<PurchaseOrderDto>.Failure(ErrorCodes.WarehouseMaterialInactive, WarehouseMessages.MaterialInactive);

        var orderMaterials = await MapOrderMaterialsAsync(dto.OrderId, materialIds, ct);
        if (dto.OrderId is not null && lines.Any(l => !orderMaterials.ContainsKey(l.MaterialId)))
            return Result<PurchaseOrderDto>.Failure(ErrorCodes.WarehouseInvalidReference,
                WarehouseMessages.PurchaseOrderMaterialNotInOrder);

        var created = await _uow.InTransactionAsync(() => AddOrderAsync(dto, lines, orderMaterials, ct));
        return await GetByIdAsync(created.Data, ct);
    }

    private sealed record NewLine(int MaterialId, decimal OrderedQty, decimal UnitPrice);

    /// <summary>
    /// Gắn đơn hàng: dòng phải trỏ đúng NVL đã khai báo của đơn (UC09), trả về NVL → OrderMaterialId. Không gắn: mua dự trữ,
    /// trả về từ điển rỗng.
    /// </summary>
    private async Task<Dictionary<int, int>> MapOrderMaterialsAsync(int? orderId, List<int> materialIds, CancellationToken ct)
    {
        if (orderId is not { } id) return new Dictionary<int, int>();
        var rows = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(om => om.OrderId == id && materialIds.Contains(om.MaterialId))
            .Select(om => new { om.MaterialId, om.OrderMaterialId }).ToListAsync(ct);
        return rows.ToDictionary(r => r.MaterialId, r => r.OrderMaterialId);
    }

    private async Task<Result<int>> AddOrderAsync(CreatePurchaseOrderDto dto, List<NewLine> lines,
        Dictionary<int, int> orderMaterials, CancellationToken ct)
    {
        var deliveryDates = DeliveryScheduleStore.Summary(dto.ExpectedDeliveryDates ?? []);
        var po = new PurchaseOrder
        {
            PoCode = await _codes.NextAsync<PurchaseOrder>(DocumentPrefixes.PurchaseOrder, p => p.PoCode, ct),
            OrderId = dto.OrderId,
            SupplierId = dto.SupplierId,
            OrderDate = dto.OrderDate,
            ExpectedDeliveryCount = dto.ExpectedDeliveryCount,
            ExpectedDeliveryDate = deliveryDates.From,
            ExpectedDeliveryDateTo = deliveryDates.To,
            GoodsStatus = PurchaseGoodsStatuses.Ordered,
            PaymentStatus = PurchaseOrderStatuses.Unpaid,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            CreatedAt = _clock.Now,
            PurchaseOrderLines = lines.Select(l => orderMaterials.TryGetValue(l.MaterialId, out var omId)
                ? new PurchaseOrderLine { OrderMaterialId = omId, OrderedQty = l.OrderedQty, UnitPrice = l.UnitPrice }
                : new PurchaseOrderLine { MaterialId = l.MaterialId, OrderedQty = l.OrderedQty, UnitPrice = l.UnitPrice })
                .ToList(),
        };
        await _uow.Repository<PurchaseOrder>().AddAsync(po);
        await _uow.SaveChangesAsync();   // log và thông báo cần PurchaseOrderId nên lưu trước (cùng transaction)
        await _schedule.ReplaceAsync(DeliveryOwner.PurchaseOrder, po.PurchaseOrderId, dto.ExpectedDeliveryDates ?? [], ct);

        var supplier = await _uow.Repository<Supplier>().Query().AsNoTracking()
            .Where(s => s.SupplierId == dto.SupplierId).Select(s => s.SupplierName).FirstAsync(ct);
        _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Insert, null,
            new { po.GoodsStatus, po.SupplierId, po.OrderDate, po.ExpectedDeliveryCount, po.ExpectedDeliveryDate, po.ExpectedDeliveryDateTo,
                ExpectedDeliveryDates = DocumentDateRules.FormatDates(dto.ExpectedDeliveryDates ?? []), Lines = po.PurchaseOrderLines.Count }));
        await _notifier.NotifyRoleAsync(RoleCodes.Accountant, new NotificationMessage(NotificationSources.Warehouse,
            Messages.NotifyPoPlacedTitle, string.Format(Messages.NotifyPoPlacedBody, po.PoCode, supplier),
            string.Format(PaymentLink, po.PurchaseOrderId), nameof(PurchaseOrder), po.PurchaseOrderId), ct);
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            Messages.NotifyIncomingPoTitle, string.Format(Messages.NotifyIncomingPoBody, po.PoCode, supplier),
            IncomingLink, nameof(PurchaseOrder), po.PurchaseOrderId), ct);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(po.PurchaseOrderId);
    }

    /// <summary>Đơn mua phải gắn nhà cung cấp đang hợp tác ngay lúc tạo.</summary>
    private async Task<Result<PurchaseOrderDto>?> ValidateSupplierAsync(int supplierId, CancellationToken ct)
    {
        var supplier = await _uow.Repository<Supplier>().Query().AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);
        if (supplier is null) return Result<PurchaseOrderDto>.Failure(ErrorCodes.CommonNotFound, Messages.SupplierNotFound, ResultKind.NotFound);
        return supplier.Status == RecordStatus.Active
            ? null
            : Result<PurchaseOrderDto>.Failure(ErrorCodes.SupplierNotActive, Messages.SupplierNotActive);
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
