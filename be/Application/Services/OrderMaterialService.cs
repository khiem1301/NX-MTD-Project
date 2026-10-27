using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC09 (phần NVL): NVL khách cấp / phải mua của đơn; số cần theo định mức đọc từ định mức của mã hàng (UC10).</summary>
public sealed class OrderMaterialService : IOrderMaterialService
{
    private const string IncomingLink = "/warehouse/incoming-materials";

    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifier;
    private readonly FabricPlanner _planner;

    public OrderMaterialService(IUnitOfWork uow, INotificationService notifier, FabricPlanner planner)
    {
        _uow = uow; _notifier = notifier; _planner = planner;
    }

    private IQueryable<OrderMaterial> OrderMaterials => _uow.Repository<OrderMaterial>().Query();

    public async Task<Result<IReadOnlyList<OrderMaterialDto>>> GetByOrderAsync(int orderId, CancellationToken ct)
    {
        var rows = await OrderMaterials.AsNoTracking().Where(om => om.OrderId == orderId)
            .Include(om => om.Material)
            .OrderBy(om => om.Material.MaterialType).ThenBy(om => om.Material.MaterialCode).ThenBy(om => om.Material.Color)
            .ToListAsync(ct);

        var norm = await _planner.GetOrderNormAsync(orderId, ct);
        var received = await ReceivedQtyAsync(orderId, ct);
        var deliveries = await ReceivedCountsAsync(orderId, ct);

        return Result<IReadOnlyList<OrderMaterialDto>>.Success(rows
            .Select(om => ToDto(om, norm.GetValueOrDefault(om.MaterialId),
                received.GetValueOrDefault(om.OrderMaterialId), deliveries.GetValueOrDefault(om.OrderMaterialId)))
            .ToList());
    }

    public async Task<Result<IReadOnlyList<PendingMaterialOrderDto>>> GetPendingOrdersAsync(CancellationToken ct)
    {
        var running = new[] { OrderStatuses.New, OrderStatuses.InProduction };
        var rows = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => running.Contains(o.Status) && o.MaterialStatus != OrderMaterialStatuses.Complete)
            .OrderBy(o => o.DueDate).ThenBy(o => o.OrderId)
            .Select(o => new PendingMaterialOrderDto(o.OrderId, o.OrderCode, o.Customer.CustomerName, o.DueDate, o.Status,
                o.MaterialStatus, o.OrderMaterials.Count, o.OrderMaterials.Sum(m => (decimal?)m.CustomerQty) ?? 0m,
                o.OrderMaterials.Sum(m => (decimal?)m.PurchaseQty) ?? 0m))
            .ToListAsync(ct);
        return Result<IReadOnlyList<PendingMaterialOrderDto>>.Success(rows
            .Select(r => r with { Status = CodeText.ToPascal(r.Status), MaterialStatus = CodeText.ToPascal(r.MaterialStatus) })
            .ToList());
    }

    public async Task<Result<OrderMaterialDto>> CreateAsync(CreateOrderMaterialDto dto, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return NotFound();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

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

        var oldCustomerQty = entity.CustomerQty;
        var oldDeliveryCount = entity.ExpectedDeliveryCount;
        var keyChanged = entity.MaterialId != dto.MaterialId;
        if (keyChanged && await HasReceiptsAsync(orderMaterialId, ct))
            return Fail(ErrorCodes.WarehouseOrderMaterialInUse, WarehouseMessages.OrderMaterialInUse);

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
            return Fail(ErrorCodes.WarehouseMaterialInactive, WarehouseMessages.MaterialInactive);

        // UQ_OM (đơn, NVL): NVL đã gồm màu nên mỗi màu là một dòng riêng.
        var duplicate = await OrderMaterials.AnyAsync(om => om.OrderId == entity.OrderId && om.MaterialId == dto.MaterialId
            && om.OrderMaterialId != entity.OrderMaterialId, ct);
        if (duplicate)
            return Fail(ErrorCodes.WarehouseOrderMaterialDuplicate, string.Format(WarehouseMessages.OrderMaterialDuplicateOf,
                MaterialLabel(material)));

        entity.MaterialId = dto.MaterialId;
        entity.RequiredQty = dto.RequiredQty;
        entity.CustomerQty = dto.CustomerQty;
        entity.ExpectedDeliveryCount = entity.CustomerQty > 0 ? dto.ExpectedDeliveryCount : null;   // CK_OM_Delivery
        entity.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        return null;
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

    private Task<bool> HasReceiptsAsync(int orderMaterialId, CancellationToken ct) =>
        _uow.Repository<MaterialReceiptLine>().Query().AnyAsync(l => l.OrderMaterialId == orderMaterialId, ct);

    /// <summary>Số phiếu nhập đã xác nhận của từng NVL khách cấp (vw_OrderMaterialDelivery.ReceivedCount).</summary>
    private async Task<Dictionary<int, int>> ReceivedCountsAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<VwOrderMaterialDelivery>().Query().AsNoTracking().Where(v => v.OrderId == orderId)
            .ToDictionaryAsync(v => v.OrderMaterialId, v => v.ReceivedCount, ct);

    private static OrderMaterialDto ToDto(OrderMaterial om, decimal normQty, decimal receivedQty, int receivedCount) => new(
        om.OrderMaterialId, om.OrderId, om.MaterialId, om.Material.MaterialCode, om.Material.MaterialName,
        (om.Material.MaterialType ?? string.Empty), om.Material.Unit, om.Material.Color, om.RequiredQty, om.CustomerQty,
        om.ExpectedDeliveryCount, receivedCount, om.PurchaseQty, om.Source, om.Note, normQty, receivedQty);

    private static string MaterialLabel(Material m) =>
        string.IsNullOrEmpty(m.Color) ? $"{m.MaterialCode} {m.MaterialName}" : $"{m.MaterialCode} {m.MaterialName} ({m.Color})";

    private static Result<OrderMaterialDto> NotFound() =>
        Result<OrderMaterialDto>.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    private static Result<OrderMaterialDto> Fail(string code, string message) =>
        Result<OrderMaterialDto>.Failure(code, message);
}
