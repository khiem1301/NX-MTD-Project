namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Kho lưu nguyên file Excel bảng lương Kế toán đã nhập, để Admin (giám đốc) tải về xem (DEV-MI-09-5).
/// Chỉ trả đường dẫn nội bộ để lưu vào <c>Attachment (PAYROLL, IMPORT_FILE)</c>; không bao giờ đưa đường dẫn đó ra API.
/// </summary>
public interface IPayrollFileStore
{
    /// <summary>Lưu file, trả đường dẫn tương đối trong kho.</summary>
    Task<string> SaveAsync(string periodMonth, byte[] content, CancellationToken ct);

    /// <summary>Đọc file đã lưu; null nếu đường dẫn không tồn tại hoặc nằm ngoài kho.</summary>
    Task<byte[]?> ReadAsync(string storedPath, CancellationToken ct);

    /// <summary>Xóa file (không lỗi nếu không còn).</summary>
    Task DeleteAsync(string storedPath, CancellationToken ct);
}
