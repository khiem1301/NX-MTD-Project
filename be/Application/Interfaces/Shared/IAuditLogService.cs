using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <param name="OldValues">Object ẩn danh chỉ chứa các field thay đổi, vd. new { Status = "PENDING_APPROVAL" }.
/// Không bao giờ đưa mật khẩu, hash, token vào đây. Đơn giá CMT chỉ được ghi ở log của CustomerOrder (D26).</param>
/// <param name="Summary">Câu mô tả tiếng Việt cho người đọc. Để trống thì AuditLogService tự dựng từ hành động, mã bản ghi và các field đổi (AuditText).</param>
public sealed record AuditEntry(string TableName, long RecordId, string Action,
    object? OldValues = null, object? NewValues = null, string? Reason = null, string? Summary = null);

public interface IAuditLogService
{
    /// <summary>Thêm dòng AuditLog vào DbContext hiện tại, KHÔNG SaveChanges (D17).
    /// ChangedBy = người đăng nhập; null khi hệ thống (job) ghi.</summary>
    void Add(AuditEntry entry);

    Task<IReadOnlyList<AuditLogDto>> GetHistoryAsync(string tableName, long recordId, CancellationToken ct);
}
