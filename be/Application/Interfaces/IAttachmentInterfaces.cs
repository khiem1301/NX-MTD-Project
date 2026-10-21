using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface IFileStorage
{
    /// <summary>Lưu file, trả khóa lưu trữ (vd. "REQUEST/2026/10/3f2a….jpg") để ghi vào Attachment.FileUrl.</summary>
    Task<string> SaveAsync(byte[] content, string folder, string extension, CancellationToken ct);

    /// <summary>null nếu không có file hoặc khóa không hợp lệ.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
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

public interface IAttachmentService
{
    /// <param name="ownerType">Mã DB (REQUEST) hoặc PascalCase (Request).</param>
    Task<Result<AttachmentDto>> UploadAsync(string ownerType, int ownerId, string purpose, FileUpload file, CancellationToken ct);
    Task<Result<IReadOnlyList<AttachmentDto>>> ListAsync(string ownerType, int ownerId, CancellationToken ct);
    Task<Result<AttachmentContent>> OpenAsync(long attachmentId, CancellationToken ct);

    /// <summary>Gỡ ảnh = IsCurrent = 0 (không xóa file, không xóa dòng).</summary>
    Task<Result> RemoveAsync(long attachmentId, CancellationToken ct);
}
