namespace NX_MTD.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Accountant = "Accountant";
    public const string WarehouseKeeper = "WarehouseKeeper";
    public const string TeamLeader = "TeamLeader";
    public const string TeamDeputy = "TeamDeputy";
    public const string QaQc = "QaQc";
    public const string Electrician = "Electrician";
    public const string Worker = "Worker";

    /// <summary>Tổ phó có cùng quyền tổ trưởng trong tổ của mình.</summary>
    public const string TeamLeaderOrDeputy = TeamLeader + "," + TeamDeputy;

    /// <summary>Người xử lý đơn từ (D12).</summary>
    public const string Handlers = Admin + "," + Manager + "," + Accountant + "," + WarehouseKeeper;
    public const string AdminOrManager = Admin + "," + Manager;
    /// <summary>Mọi vai trò trừ Công nhân: dùng cho API tra cứu dùng chung.</summary>
    public const string StaffExceptWorker = Admin + "," + Manager + "," + Accountant + "," + WarehouseKeeper + ","
        + TeamLeader + "," + TeamDeputy + "," + QaQc + "," + Electrician;

    public static bool IsTeamRole(string role) => role is TeamLeader or TeamDeputy;

    public static string ToRoleCode(string role) => role switch
    {
        Admin => RoleCodes.Admin,
        Manager => RoleCodes.Manager,
        Accountant => RoleCodes.Accountant,
        WarehouseKeeper => RoleCodes.WarehouseKeeper,
        TeamLeader => RoleCodes.TeamLeader,
        TeamDeputy => RoleCodes.TeamDeputy,
        QaQc => RoleCodes.QaQc,
        Electrician => RoleCodes.Electrician,
        Worker => RoleCodes.Worker,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role")
    };

    public static bool IsKnown(string role) =>
        role is Admin or Manager or Accountant or WarehouseKeeper or TeamLeader or TeamDeputy or QaQc or Electrician or Worker;

    public static string FromRoleCode(string roleCode) => roleCode switch
    {
        "ADMIN" => Admin,
        "MANAGER" => Manager,
        "ACCOUNTANT" => Accountant,
        "WAREHOUSE_KEEPER" => WarehouseKeeper,
        "TEAM_LEADER" => TeamLeader,
        "TEAM_DEPUTY" => TeamDeputy,
        "QAQC" => QaQc,
        "ELECTRICIAN" => Electrician,
        "WORKER" => Worker,
        _ => throw new ArgumentOutOfRangeException(nameof(roleCode), roleCode, "Unknown role code")
    };
}
