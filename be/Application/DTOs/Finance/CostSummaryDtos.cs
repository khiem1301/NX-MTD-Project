namespace NX_MTD.Application.DTOs;

// ---------- MI-19 — UC12 "Tổng hợp chi phí" (CHỈ Admin, GBR-02) ----------

/// <summary>Một khoản chi phí cố định (FIXED) đã gộp theo loại — không gắn mã hàng.</summary>
public sealed record CostFixedItemDto(string CostCategoryCode, decimal Amount);

/// <summary>Một khoản chi phí biến đổi (VARIABLE) theo loại + mã hàng.</summary>
public sealed record CostVariableItemDto(
    string CostCategoryCode, int ProductId, string ProductCode, string ProductName, decimal Amount);

/// <summary>Tổng biến phí của một mã hàng, gộp mọi khoản VARIABLE — "theo mã hàng" (per style) trong SRS UC12.</summary>
public sealed record CostByProductDto(int ProductId, string ProductCode, string ProductName, decimal Amount);

/// <summary>UC12 "Tổng hợp chi phí": vật tư (MonthlyCost, MI-12) + lương (Payroll đã duyệt, MI-09) — tổng
/// và theo mã hàng. CHỈ Admin xem được (GBR-02 — "3 tầng dữ liệu... Mật: tổng hợp chi phí chỉ Admin").
/// Không copy lại logic gộp của MonthlyCostService — gọi thẳng GetByPeriodMonthAsync rồi cộng thêm
/// Payroll.TotalAmount (xem CostSummaryService).</summary>
public sealed record CostSummaryDto(
    int PeriodId, string PeriodMonth, bool IsLocked,
    decimal FixedCostTotal, IReadOnlyList<CostFixedItemDto> FixedCosts,
    decimal VariableCostTotal, IReadOnlyList<CostVariableItemDto> VariableCosts,
    IReadOnlyList<CostByProductDto> VariableCostsByProduct,
    decimal MonthlyCostTotal,
    bool PayrollApproved, decimal? PayrollTotal,
    decimal GrandTotal,
    MealsReconciliationDto MealsReconciliation);
