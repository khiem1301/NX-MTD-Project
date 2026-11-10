using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC09 (phần NVL): NVL khách cấp / phải mua của đơn; số cần nhập tay theo số khách báo.</summary>
public sealed class OrderMaterialService : IOrderMaterialService
{
    private const string IncomingLink = "/warehouse/incoming-materials";

    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifier;
    private readonly IClock _clock;

    public OrderMaterialService(IUnitOfWork uow, INotificationService notifier, IClock clock)
    {
        _uow = uow; _notifier = notifier; _clock = clock;
    }

    private IQueryable<OrderMaterial> OrderMaterials => _uow.Repository<OrderMaterial>().Query();

    public async Task<Result<IReadOnlyList<OrderMaterialDto>>> GetByOrderAsync(int orderId, CancellationToken ct)
    {
        var rows = await OrderMaterials.AsNoTracking().Where(om => om.OrderId == orderId)
            .Include(om => om.Material)
            .OrderBy(om => om.Material.MaterialType).ThenBy(om => om.Material.MaterialCode).ThenBy(om => om.Material.Color)
            .ToListAsync(ct);

        var received = await ReceivedQtyAsync(orderId, ct);
        var deliveries = await ReceivedCountsAsync(orderId, ct);

        return Result<IReadOnlyList<OrderMaterialDto>>.Success(rows
            .Select(om => ToDto(om, received.GetValueOrDefault(om.OrderMaterialId), deliveries.GetValueOrDefault(om.OrderMaterialId)))
            .ToList());
    }

    /// <summary>Số đã nhận tính từ phiếu nhập nên lọc trên danh sách đã dựng; số dòng của một đơn không lớn.</summary>
    public async Task<Result<PagedResult<OrderMaterialDto>>> GetPagedAsync(OrderMaterialPageQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var all = (await GetByOrderAsync(query.OrderId, ct)).Data!;
        var rows = all
            .Where(m => string.IsNullOrEmpty(keyword) || new[] { m.MaterialCode, m.MaterialName, m.MaterialColor }
                .Any(text => text?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true))
            .Where(m => query.Supply != OrderMaterialSupplies.Customer || m.CustomerQty > 0)
            .Where(m => query.Supply != OrderMaterialSupplies.Purchase || m.PurchaseQty > 0)
            .Where(m => string.IsNullOrEmpty(query.ReceiptState)
                        || IsFullyReceived(m) == (query.ReceiptState == OrderMaterialReceiptStates.Done))
            .ToList();
        var items = rows.Skip(query.Skip).Take(query.SafePageSize).ToList();
        return Result<PagedResult<OrderMaterialDto>>.Success(
            new PagedResult<OrderMaterialDto>(items, query.SafePage, query.SafePageSize, rows.Count));
    }

    private static bool IsFullyReceived(OrderMaterialDto m) => m.RequiredQty > 0 && m.ReceivedQty >= m.RequiredQty;

    /// <summary>Chỉ lấy NVL của những màu áo đơn đặt (định mức theo màu, DEV-ALL-87); NVL hoặc thành phần đã ngừng dùng không khai báo được nên bỏ qua.</summary>
    public async Task<Result<IReadOnlyList<OrderProductMaterialsDto>>> GetProductMaterialsAsync(int orderId, CancellationToken ct)
    {
        if (!await _uow.Repository<CustomerOrder>().Query().AnyAsync(o => o.OrderId == orderId, ct))
            return Fail.NotFound<IReadOnlyList<OrderProductMaterialsDto>>();

        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => l.OrderId == orderId)
            .Select(l => new { l.ProductVariant.ProductColorId, l.ProductId, l.ProductVariant.Product.ProductCode, l.ProductVariant.Product.ProductName })
            .ToListAsync(ct);
        var colorIds = lines.Select(l => l.ProductColorId).Distinct().ToList();
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(n => colorIds.Contains(n.ProductColorId) && n.Material.Status == MaterialStatuses.Active
                && n.ProductComponent.Status == RecordStatus.Active)
            .Select(n => new
            {
                n.ProductId,
                Material = new ProductMaterialOptionDto(n.MaterialId, n.Material.MaterialCode, n.Material.MaterialName,
                    n.Material.MaterialType ?? string.Empty, n.Material.Unit, n.Material.Color),
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<OrderProductMaterialsDto>>.Success(lines
            .GroupBy(l => (l.ProductId, l.ProductCode, l.ProductName))
            .OrderBy(g => g.Key.ProductCode)
            .Select(g => new OrderProductMaterialsDto(g.Key.ProductId, g.Key.ProductCode, g.Key.ProductName, norms
                .Where(n => n.ProductId == g.Key.ProductId)
                .Select(n => n.Material)
                .Distinct()
                .OrderBy(m => m.MaterialType).ThenBy(m => m.MaterialCode).ThenBy(m => m.Color)
                .ToList()))
            .ToList());
    }

    /// <summary>Kiểm tra hết các dòng rồi mới thêm và lưu một lần, nên một dòng lỗi thì không lưu dòng nào.</summary>
    public async Task<Result<IReadOnlyList<OrderMaterialDto>>> CreateBatchAsync(CreateOrderMaterialBatchDto dto, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return Fail.As<IReadOnlyList<OrderMaterialDto>>(NotFound());
        if (OrderStatuses.Closed.Contains(order.Status))
            return Fail.As<IReadOnlyList<OrderMaterialDto>>(Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderMaterialOrderClosed));

        var entities = new List<OrderMaterial>();
        foreach (var line in dto.Lines)
        {
            var entity = new OrderMaterial { OrderId = dto.OrderId };
            var check = await ApplyAsync(entity, line, ct);
            if (check is not null) return Fail.As<IReadOnlyList<OrderMaterialDto>>(check);
            entities.Add(entity);
        }

        foreach (var entity in entities)
        {
            await _uow.Repository<OrderMaterial>().AddAsync(entity);
            if (entity.CustomerQty > 0) await NotifyCustomerSupplyAsync(order.OrderId, order.OrderCode, entity, ct);
        }
        await _uow.SaveChangesAsync();
        return await GetByOrderAsync(dto.OrderId, ct);
    }

    public async Task<Result<OrderMaterialDto>> CreateAsync(CreateOrderMaterialDto dto, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return NotFound();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderMaterialOrderClosed);

        var entity = new OrderMaterial { OrderId = dto.OrderId };
        var check = await ApplyAsync(entity, dto, ct);
        if (check is not null) return check;

        await _uow.Repository<OrderMaterial>().AddAsync(entity);
        if (entity.CustomerQty > 0) await NotifyCustomerSupplyAsync(order.OrderId, order.OrderCode, entity, ct);
        await _uow.SaveChangesAsync();
        return await GetOneAsync(entity.OrderId, entity.OrderMaterialId, ct);
    }

    public async Task<Result<OrderMaterialDto>> UpdateAsync(int orderMaterialId, UpdateOrderMaterialDto dto,
        CancellationToken ct)
    {
        var entity = await OrderMaterials.FirstOrDefaultAsync(om => om.OrderMaterialId == orderMaterialId, ct);
        if (entity is null) return NotFound();

        if (await IsOrderClosedAsync(entity.OrderId, ct))
            return Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderMaterialOrderClosed);

        var oldCustomerQty = entity.CustomerQty;
        var oldDeliveryCount = entity.ExpectedDeliveryCount;
        var keyChanged = entity.MaterialId != dto.MaterialId;
        if (keyChanged && await HasReceiptsAsync(orderMaterialId, ct))
            return Failure(ErrorCodes.WarehouseOrderMaterialInUse, WarehouseMessages.OrderMaterialInUse);

        var check = await ApplyAsync(entity, dto, ct);
        if (check is not null) return check;

        // Báo Thủ kho khi số khách cấp hoặc số lần giao dự kiến thay đổi (không báo lại khi chỉ sửa ghi chú).
        if (entity.CustomerQty > 0 && (entity.CustomerQty != oldCustomerQty || entity.ExpectedDeliveryCount != oldDeliveryCount))
        {
            var orderCode = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
                .Where(o => o.OrderId == entity.OrderId).Select(o => o.OrderCode).FirstAsync(ct);
            await NotifyCustomerSupplyAsync(entity.OrderId, orderCode, entity, ct);
        }
        await _uow.SaveChangesAsync();
        return await GetOneAsync(entity.OrderId, entity.OrderMaterialId, ct);
    }

    public async Task<Result> DeleteAsync(int orderMaterialId, CancellationToken ct)
    {
        var entity = await OrderMaterials.FirstOrDefaultAsync(om => om.OrderMaterialId == orderMaterialId, ct);
        if (entity is null) return Result.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);
        if (await IsOrderClosedAsync(entity.OrderId, ct))
            return Result.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderMaterialOrderClosed);
        if (await HasReceiptsAsync(orderMaterialId, ct))
            return Result.Failure(ErrorCodes.WarehouseOrderMaterialInUse, WarehouseMessages.OrderMaterialInUse);

        _uow.Repository<OrderMaterial>().Remove(entity);
        await _uow.SaveChangesAsync();
        return Result.Success();
    }

    /// <summary>Kiểm tra nghiệp vụ rồi gán dữ liệu vào entity; trả Failure nếu sai, null nếu hợp lệ.</summary>
    private async Task<Result<OrderMaterialDto>?> ApplyAsync(OrderMaterial entity, IOrderMaterialInput dto,
        CancellationToken ct)
    {
        var material = await _uow.Repository<Material>().Query().AsNoTracking()
            .FirstOrDefaultAsync(m => m.MaterialId == dto.MaterialId, ct);
        if (material is null) return NotFound();
        if (material.Status != MaterialStatuses.Active
            && (entity.MaterialId != dto.MaterialId || entity.OrderMaterialId == 0))
            return Failure(ErrorCodes.WarehouseMaterialInactive, WarehouseMessages.MaterialInactive);

        // UQ_OM (đơn, NVL): NVL đã gồm màu nên mỗi màu là một dòng riêng.
        var duplicate = await OrderMaterials.AnyAsync(om => om.OrderId == entity.OrderId && om.MaterialId == dto.MaterialId
            && om.OrderMaterialId != entity.OrderMaterialId, ct);
        if (duplicate)
            return Failure(ErrorCodes.WarehouseOrderMaterialDuplicate, string.Format(WarehouseMessages.OrderMaterialDuplicateOf,
                MaterialLabel(material)));

        var dateError = await CheckExpectedDeliveryDateAsync(entity, dto, ct);
        if (dateError is not null) return Failure(ErrorCodes.WarehouseInvalidReference, dateError, ResultKind.Validation);

        entity.MaterialId = dto.MaterialId;
        entity.RequiredQty = dto.RequiredQty;
        entity.CustomerQty = dto.CustomerQty;
        entity.ExpectedDeliveryCount = entity.CustomerQty > 0 ? dto.ExpectedDeliveryCount : null;   // CK_OM_Delivery
        entity.ExpectedDeliveryDate = entity.CustomerQty > 0 ? dto.ExpectedDeliveryDate : null;   // CK_OM_DeliveryDate
        entity.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        return null;
    }

    /// <summary>
    /// Khách có cấp thì phải khai ngày dự kiến giao (không trước hôm nay, không sau hạn giao của đơn); khách không cấp thì để trống.
    /// Giữ nguyên ngày cũ (không đổi) thì không kiểm lại, để sửa ghi chú của đơn đã quá ngày vẫn được.
    /// </summary>
    private async Task<string?> CheckExpectedDeliveryDateAsync(OrderMaterial entity, IOrderMaterialInput dto, CancellationToken ct)
    {
        if (dto.CustomerQty <= 0)
            return dto.ExpectedDeliveryDate is null ? null : WarehouseMessages.ExpectedDeliveryNotAllowed;
        if (dto.ExpectedDeliveryDate is not { } date) return null;   // bắt buộc kiểm ở validator
        if (entity.ExpectedDeliveryDate == date) return null;

        var due = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => o.OrderId == entity.OrderId).Select(o => (DateOnly?)o.DueDate).FirstOrDefaultAsync(ct);
        return DocumentDateRules.CheckExpectedDelivery(date, _clock.Today, null, due);
    }

    private async Task<Result<OrderMaterialDto>> GetOneAsync(int orderId, int orderMaterialId, CancellationToken ct)
    {
        var all = await GetByOrderAsync(orderId, ct);
        var one = all.Data!.FirstOrDefault(x => x.OrderMaterialId == orderMaterialId);
        return one is null ? NotFound() : Result<OrderMaterialDto>.Success(one);
    }

    /// <summary>Thủ kho cần biết khách sẽ cấp NVL gì, bao nhiêu, dự kiến giao mấy lần để chuẩn bị nhận hàng.</summary>
    private async Task NotifyCustomerSupplyAsync(int orderId, string orderCode, OrderMaterial entity, CancellationToken ct)
    {
        var material = await _uow.Repository<Material>().Query().AsNoTracking().Where(m => m.MaterialId == entity.MaterialId)
            .Select(m => new { m.MaterialName, m.Unit }).FirstAsync(ct);
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            Messages.NotifyIncomingCustomerTitle,
            string.Format(Messages.NotifyIncomingCustomerBody, orderCode, entity.CustomerQty.ToString("0.###"), material.Unit,
                material.MaterialName, entity.ExpectedDeliveryCount),
            IncomingLink, nameof(CustomerOrder), orderId), ct);
    }

    private async Task<Dictionary<int, decimal>> ReceivedQtyAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.MaterialReceipt.OrderId == orderId && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed
                && l.OrderMaterialId != null)
            .GroupBy(l => l.OrderMaterialId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    /// <summary>Thêm, sửa, xóa NVL của đơn đều bị chặn khi đơn đã đóng (hủy, dừng, đã giao, hoàn tất), không chỉ thêm mới.</summary>
    private async Task<bool> IsOrderClosedAsync(int orderId, CancellationToken ct)
    {
        var status = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => o.OrderId == orderId).Select(o => o.Status).FirstOrDefaultAsync(ct);
        return status is not null && OrderStatuses.Closed.Contains(status);
    }

    private Task<bool> HasReceiptsAsync(int orderMaterialId, CancellationToken ct) =>
        _uow.Repository<MaterialReceiptLine>().Query().AnyAsync(l => l.OrderMaterialId == orderMaterialId, ct);

    /// <summary>Số phiếu nhập đã xác nhận của từng NVL khách cấp (vw_OrderMaterialDelivery.ReceivedCount).</summary>
    private async Task<Dictionary<int, int>> ReceivedCountsAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<VwOrderMaterialDelivery>().Query().AsNoTracking().Where(v => v.OrderId == orderId)
            .ToDictionaryAsync(v => v.OrderMaterialId, v => v.ReceivedCount, ct);

    private static OrderMaterialDto ToDto(OrderMaterial om, decimal receivedQty, int receivedCount) => new(
        om.OrderMaterialId, om.OrderId, om.MaterialId, om.Material.MaterialCode, om.Material.MaterialName,
        (om.Material.MaterialType ?? string.Empty), om.Material.Unit, om.Material.Color, om.RequiredQty, om.CustomerQty,
        om.ExpectedDeliveryCount, receivedCount, om.PurchaseQty, om.Source, om.Note, receivedQty,
        om.ExpectedDeliveryDate);

    private static string MaterialLabel(Material m) =>
        string.IsNullOrEmpty(m.Color) ? $"{m.MaterialCode} {m.MaterialName}" : $"{m.MaterialCode} {m.MaterialName} ({m.Color})";

    private static Result<OrderMaterialDto> NotFound() =>
        Result<OrderMaterialDto>.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    private static Result<OrderMaterialDto> Failure(string code, string message, ResultKind kind) =>
        Result<OrderMaterialDto>.Failure(code, message, kind);

    private static Result<OrderMaterialDto> Failure(string code, string message) =>
        Result<OrderMaterialDto>.Failure(code, message);
}
