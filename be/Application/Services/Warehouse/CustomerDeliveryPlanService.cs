using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-95: lịch khách giao NVL khai một lần cho cả đơn hàng (không theo từng NVL). Một phiếu khách cấp thường đã xác nhận của đơn
/// là một lần giao, dù phiếu có những NVL nào; đủ số lần mà còn thiếu thì hệ thống đưa mọi NVL còn thiếu vào đơn thiếu NVL.
/// </summary>
public sealed class CustomerDeliveryPlanService : ICustomerDeliveryPlanService
{
    private const string IncomingLink = "/warehouse/incoming-materials";

    private readonly IUnitOfWork _uow;
    private readonly DeliveryCountGuard _deliveries;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public CustomerDeliveryPlanService(IUnitOfWork uow, DeliveryCountGuard deliveries, INotificationService notifier,
        IAuditLogService audit, IClock clock)
    {
        _uow = uow; _deliveries = deliveries; _notifier = notifier; _audit = audit; _clock = clock;
    }

    public async Task<Result<OrderCustomerDeliveryDto>> GetAsync(int orderId, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        return order is null ? Fail.NotFound<OrderCustomerDeliveryDto>() : Result<OrderCustomerDeliveryDto>.Success(await ToDtoAsync(order, ct));
    }

    /// <summary>
    /// Ngày đổi thì kiểm như ngày dự kiến giao (không trước hôm nay, không sau hạn giao của đơn); số lần không nhỏ hơn số lần khách
    /// đã giao. Báo Thủ kho khi đơn có NVL khách cấp.
    /// </summary>
    public async Task<Result<OrderCustomerDeliveryDto>> SaveAsync(int orderId, SaveOrderCustomerDeliveryDto dto, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return Fail.NotFound<OrderCustomerDeliveryDto>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Result<OrderCustomerDeliveryDto>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderMaterialOrderClosed);

        var dateTo = DocumentDateRules.IsRangeDelivery(dto.DeliveryCount) ? dto.DeliveryDateTo : null;
        var invalid = await CheckAsync(order, dto, dateTo, ct);
        if (invalid is not null) return Result<OrderCustomerDeliveryDto>.Failure(ErrorCodes.WarehouseInvalidReference, invalid, ResultKind.Validation);

        var before = new { order.CustomerDeliveryCount, order.CustomerDeliveryDate, order.CustomerDeliveryDateTo };
        order.CustomerDeliveryCount = dto.DeliveryCount;
        order.CustomerDeliveryDate = dto.DeliveryDate;
        order.CustomerDeliveryDateTo = dateTo;
        _audit.Add(new AuditEntry(nameof(CustomerOrder), order.OrderId, AuditActions.Update, before,
            new { order.CustomerDeliveryCount, order.CustomerDeliveryDate, order.CustomerDeliveryDateTo }));
        var result = await ToDtoAsync(order, ct);
        if (result.HasCustomerMaterials) await NotifyAsync(order, ct);
        await _uow.SaveChangesAsync();
        return Result<OrderCustomerDeliveryDto>.Success(result, WarehouseMessages.CustomerDeliverySaved);
    }

    private async Task<string?> CheckAsync(CustomerOrder order, SaveOrderCustomerDeliveryDto dto, DateOnly? dateTo, CancellationToken ct)
    {
        var received = await _deliveries.CustomerDeliveriesAsync(order.OrderId, null, ct);
        if (dto.DeliveryCount < received) return string.Format(WarehouseMessages.CustomerDeliveryBelowReceived, received);
        // Tăng số lần = khách giao thêm trong tương lai: ngày giao cuối không được giữ ngày cũ đã qua.
        if (order.CustomerDeliveryCount is { } old && dto.DeliveryCount > old && (dateTo ?? dto.DeliveryDate) < _clock.Today)
            return WarehouseMessages.CustomerDeliveryIncreaseNeedsFutureDate;
        var changed = new[]
        {
            dto.DeliveryDate == order.CustomerDeliveryDate ? (DateOnly?)null : dto.DeliveryDate,
            dateTo == order.CustomerDeliveryDateTo ? null : dateTo,
        }.OfType<DateOnly>();
        return changed.Select(d => DocumentDateRules.CheckExpectedDelivery(d, _clock.Today, null, order.DueDate))
            .FirstOrDefault(e => e is not null);
    }

    private async Task<OrderCustomerDeliveryDto> ToDtoAsync(CustomerOrder order, CancellationToken ct)
    {
        var received = await _deliveries.CustomerDeliveriesAsync(order.OrderId, null, ct);
        var hasCustomer = await _uow.Repository<OrderMaterial>().Query().AnyAsync(m => m.OrderId == order.OrderId && m.CustomerQty > 0, ct);
        return new OrderCustomerDeliveryDto(order.OrderId, order.CustomerDeliveryCount, order.CustomerDeliveryDate,
            order.CustomerDeliveryDateTo, received, hasCustomer);
    }

    private Task NotifyAsync(CustomerOrder order, CancellationToken ct)
    {
        var window = order.CustomerDeliveryDateTo is { } end
            ? string.Format(WarehouseMessages.DeliveryWindowRange, order.CustomerDeliveryDate, end)
            : string.Format(WarehouseMessages.DeliveryWindowSingle, order.CustomerDeliveryDate);
        return _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            Messages.NotifyIncomingCustomerTitle,
            string.Format(Messages.NotifyCustomerDeliveryBody, order.OrderCode, order.CustomerDeliveryCount, window),
            IncomingLink, nameof(CustomerOrder), order.OrderId), ct);
    }
}
