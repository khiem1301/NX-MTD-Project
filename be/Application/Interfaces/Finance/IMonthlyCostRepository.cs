using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC12/UC33: chi phí tháng (MonthlyCost) + kỳ kế toán (AccountingPeriod). Không có thủ tục DB
/// riêng nên đọc/ghi thẳng bằng EF, cùng kiểu dedicated repository như IPayrollRepository (module cùng
/// người sở hữu — Minh — nhưng không dùng chung class, mỗi module một repo riêng).</summary>
public interface IMonthlyCostRepository
{
    // ---------- Kỳ kế toán ----------

    Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct);

    /// <summary>Lấy kỳ kế toán của tháng, tạo mới nếu chưa có (StartDate/EndDate = đầu/cuối tháng),
    /// race-safe giống PayrollRepository.GetOrCreatePeriodAsync.</summary>
    Task<AccountingPeriod> GetOrCreatePeriodAsync(string periodMonth, CancellationToken ct);

    // ---------- Chi phí tháng ----------

    /// <summary>Toàn bộ dòng chi phí đã lưu của một kỳ, kèm Product (cho khoản VARIABLE).</summary>
    Task<IReadOnlyList<MonthlyCost>> GetItemsByPeriodIdAsync(int periodId, CancellationToken ct);

    /// <summary>Xóa toàn bộ dòng chi phí hiện có của kỳ (dùng khi nhập lại Excel — "import lại thay số
    /// tháng đó", xem mục 4.7 AI Context). Chưa SaveChanges.</summary>
    Task RemoveItemsByPeriodIdAsync(int periodId, CancellationToken ct);

    void AddItems(IEnumerable<MonthlyCost> items);

    Task SaveChangesAsync(CancellationToken ct);

    // ---------- Import Excel: tra cứu mã hàng cho khoản VARIABLE (UC12) ----------

    Task<IReadOnlyDictionary<string, (int ProductId, string ProductName)>> GetProductsByCodesAsync(
        IReadOnlyCollection<string> productCodes, CancellationToken ct);

    // ---------- Đối soát suất ăn (UC30/UC12): Σ DailyMeal.TotalAmount trong khoảng ngày ----------

    Task<decimal> GetMealsTotalAmountAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);
}
