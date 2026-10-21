namespace NX_MTD.Domain.Constants;

/// <summary>Khoản chi phí tháng (MonthlyCost.CostCategoryCode) — UC12/UC33, 10 khoản cố định theo
/// schema CHECK (mục 4.7 NX-MTD_AI_Context.md).</summary>
public static class MonthlyCostCategory
{
    public const string Meals = "MEALS";
    public const string Fuel = "FUEL";
    public const string Utilities = "UTILITIES";
    public const string Premises = "PREMISES";
    public const string MachineRepair = "MACHINE_REPAIR";
    public const string MachineSupplies = "MACHINE_SUPPLIES";
    public const string Stationery = "STATIONERY";
    public const string MaterialsBought = "MATERIALS_BOUGHT";
    public const string Scrap = "SCRAP";
    public const string Other = "OTHER";

    public static readonly string[] All =
        [Meals, Fuel, Utilities, Premises, MachineRepair, MachineSupplies, Stationery, MaterialsBought, Scrap, Other];
}

/// <summary>Kiểu chi phí (MonthlyCost.CostType) — FIXED không gắn mã hàng (ProductId NULL),
/// VARIABLE bắt buộc mã hàng (CHECK CostType = 'FIXED' OR ProductId IS NOT NULL).</summary>
public static class MonthlyCostType
{
    public const string Fixed = "FIXED";
    public const string Variable = "VARIABLE";

    public static readonly string[] All = [Fixed, Variable];
}
