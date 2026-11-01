namespace NX_MTD.Application.DTOs;

// ---------- Import Excel chi phí tháng (UC12/UC33, DEV-MI-12-x) ----------

/// <summary>Một dòng đọc thô từ file Excel chi phí tháng (parser không ném lỗi theo dòng, chỉ ghi
/// ParseError khi ô có nội dung nhưng sai kiểu số — để Service dựng preview đủ 3 loại:
/// hợp lệ / lỗi / trùng, giống MSG24).</summary>
public sealed record MonthlyCostExcelRowDto(
    int RowNumber, string? CostCategoryCode, string? CostType, string? ProductCode,
    decimal? Amount, int? Quantity, string? Note, string? ParseError);

/// <summary>Trạng thái một dòng sau khi kiểm tra nghiệp vụ (không lưu DB, chỉ để hiển thị preview).</summary>
public static class MonthlyCostRowStatus
{
    public const string Valid = "Valid";
    public const string Error = "Error";
    public const string Duplicate = "Duplicate";
}

/// <summary>Một dòng trong bảng xem trước, đã gắn tên mã hàng (nếu tìm thấy) và trạng thái.</summary>
public sealed record MonthlyCostExcelRowPreviewDto(
    int RowNumber, string? CostCategoryCode, string? CostType, string? ProductCode, string? ProductName,
    decimal? Amount, int? Quantity, string? Note, string RowStatus, string? ErrorMessage);

/// <summary>Đối soát MEALS: hóa đơn bếp (dòng CostCategoryCode = MEALS) so với Σ DailyMeal.TotalAmount
/// trong khoảng ngày của tháng — chỉ để Kế toán tham khảo (cảnh báo mềm), không chặn import
/// (xem docs/CODE_DEVIATIONS.md, DEV-MI-12-2).</summary>
public sealed record MealsReconciliationDto(decimal ExpectedAmount, decimal? ImportedAmount, bool HasDiscrepancy);

/// <summary>Kết quả xem trước file Excel chi phí tháng trước khi nhập (chưa ghi DB) — MSG24.</summary>
public sealed record MonthlyCostExcelPreviewDto(
    int ValidCount, int ErrorCount, int DuplicateCount, IReadOnlyList<MonthlyCostExcelRowPreviewDto> Rows,
    MealsReconciliationDto MealsReconciliation);

/// <summary>Kết quả sau khi nhập thật: xóa toàn bộ dòng cũ của tháng, ghi lại các dòng hợp lệ — MSG24.</summary>
public sealed record MonthlyCostImportResultDto(
    int PeriodId, string PeriodMonth, int ValidCount, int ErrorCount, int DuplicateCount, int ItemCount,
    decimal TotalAmount, MealsReconciliationDto MealsReconciliation);

// ---------- Chi phí tháng (xem theo tháng) ----------

/// <summary>Một dòng chi phí đã lưu, kèm tên mã hàng cho khoản VARIABLE.</summary>
public sealed record MonthlyCostItemDto(
    int MonthlyCostId, string CostCategoryCode, string CostType, int? ProductId, string? ProductCode,
    string? ProductName, decimal Amount, int? Quantity, string? Note, DateTime ImportedAt);

/// <summary>Toàn bộ chi phí đã nhập của một tháng — UC12/UC33. IsLocked cho FE biết tháng đã khóa để vô
/// hiệu hóa nút nhập lại, tránh phải thử rồi mới biết qua lỗi 409 (MSG21).</summary>
public sealed record MonthlyCostSummaryDto(
    int PeriodId, string PeriodMonth, bool IsLocked, decimal TotalAmount, IReadOnlyList<MonthlyCostItemDto> Items,
    MealsReconciliationDto MealsReconciliation);
