using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>MI-09 — Bảng lương + khóa tháng: UC31 (Kế toán xuất dữ liệu / import / gửi duyệt),
/// UC32 (Admin duyệt / từ chối / mở lại, duyệt khóa tháng), UC06 (công nhân xem phiếu lương).</summary>
public interface IPayrollService
{
    // ---------- Kế toán (UC31) ----------

    Task<Result<ExcelFileDto>> ExportAsync(string periodMonth, CancellationToken ct);

    Task<Result<PayrollExcelPreviewDto>> PreviewImportAsync(Stream stream, CancellationToken ct);

    Task<Result<PayrollImportResultDto>> ImportAsync(Stream stream, string periodMonth, CancellationToken ct);

    Task<Result<PayrollSummaryDto>> GetByPeriodMonthAsync(string periodMonth, CancellationToken ct);

    Task<Result> SubmitAsync(int payrollId, CancellationToken ct);

    /// <summary>Tải lại nguyên file Excel Kế toán đã nhập (Kế toán và Admin dùng chung).</summary>
    Task<Result<ExcelFileDto>> GetImportedFileAsync(int payrollId, CancellationToken ct);

    // ---------- Admin (UC32) ----------

    Task<Result<PagedResult<PayrollSummaryDto>>> GetPayrollsAsync(PayrollQuery query, CancellationToken ct);

    Task<Result<PayrollDetailDto>> GetDetailAsync(int payrollId, CancellationToken ct);

    Task<Result> ApproveAsync(int payrollId, CancellationToken ct);

    Task<Result> RejectAsync(int payrollId, RejectPayrollDto dto, CancellationToken ct);

    Task<Result> ReopenAsync(int payrollId, ReopenPayrollDto dto, CancellationToken ct);

    // ---------- Công nhân (UC06) ----------

    Task<Result<IReadOnlyList<MyPayslipSummaryDto>>> GetMyPayslipsAsync(int employeeId, CancellationToken ct);

    Task<Result<MyPayslipDetailDto>> GetMyPayslipAsync(int employeeId, string periodMonth, CancellationToken ct);
}
