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

public static class NotificationSources
{
    public const string Announcement = "ANNOUNCEMENT";
    public const string Request = "REQUEST";
    public const string Warehouse = "WAREHOUSE";
    public const string Production = "PRODUCTION";
    public const string System = "SYSTEM";
}
