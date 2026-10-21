namespace NX_MTD.Application.Interfaces;

public sealed record NotificationMessage(string SourceType, string Title, string Message, string? LinkUrl,
    string? RefTable = null, long? RefId = null);

/// <summary>Chỉ thêm dòng Notification vào DbContext (không SaveChanges). Push FCM: KH-12.</summary>
public interface INotificationService
{
    void NotifyEmployees(IEnumerable<int> employeeIds, NotificationMessage message);

    /// <summary>Gửi cho mọi nhân viên ACTIVE có vai trò này (roleCode theo bảng Role, vd. "ADMIN").</summary>
    Task NotifyRoleAsync(string roleCode, NotificationMessage message, CancellationToken ct);
}
