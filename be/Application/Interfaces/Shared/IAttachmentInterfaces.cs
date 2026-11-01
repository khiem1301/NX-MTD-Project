using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <param name="Key">Khóa lưu trữ, vd. "PRODUCT/2026/11/3f2a….jpg".</param>
public sealed record StoredFile(string Key, DateTime LastWriteUtc);

public interface IFileStorage
{
    /// <summary>Lưu file, trả khóa lưu trữ (vd. "REQUEST/2026/10/3f2a….jpg") để ghi vào Attachment.FileUrl.</summary>
    Task<string> SaveAsync(byte[] content, string folder, string extension, CancellationToken ct);

    /// <summary>null nếu không có file hoặc khóa không hợp lệ.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    /// <summary>Mọi file trong một thư mục gốc của kho (kể cả thư mục con), kèm giờ ghi cuối; rỗng nếu chưa có thư mục.</summary>
    Task<IReadOnlyList<StoredFile>> ListAsync(string folder, CancellationToken ct);

    /// <summary>Xóa file; false nếu không có file hoặc khóa nằm ngoài kho.</summary>
    Task<bool> DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Mỗi luồng đăng ký 1 class cho OwnerType của mình (D20). Dùng ICurrentUser bên trong để kiểm quyền.</summary>
public interface IAttachmentOwnerAccess
{
    string OwnerType { get; }
    Task<bool> CanReadAsync(int ownerId, CancellationToken ct);
    Task<bool> CanWriteAsync(int ownerId, CancellationToken ct);
    Task<int> MaxFilesAsync(CancellationToken ct);
}

public interface IAttachmentRepository
{
    Task AddAsync(Attachment attachment, CancellationToken ct);
    Task<Attachment?> GetAsync(long attachmentId, CancellationToken ct);   // tracked
    Task<int> CountCurrentAsync(string ownerType, int ownerId, CancellationToken ct);
    Task<IReadOnlyList<AttachmentDto>> ListCurrentAsync(string ownerType, int ownerId, CancellationToken ct);
}

/// <summary>Dọn tệp chứng từ: ảnh đã gỡ quá 15 ngày, và tệp không còn dòng Attachment nào trỏ tới quá 15 ngày.</summary>
public interface IAttachmentCleanupService
{
    /// <param name="utcNow">Giờ UTC hiện tại, so với giờ ghi cuối của file (truyền vào để kiểm thử được).</param>
    /// <returns>Số file đã xóa.</returns>
    Task<int> RunAsync(DateTime utcNow, CancellationToken ct);
}

public interface IAttachmentService
{
    /// <param name="ownerType">Mã DB (REQUEST) hoặc PascalCase (Request).</param>
    Task<Result<AttachmentDto>> UploadAsync(string ownerType, int ownerId, string purpose, FileUpload file, CancellationToken ct);
    Task<Result<IReadOnlyList<AttachmentDto>>> ListAsync(string ownerType, int ownerId, CancellationToken ct);
    Task<Result<AttachmentContent>> OpenAsync(long attachmentId, CancellationToken ct);

    /// <summary>Gỡ ảnh = IsCurrent = 0 và ghi RemovedAt. File và dòng được giữ 15 ngày rồi job dọn tệp mới xóa (IAttachmentCleanupService).</summary>
    Task<Result> RemoveAsync(long attachmentId, CancellationToken ct);
}
