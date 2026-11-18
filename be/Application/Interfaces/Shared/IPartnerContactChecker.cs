using NX_MTD.Application.Common;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// DEV-ALL-125: kiểm SĐT / email của khách hàng và nhà cung cấp không trùng nhau — xét chéo cả hai danh sách, kể cả đơn vị đã
/// ngừng. Bỏ qua chính đơn vị đang sửa.
/// </summary>
public interface IPartnerContactChecker
{
    /// <returns>null khi không trùng; ngược lại là lỗi Validation có errors theo field (phone / email).</returns>
    Task<Result?> CheckAsync(string? phone, string? email, int? exceptCustomerId, int? exceptSupplierId, CancellationToken ct);
}
