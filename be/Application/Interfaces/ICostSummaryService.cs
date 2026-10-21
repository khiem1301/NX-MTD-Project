using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>MI-19 — UC12 "Tổng hợp chi phí". CHỈ Admin (GBR-02, cùng tầng dữ liệu "Mật" với hòa vốn —
/// xem <see cref="IFinanceService"/>). Lớp trên <see cref="IMonthlyCostService"/> (MI-12): không viết lại
/// logic gộp chi phí theo khoản/mã hàng, chỉ cộng thêm Payroll.TotalAmount của bảng lương đã duyệt.</summary>
public interface ICostSummaryService
{
    /// <summary>Tổng hợp chi phí tháng (vật tư + lương), tổng và theo mã hàng.</summary>
    Task<Result<CostSummaryDto>> GetAsync(string periodMonth, CancellationToken ct);

    /// <summary>Xuất tổng hợp chi phí ra Excel (MSG27).</summary>
    Task<Result<ExcelFileDto>> ExportAsync(string periodMonth, CancellationToken ct);
}
