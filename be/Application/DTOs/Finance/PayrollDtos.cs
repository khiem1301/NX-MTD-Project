namespace NX_MTD.Application.DTOs;

// ---------- Xuất dữ liệu tính lương (UC31, Kế toán) ----------

/// <summary>Một dòng dữ liệu tính lương của một nhân viên trong tháng (nguồn vw_PayrollExportData)
/// kèm 2 cảnh báo: ngày thiếu giờ (chấm công) và ngày có giờ công nhưng chưa nhập sản lượng công đoạn.</summary>
public sealed record PayrollExportRowDto(
    int EmployeeId, string EmployeeCode, string FullName, string RoleName, string PayType, string? TeamName,
    decimal WorkDays, decimal SundayWorkDays, decimal OvertimeHours, decimal SundayOvertimeHours,
    decimal PieceRateAmount, decimal SundayPieceRateBonus, decimal ScrapDeduction,
    string? GradeCode, decimal? GradeAllowance,
    int MissingPunchDays, int DaysWithoutOutput);

/// <summary>Dữ liệu file nhị phân trả về cho controller (Excel xuất hoặc đã nhập xong không cần ở đây).</summary>
public sealed record ExcelFileDto(byte[] Content, string FileName);

// ---------- Import bảng lương đã tính trên Excel (UC31, DEV-MI-09-4) ----------

/// <summary>Một khoản đọc từ một dòng nhân viên (đã đổi sang đồng, nhóm cộng/trừ theo danh mục cột).</summary>
public sealed record PayrollExcelItemDto(string Group, string Name, decimal Amount);

/// <summary>Một dòng nhân viên đọc thô từ file Excel bảng thanh toán lương (mỗi dòng = một nhân viên, nhiều khoản).
/// Parser không ném lỗi theo dòng, chỉ ghi ParseError để Service dựng preview đủ 3 loại: hợp lệ / lỗi / trùng (MSG24).
/// AttendanceCode là mã chấm công (cột SLĐ), không phải EmployeeCode.</summary>
public sealed record PayrollExcelRowDto(
    string SheetName, int RowNumber, string? AttendanceCode, string? FullName, string? GradeCode,
    decimal? WorkDays, decimal? NetPay, IReadOnlyList<PayrollExcelItemDto> Items, string? ParseError);

/// <summary>Trạng thái một dòng sau khi kiểm tra nghiệp vụ (không lưu DB, chỉ để hiển thị preview).</summary>
public static class PayrollRowStatus
{
    public const string Valid = "Valid";
    public const string Error = "Error";
    public const string Duplicate = "Duplicate";
}

/// <summary>Một nhân viên trong bảng xem trước, đã gắn tên trong hệ thống (nếu tìm thấy) và trạng thái.
/// Message là lý do lỗi/trùng, hoặc cảnh báo (không chặn) khi dòng vẫn hợp lệ.</summary>
public sealed record PayrollExcelRowPreviewDto(
    string SheetName, int RowNumber, string? AttendanceCode, string? EmployeeName, decimal? WorkDays,
    decimal? NetPay, int ItemCount, string RowStatus, string? Message);

/// <summary>Kết quả xem trước file Excel bảng lương trước khi nhập (chưa ghi DB) — MSG24.</summary>
public sealed record PayrollExcelPreviewDto(
    int ValidCount, int ErrorCount, int DuplicateCount, IReadOnlyList<PayrollExcelRowPreviewDto> Rows);

/// <summary>Kết quả sau khi nhập thật (ghi Payroll ở trạng thái Nháp + Payslip/PayslipItem) — MSG24.</summary>
public sealed record PayrollImportResultDto(
    int PayrollId, string PeriodMonth, int ValidCount, int ErrorCount, int DuplicateCount,
    int PayslipCount, decimal TotalAmount);

// ---------- Bảng lương tháng (Kế toán lập, Admin duyệt) ----------

public sealed record PayrollSummaryDto(
    int PayrollId, int PeriodId, string PeriodMonth, string Status, bool HasImportedFile,
    DateTime? ImportedAt, decimal TotalAmount, int PayslipCount,
    DateTime? SubmittedAt, DateTime? ApprovedAt, string? RejectReason, string? ReopenReason);

/// <summary>Một khoản trên phiếu lương. ItemGroup quyết định cộng/trừ; Section là nhóm hiển thị theo mẫu phiếu
/// (PayslipLayout); Unit là "Money" | "Number" | "Text"; TextValue chỉ có với kiểu chữ (bậc thợ).</summary>
public sealed record PayslipItemDto(
    string ItemGroup, string ItemName, decimal Amount, short SortOrder,
    string Section, string Unit, string? TextValue);

/// <summary>Phiếu lương một nhân viên trong bảng lương, kèm các khoản chi tiết — UC06, UC32.</summary>
public sealed record PayslipDto(
    int PayslipId, int EmployeeId, string EmployeeCode, string FullName, string? TeamName,
    decimal? WorkDays, decimal TotalAdditions, decimal TotalDeductions, decimal Insurance, decimal NetPay,
    IReadOnlyList<PayslipItemDto> Items);

/// <summary>Chi tiết bảng lương cho Admin duyệt: tổng quỹ lương + từng phiếu lương (UC32).</summary>
public sealed record PayrollDetailDto(
    int PayrollId, int PeriodId, string PeriodMonth, string Status, bool HasImportedFile, decimal TotalAmount,
    DateTime? SubmittedAt, DateTime? ApprovedAt, string? RejectReason, string? ReopenReason,
    IReadOnlyList<PayslipDto> Payslips);

public sealed record RejectPayrollDto(string Reason);

public sealed record ReopenPayrollDto(string Reason);

/// <summary>Bộ lọc danh sách bảng lương theo trạng thái (Admin xem Chờ duyệt, Kế toán xem của mình).</summary>
public sealed record PayrollQuery(string? Status, int Page = 1, int PageSize = 20);

// ---------- Công nhân xem phiếu lương của mình (UC06) ----------

/// <summary>Một dòng trong danh sách các tháng công nhân có phiếu lương đã duyệt.</summary>
public sealed record MyPayslipSummaryDto(string PeriodMonth, decimal NetPay, DateTime ApprovedAt);

/// <summary>Chi tiết phiếu lương của chính công nhân cho một tháng — chỉ xem được khi đã duyệt.</summary>
public sealed record MyPayslipDetailDto(
    string PeriodMonth, decimal? WorkDays, decimal TotalAdditions, decimal TotalDeductions, decimal Insurance,
    decimal NetPay, DateTime ApprovedAt, IReadOnlyList<PayslipItemDto> Items);
