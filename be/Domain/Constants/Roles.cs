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
