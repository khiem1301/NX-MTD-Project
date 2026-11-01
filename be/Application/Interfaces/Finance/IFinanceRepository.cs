using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC27 (doanh thu) + UC11 (hòa vốn, giá trị thặng dư — chỉ Admin). Đọc nhiều bảng của nhiều
/// người (vw_MonthlyRevenue/OrderLine/Product/CustomerOrder — chỉ đọc, không đổi), cộng với bảng của
/// chính Minh (Payroll, MonthlyCost, AccountingPeriod, BreakEvenResult) và đọc OutputRecord/DailyTimesheet
/// (Kiều/Minh) cho công thức LABOUR_HOURS — dùng dedicated repository thay vì IUnitOfWork vì lượng truy
/// vấn cross-table khá nhiều, cùng kiểu MonthlyCostRepository/PayrollRepository.</summary>
public interface IFinanceRepository
{
    // ---------- Doanh thu (UC27) ----------

    /// <summary>Dòng doanh thu thô từ vw_MonthlyRevenue, lọc theo tháng/đơn/mã hàng (còn nguyên theo
    /// (PeriodMonth, OrderId, ProductId) của view — Service tự gộp lại theo (PeriodMonth, ProductId) khi
    /// cần tính theo mã hàng xuyên đơn, ví dụ cho hòa vốn).</summary>
    Task<IReadOnlyList<VwMonthlyRevenue>> GetRevenueRowsAsync(
        string? periodMonth, int? orderId, int? productId, CancellationToken ct);

    Task<IReadOnlyDictionary<int, string>> GetOrderCodesAsync(IReadOnlyCollection<int> orderIds, CancellationToken ct);

    Task<IReadOnlyDictionary<int, (string ProductCode, string ProductName)>> GetProductsAsync(
        IReadOnlyCollection<int> productIds, CancellationToken ct);

    // ---------- Cổng điều kiện tính hòa vốn (GBR-17, MSG25) ----------

    /// <summary>Không tự tạo kỳ kế toán nếu chưa có — khác MonthlyCostRepository/PayrollRepository
    /// (hòa vốn không phải hành động "import" nên không có lý do tạo kỳ mới).</summary>
    Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct);

    Task<bool> IsPayrollApprovedAsync(int periodId, CancellationToken ct);

    Task<bool> HasMonthlyCostAsync(int periodId, CancellationToken ct);

    // ---------- Chi phí tháng của kỳ (bước 4/5 công thức) ----------

    Task<IReadOnlyList<MonthlyCost>> GetMonthlyCostsAsync(int periodId, CancellationToken ct);

    // ---------- Phân bổ LABOUR_HOURS (DEV-MI-13-1): sản lượng công đoạn + bảng công trong kỳ ----------

    /// <summary>Sản lượng công đoạn trong khoảng ngày của kỳ, đã gộp theo (WorkerId, WorkDate, ProductId)
    /// bằng OutputRecord.OrderLineId → OrderLine.ProductId (OutputRecord không có cột ProductId trực
    /// tiếp).</summary>
    Task<IReadOnlyList<(int WorkerId, DateOnly WorkDate, int ProductId, int Quantity)>> GetOutputForLabourAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct);

    /// <summary>DailyTimesheet.WorkedHours theo (EmployeeId, WorkDate) cho đúng tập công nhân đã có
    /// OutputRecord trong kỳ — không cần đọc toàn bộ bảng công của kỳ.</summary>
    Task<IReadOnlyDictionary<(int WorkerId, DateOnly WorkDate), decimal>> GetWorkedHoursAsync(
        IReadOnlyCollection<int> workerIds, DateOnly fromDate, DateOnly toDate, CancellationToken ct);

    // ---------- Lưu / đọc kết quả hòa vốn (lịch sử, không bao giờ ghi đè) ----------

    void AddBreakEvenResults(IEnumerable<BreakEvenResult> results);

    /// <summary>Kết quả đã lưu của một kỳ, kèm Product; lọc thêm theo mã hàng nếu có.</summary>
    Task<IReadOnlyList<BreakEvenResult>> GetResultsAsync(int periodId, int? productId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
