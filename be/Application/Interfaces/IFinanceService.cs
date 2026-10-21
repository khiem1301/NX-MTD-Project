using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>MI-13 — Doanh thu (UC27, Kế toán + Admin) và Hòa vốn + giá trị thặng dư (UC11, chỉ Admin,
/// GBR-17/GBR-02). Doanh thu không bao giờ nhập tay, suy ra từ vw_MonthlyRevenue (đọc, không đổi view).
/// Hòa vốn lưu lịch sử mỗi lần tính (BreakEvenResult), không ghi đè.</summary>
public interface IFinanceService
{
    // ---------- Doanh thu (UC27) — dùng chung Kế toán + Admin, KHÔNG có CmtUnitPrice (GBR-02) ----------

    Task<Result<PagedResult<RevenueSummaryDto>>> GetRevenueAsync(RevenueQuery query, CancellationToken ct);

    Task<Result<ExcelFileDto>> ExportRevenueAsync(RevenueQuery query, CancellationToken ct);

    // ---------- Hòa vốn + giá trị thặng dư (UC11) — CHỈ Admin ----------

    /// <summary>Chạy một lần tính mới cho một tháng theo cách phân bổ Admin chọn; chặn nếu bảng lương
    /// tháng chưa duyệt hoặc chi phí tháng chưa nhập (MSG25/GBR-17).</summary>
    Task<Result<BreakEvenCalculationResultDto>> CalculateAsync(BreakEvenCalculateDto dto, CancellationToken ct);

    /// <summary>Xem kết quả đã tính của một tháng; history=false (mặc định) chỉ lấy lần tính gần nhất.</summary>
    Task<Result<BreakEvenListResultDto>> GetBreakEvenAsync(
        string periodMonth, int? productId, bool history, CancellationToken ct);

    Task<Result<ExcelFileDto>> ExportBreakEvenAsync(
        string periodMonth, int? productId, bool history, CancellationToken ct);
}
