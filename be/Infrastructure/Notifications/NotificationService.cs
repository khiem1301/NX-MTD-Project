using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Notifications;

public sealed class NotificationService : INotificationService
{
    private const string PushSkipped = "SKIPPED"; // TODO(Khiêm, KH-12): PENDING + gửi FCM
    private readonly NxMtdDbContext _db;
    private readonly IClock _clock;

    public NotificationService(NxMtdDbContext db, IClock clock) { _db = db; _clock = clock; }

    public void NotifyEmployees(IEnumerable<int> employeeIds, NotificationMessage message)
    {
        foreach (var id in employeeIds.Distinct())
        {
            _db.Notifications.Add(new Notification
            {
                RecipientId = id, SourceType = message.SourceType, Title = message.Title, Message = message.Message,
                LinkUrl = message.LinkUrl, RefTable = message.RefTable, RefId = message.RefId,
                PushStatus = PushSkipped, CreatedAt = _clock.Now,
            });
        }
    }

    public async Task NotifyRoleAsync(string roleCode, NotificationMessage message, CancellationToken ct)
    {
        var ids = await _db.Employees.AsNoTracking()
            .Where(e => e.Status == RecordStatus.Active && e.Role.RoleCode == roleCode)
            .Select(e => e.EmployeeId).ToListAsync(ct);
        NotifyEmployees(ids, message);
    }
}
