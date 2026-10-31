using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC21: dashboard tiến độ đơn hàng (Admin / Quản đốc) và hai job nền JOB-04, JOB-05.</summary>
public interface IOrderProgressService
{
    Task<Result<IReadOnlyList<OrderProgressDto>>> GetOverviewAsync(bool includeClosed, CancellationToken ct);
    Task<Result<IReadOnlyList<OrderLineProgressDto>>> GetLinesAsync(int orderId, CancellationToken ct);

    /// <summary>JOB-04: đặt / gỡ cờ IsDelayRisk; trả số đơn mới bị gắn cờ.</summary>
    Task<int> RunDelayCheckAsync(DateOnly today, CancellationToken ct);

    /// <summary>JOB-05: xóa thông báo quá NOTIFICATION_RETENTION_DAYS ngày; trả số dòng đã xóa.</summary>
    Task<int> PurgeOldNotificationsAsync(DateOnly today, CancellationToken ct);
}
