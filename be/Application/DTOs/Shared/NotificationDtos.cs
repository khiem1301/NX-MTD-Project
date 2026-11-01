using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record NotificationDto(long NotificationId, string SourceType, string Title, string Message, string? LinkUrl,
    bool IsRead, bool IsUrgent, DateTime CreatedAt);

public sealed record NotificationQuery : PageQuery
{
    public bool? UnreadOnly { get; init; }
}

public sealed record UnreadCountDto(int Count);

public sealed record AnnouncementDto(int AnnouncementId, string Content, string Priority, string TargetScope, string SenderName,
    DateTime SentAt, int RecipientCount, IReadOnlyList<string> Targets);

public sealed record SendAnnouncementDto(string Content, string Priority, string TargetScope, IReadOnlyList<int>? TeamIds,
    IReadOnlyList<int>? RoleIds);

public sealed record AnnouncementTeamOptionDto(int TeamId, string TeamName, string TeamType);

public sealed record AnnouncementRoleOptionDto(int RoleId, string RoleCode, string RoleName);

public sealed record AnnouncementTargetsDto(IReadOnlyList<AnnouncementTeamOptionDto> Teams,
    IReadOnlyList<AnnouncementRoleOptionDto> Roles);

public sealed record AnnouncementQuery : PageQuery;
