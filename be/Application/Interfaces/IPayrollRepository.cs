using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC06/UC31/UC32: bảng lương tháng + khóa kỳ kế toán. Không có thủ tục DB riêng nên đọc/ghi
/// thẳng bằng EF, giống MealRepository (module cùng người, cùng kiểu).</summary>
/// <summary>File Excel gốc Kế toán đã nhập: đường dẫn trong kho (không đưa ra API) và lúc nhập.</summary>
public sealed record PayrollImportFile(string FileUrl, DateTime ImportedAt);

public interface IPayrollRepository
{
    // ---------- Kỳ kế toán ----------

    Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct);

    /// <summary>Lấy kỳ kế toán của tháng, tạo mới nếu chưa có (StartDate/EndDate = đầu/cuối tháng).</summary>
    Task<AccountingPeriod> GetOrCreatePeriodAsync(string periodMonth, CancellationToken ct);

    // ---------- Bảng lương ----------

    Task<Payroll?> FindPayrollByPeriodIdAsync(int periodId, bool includePayslips, CancellationToken ct);

    Task<Payroll?> FindPayrollByIdAsync(int payrollId, bool includePayslips, CancellationToken ct);

    Task<(IReadOnlyList<Payroll> Items, int Total)> GetPayrollsAsync(
        string? status, int page, int pageSize, CancellationToken ct);

    void AddPayroll(Payroll payroll);

    /// <summary>Xóa toàn bộ Payslip + PayslipItem hiện có của bảng lương (dùng khi nhập lại Excel).</summary>
    Task RemovePayslipsAsync(int payrollId, CancellationToken ct);

    void AddPayslip(Payslip payslip);

    // ---------- File import (Attachment: OwnerType = PAYROLL, Purpose = IMPORT_FILE) và lý do mở lại (AuditLog UNLOCK) ----------

    /// <summary>File import hiện hành của từng bảng lương (khóa = PayrollId); bảng lương chưa có file không có khóa.</summary>
    Task<IReadOnlyDictionary<int, PayrollImportFile>> GetImportFilesAsync(IReadOnlyCollection<int> payrollIds, CancellationToken ct);

    /// <summary>Gỡ các dòng Attachment file import cũ của bảng lương; trả đường dẫn các file để xóa khỏi kho.</summary>
    Task<IReadOnlyList<string>> RemoveImportFilesAsync(int payrollId, CancellationToken ct);

    void AddImportFile(int payrollId, string fileUrl, DateTime uploadedAt);

    /// <summary>Lý do của lần mở lại gần nhất (AuditLog Action = UNLOCK); bảng lương chưa từng mở lại không có khóa.</summary>
    Task<IReadOnlyDictionary<int, string>> GetReopenReasonsAsync(IReadOnlyCollection<int> payrollIds, CancellationToken ct);

    // ---------- Import Excel: tra cứu nhân viên theo mã chấm công (UC31, DEV-MI-09-4) ----------

    /// <summary>Tra nhân viên theo <c>Employee.AttendanceCode</c> (cột SLĐ trong file lương). Khóa kết quả là
    /// mã chấm công dạng chuỗi số nguyên; GradeCode là bậc thợ hiện tại trong hệ thống.</summary>
    Task<IReadOnlyDictionary<string, (int EmployeeId, string FullName, string Status, string? GradeCode)>>
        GetEmployeesByAttendanceCodesAsync(IReadOnlyCollection<string> attendanceCodes, CancellationToken ct);

    // ---------- Xuất dữ liệu tính lương (UC31) ----------

    Task<IReadOnlyList<VwPayrollExportDatum>> GetExportRowsAsync(string periodMonth, CancellationToken ct);

    /// <summary>Số ngày thiếu giờ (DailyTimesheet.IsMissingPunch) của mỗi nhân viên trong khoảng ngày.</summary>
    Task<IReadOnlyDictionary<int, int>> GetMissingPunchDaysAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);

    /// <summary>Số ngày có giờ công nhưng chưa có sản lượng công đoạn nào (chỉ xét CN khoán sản phẩm) —
    /// DEV-MI-09-1: xấp xỉ theo ngày, không theo từng ca vì DailyTimesheet không lưu ca.</summary>
    Task<IReadOnlyDictionary<int, int>> GetDaysWithoutOutputAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);

    // ---------- Công nhân xem phiếu lương của mình (UC06) ----------

    Task<IReadOnlyList<(string PeriodMonth, decimal NetPay, DateTime ApprovedAt)>> GetMyPayslipSummariesAsync(
        int employeeId, CancellationToken ct);

    Task<(Payslip Payslip, DateTime ApprovedAt)?> FindMyPayslipAsync(
        int employeeId, string periodMonth, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
