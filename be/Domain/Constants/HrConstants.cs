namespace NX_MTD.Domain.Constants;

/// <summary>Mã vai trò trong bảng Role (khác chuỗi role trong token ở Roles.cs).</summary>
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string Manager = "MANAGER";
    public const string Accountant = "ACCOUNTANT";
    public const string WarehouseKeeper = "WAREHOUSE_KEEPER";
    public const string TeamLeader = "TEAM_LEADER";
    public const string TeamDeputy = "TEAM_DEPUTY";
    public const string QaQc = "QAQC";
    public const string Electrician = "ELECTRICIAN";
    public const string Worker = "WORKER";

    /// <summary>Quản đốc không được xem / sửa nhân viên có các vai trò này (UC16, GBR-01).</summary>
    public static readonly IReadOnlySet<string> HiddenFromManager = new HashSet<string> { Admin, Manager, Accountant };
}

public static class TeamPositions
{
    public const string Leader = "Leader";
    public const string Deputy = "Deputy";
}

public static class PayTypes
{
    public const string PieceRate = "PIECE_RATE";
    public const string Daily = "DAILY";
    public const string Monthly = "MONTHLY";
}

public static class AuditActions
{
    public const string Insert = "INSERT";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Confirm = "CONFIRM";
    public const string Lock = "LOCK";
    public const string Unlock = "UNLOCK";
    public const string Login = "LOGIN";
}

/// <summary>GradeLevel / GradeEvaluation: bậc thợ A–D.</summary>
public static class GradeCodes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string> { "A", "B", "C", "D" };
}

/// <summary>GradeEvaluation.Status (CK_GradeEval_Status). Dòng APPLIED là lịch sử bậc.</summary>
public static class GradeEvaluationStatuses
{
    public const string Submitted = "SUBMITTED";
    public const string Applied = "APPLIED";
    public const string NotApplied = "NOT_APPLIED";
}

/// <summary>Announcement.Priority (CK_Announcement_Priority).</summary>
public static class AnnouncementPriorities
{
    public const string Normal = "NORMAL";
    public const string Urgent = "URGENT";
}

/// <summary>Announcement.TargetScope (CK_Announcement_Scope): toàn xưởng, theo tổ, theo vai trò.</summary>
public static class AnnouncementScopes
{
    public const string All = "ALL";
    public const string Team = "TEAM";
    public const string Role = "ROLE";
}

/// <summary>Notification.PushStatus (CK_Notification_Push). Push FCM chưa làm nên luôn SKIPPED (KH-12).</summary>
public static class NotificationPushStatuses
{
    public const string Pending = "PENDING";
    public const string Skipped = "SKIPPED";
}

public static class NotificationSources
{
    public const string Announcement = "ANNOUNCEMENT";
    public const string Request = "REQUEST";
    public const string Warehouse = "WAREHOUSE";
    public const string Production = "PRODUCTION";
    public const string System = "SYSTEM";
}
