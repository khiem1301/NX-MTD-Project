namespace NX_MTD.Application.DTOs;

public sealed record AttachmentDto(long AttachmentId, string OwnerType, int OwnerId, string Purpose,
    string? FileName, string? ContentType, DateTime UploadedAt);

/// <summary>File đã đọc vào bộ nhớ (≤ 5 MB) – API tạo từ IFormFile, Application không biết ASP.NET.</summary>
public sealed record FileUpload(byte[] Content, string FileName);

public sealed record AttachmentContent(Stream Content, string ContentType, string? FileName);
