using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC05: hộp thư thông báo của người đang đăng nhập; mọi vai trò dùng chung, chỉ đọc / đánh dấu của chính mình.</summary>
public sealed class NotificationInboxService : INotificationInboxService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public NotificationInboxService(IUnitOfWork uow, ICurrentUser user, IClock clock)
    {
        _uow = uow; _user = user; _clock = clock;
    }

    private IQueryable<Notification> Mine => _uow.Repository<Notification>().Query().Where(n => n.RecipientId == _user.EmployeeId);

    public async Task<Result<PagedResult<NotificationDto>>> ListMineAsync(NotificationQuery query, CancellationToken ct)
    {
        var urgentIds = _uow.Repository<Announcement>().Query()
            .Where(a => a.Priority == AnnouncementPriorities.Urgent).Select(a => (long)a.AnnouncementId);
        var page = await Mine.AsNoTracking()
            .Where(n => query.UnreadOnly != true || n.ReadAt == null)
            .OrderByDescending(n => n.NotificationId)
            .Select(n => new NotificationDto(n.NotificationId, n.SourceType, n.Title, n.Message, n.LinkUrl, n.ReadAt != null,
                n.RefTable == nameof(Announcement) && urgentIds.Contains(n.RefId!.Value), n.CreatedAt))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<NotificationDto>>.Success(page);
    }

    public async Task<Result<UnreadCountDto>> UnreadCountAsync(CancellationToken ct) =>
        Result<UnreadCountDto>.Success(new UnreadCountDto(await Mine.CountAsync(n => n.ReadAt == null, ct)));

    public async Task<Result> MarkReadAsync(long notificationId, CancellationToken ct)
    {
        var n = await Mine.FirstOrDefaultAsync(x => x.NotificationId == notificationId, ct);
        if (n is null) return Fail.NotFound();
        if (n.ReadAt is null)
        {
            n.ReadAt = _clock.Now;
            await _uow.SaveChangesAsync();
        }
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(CancellationToken ct)
    {
        var unread = await Mine.Where(n => n.ReadAt == null).ToListAsync(ct);
        foreach (var n in unread) n.ReadAt = _clock.Now;
        if (unread.Count > 0) await _uow.SaveChangesAsync();
        return Result.Success();
    }
}

/// <summary>
/// UC20: gửi thông báo nhanh (NORMAL / URGENT) tới toàn xưởng, các tổ hoặc các vai trò được chọn; mỗi người nhận có một dòng
/// <c>Notification</c> trong hộp thư (push FCM chưa làm: PushStatus = SKIPPED).
/// </summary>
public sealed class AnnouncementService : IAnnouncementService
{
    private const int MaxContentLength = 1000;   // Notification.Message tối đa 1000 ký tự

    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public AnnouncementService(IUnitOfWork uow, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _audit = audit; _clock = clock;
    }

    public async Task<Result<PagedResult<AnnouncementDto>>> ListSentAsync(AnnouncementQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var notifications = _uow.Repository<Notification>().Query().Where(n => n.RefTable == nameof(Announcement));
        var page = await _uow.Repository<Announcement>().Query().AsNoTracking()
            .Where(a => string.IsNullOrEmpty(keyword) || a.Content.Contains(keyword))
            .OrderByDescending(a => a.AnnouncementId)
            .Select(a => new
            {
                a.AnnouncementId, a.Content, a.Priority, a.TargetScope, Sender = a.Sender.FullName, a.SentAt,
                Recipients = notifications.Count(n => n.RefId == a.AnnouncementId),
                Teams = a.AnnouncementTargets.Where(t => t.Team != null).Select(t => t.Team!.TeamName).ToList(),
                Roles = a.AnnouncementTargets.Where(t => t.Role != null).Select(t => t.Role!.RoleName).ToList(),
            })
            .ToPagedAsync(query, ct);
        return Result<PagedResult<AnnouncementDto>>.Success(new PagedResult<AnnouncementDto>(
            page.Items.Select(a => new AnnouncementDto(a.AnnouncementId, a.Content, a.Priority, a.TargetScope, a.Sender, a.SentAt,
                a.Recipients, a.Teams.Concat(a.Roles).ToList())).ToList(), page.Page, page.PageSize, page.TotalItems));
    }

    public async Task<Result<AnnouncementTargetsDto>> GetTargetsAsync(CancellationToken ct)
    {
        var teams = await _uow.Repository<Team>().Query().AsNoTracking().Where(t => t.Status == RecordStatus.Active)
            .OrderBy(t => t.TeamName).Select(t => new AnnouncementTeamOptionDto(t.TeamId, t.TeamName, t.TeamType)).ToListAsync(ct);
        var roles = await _uow.Repository<Role>().Query().AsNoTracking().OrderBy(r => r.RoleId)
            .Select(r => new AnnouncementRoleOptionDto(r.RoleId, r.RoleCode, r.RoleName)).ToListAsync(ct);
        return Result<AnnouncementTargetsDto>.Success(new AnnouncementTargetsDto(teams, roles));
    }

    public async Task<Result<AnnouncementDto>> SendAsync(SendAnnouncementDto dto, int senderId, CancellationToken ct)
    {
        var content = dto.Content.Trim();
        if (content.Length > MaxContentLength)
            return Result<AnnouncementDto>.Failure(ErrorCodes.AnnouncementInvalid, Messages.AnnouncementTooLong, ResultKind.Validation);

        var teamIds = (dto.TeamIds ?? Array.Empty<int>()).Distinct().ToList();
        var roleIds = (dto.RoleIds ?? Array.Empty<int>()).Distinct().ToList();
        var recipients = _uow.Repository<Employee>().Query().AsNoTracking().Where(e => e.Status == RecordStatus.Active && e.EmployeeId != senderId);
        switch (dto.TargetScope)
        {
            case AnnouncementScopes.All: break;
            case AnnouncementScopes.Team:
                if (teamIds.Count == 0) return Result<AnnouncementDto>.Failure(ErrorCodes.AnnouncementInvalid, Messages.AnnouncementTargetRequired, ResultKind.Validation);
                recipients = recipients.Where(e => e.TeamId != null && teamIds.Contains(e.TeamId.Value));
                break;
            case AnnouncementScopes.Role:
                if (roleIds.Count == 0) return Result<AnnouncementDto>.Failure(ErrorCodes.AnnouncementInvalid, Messages.AnnouncementTargetRequired, ResultKind.Validation);
                recipients = recipients.Where(e => roleIds.Contains(e.RoleId));
                break;
            default:
                return Result<AnnouncementDto>.Failure(ErrorCodes.AnnouncementInvalid, Messages.AnnouncementScopeInvalid, ResultKind.Validation);
        }

        var ids = await recipients.Select(e => e.EmployeeId).ToListAsync(ct);
        if (ids.Count == 0)
            return Result<AnnouncementDto>.Failure(ErrorCodes.AnnouncementInvalid, Messages.AnnouncementNoRecipient, ResultKind.Validation);

        var urgent = dto.Priority == AnnouncementPriorities.Urgent;
        var created = await _uow.InTransactionAsync(async () =>
        {
            var announcement = new Announcement
            {
                Content = content, Priority = dto.Priority, TargetScope = dto.TargetScope, SenderId = senderId, SentAt = _clock.Now,
            };
            if (dto.TargetScope == AnnouncementScopes.Team)
                foreach (var id in teamIds) announcement.AnnouncementTargets.Add(new AnnouncementTarget { TeamId = id });
            if (dto.TargetScope == AnnouncementScopes.Role)
                foreach (var id in roleIds) announcement.AnnouncementTargets.Add(new AnnouncementTarget { RoleId = id });
            await _uow.Repository<Announcement>().AddAsync(announcement);
            await _uow.SaveChangesAsync();

            var title = urgent ? "Thông báo khẩn" : "Thông báo";
            await _uow.Repository<Notification>().AddRangeAsync(ids.Select(id => new Notification
            {
                RecipientId = id, SourceType = NotificationSources.Announcement, RefTable = nameof(Announcement), RefId = announcement.AnnouncementId,
                Title = title, Message = content, PushStatus = NotificationPushStatuses.Skipped, CreatedAt = _clock.Now,
            }).ToList());
            _audit.Add(new AuditEntry(nameof(Announcement), announcement.AnnouncementId, AuditActions.Insert, null,
                new { announcement.Priority, announcement.TargetScope, Recipients = ids.Count }));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(announcement.AnnouncementId, string.Format(Messages.AnnouncementSent, ids.Count));
        });

        var list = await ListSentAsync(new AnnouncementQuery { Page = 1, PageSize = 1 }, ct);
        return Result<AnnouncementDto>.Success(list.Data!.Items.First(a => a.AnnouncementId == created.Data), created.Message);
    }
}
