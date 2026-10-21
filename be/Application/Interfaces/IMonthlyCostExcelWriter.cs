namespace NX_MTD.Application.Interfaces;

/// <summary>Dựng file Excel mẫu để Kế toán tải về, điền chi phí tháng rồi nhập lại (UC12, MSG27).
/// Đặt ở Application để Service không phụ thuộc thư viện Excel cụ thể (giống IPayrollExcelWriter).</summary>
public interface IMonthlyCostExcelWriter
{
    byte[] WriteTemplate();
}
