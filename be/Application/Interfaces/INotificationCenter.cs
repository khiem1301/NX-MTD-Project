using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC05: hộp thư thông báo của người đang đăng nhập (chỉ thấy thông báo của mình, R1).</summary>
public interface INotificationInboxService
{
    Task<Result<PagedResult<NotificationDto>>> ListMineAsync(NotificationQuery query, CancellationToken ct);
    Task<Result<UnreadCountDto>> UnreadCountAsync(CancellationToken ct);
    Task<Result> MarkReadAsync(long notificationId, CancellationToken ct);
    Task<Result> MarkAllReadAsync(CancellationToken ct);
}

/// <summary>UC20: Admin / Quản đốc gửi thông báo nhanh tới toàn xưởng, một số tổ hoặc một số vai trò (không push FCM).</summary>
public interface IAnnouncementService
{
    Task<Result<PagedResult<AnnouncementDto>>> ListSentAsync(AnnouncementQuery query, CancellationToken ct);
    Task<Result<AnnouncementTargetsDto>> GetTargetsAsync(CancellationToken ct);
    Task<Result<AnnouncementDto>> SendAsync(SendAnnouncementDto dto, int senderId, CancellationToken ct);
}
