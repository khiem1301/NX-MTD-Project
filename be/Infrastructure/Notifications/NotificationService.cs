using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Notifications;

/// <summary>
/// DEV-ALL-67: thông báo gửi tới Quản đốc thì Admin nhận bản sao; việc do Quản đốc làm thì các Quản đốc khác nhận bản sao.
/// Người thao tác không nhận thông báo về việc của chính mình. Một thông báo chỉ gửi một lần cho mỗi người trong một request.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private const string PushSkipped = "SKIPPED"; // TODO(Khiêm, KH-12): PENDING + gửi FCM
    private readonly NxMtdDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;
    private readonly HashSet<(int RecipientId, NotificationMessage Message)> _sent = new();
    private Dictionary<string, int[]>? _managementIds;

    public NotificationService(NxMtdDbContext db, IClock clock, ICurrentUser user) { _db = db; _clock = clock; _user = user; }

    public void NotifyEmployees(IEnumerable<int> employeeIds, NotificationMessage message)
    {
        foreach (var id in WithManagementCopies(employeeIds.ToHashSet()))
        {
            if (!_sent.Add((id, message))) continue;
            _db.Notifications.Add(new Notification
            {
                RecipientId = id, SourceType = message.SourceType, Title = message.Title, Message = message.Message,
                LinkUrl = message.LinkUrl, RefTable = message.RefTable, RefId = message.RefId,
                PushStatus = PushSkipped, CreatedAt = _clock.Now,
            });
        }
    }

    public async Task NotifyTeamLeadersAsync(int teamId, NotificationMessage message, CancellationToken ct)
    {
        var ids = await _db.Employees.AsNoTracking()
            .Where(e => e.Status == RecordStatus.Active && e.TeamId == teamId
                        && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
            .Select(e => e.EmployeeId).ToListAsync(ct);
        NotifyEmployees(ids, message);
    }

    public async Task NotifyRoleAsync(string roleCode, NotificationMessage message, CancellationToken ct)
    {
        var ids = await _db.Employees.AsNoTracking()
            .Where(e => e.Status == RecordStatus.Active && e.Role.RoleCode == roleCode)
            .Select(e => e.EmployeeId).ToListAsync(ct);
        NotifyEmployees(ids, message);
    }

    private HashSet<int> WithManagementCopies(HashSet<int> recipients)
    {
        var managers = ManagementIds(RoleCodes.Manager);
        // Có Quản đốc trong người nhận (tính trước khi thêm bản sao cho Quản đốc khác) thì Admin cũng nhận.
        if (recipients.Overlaps(managers)) recipients.UnionWith(ManagementIds(RoleCodes.Admin));
        if (!_user.IsAuthenticated) return recipients;   // job nền, agent chấm công: không có người thao tác
        if (_user.Role == Roles.Manager) recipients.UnionWith(managers);
        recipients.Remove(_user.EmployeeId);
        return recipients;
    }

    /// <summary>Id Quản đốc, Admin đang làm; đọc một lần cho cả request.</summary>
    private int[] ManagementIds(string roleCode)
    {
        _managementIds ??= _db.Employees.AsNoTracking()
            .Where(e => e.Status == RecordStatus.Active
                        && (e.Role.RoleCode == RoleCodes.Manager || e.Role.RoleCode == RoleCodes.Admin))
            .Select(e => new { e.EmployeeId, e.Role.RoleCode })
            .AsEnumerable()
            .GroupBy(e => e.RoleCode)
            .ToDictionary(g => g.Key, g => g.Select(e => e.EmployeeId).ToArray());
        return _managementIds.GetValueOrDefault(roleCode, []);
    }
}
