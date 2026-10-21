using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

[Authorize]
public sealed class AttachmentsController : CommonBaseController
{
    private const long MaxRequestBytes = 6 * 1024 * 1024; // > 5 MB ảnh + phần form
    private readonly IAttachmentService _attachments;
    public AttachmentsController(IAttachmentService attachments) => _attachments = attachments;

    /// <summary>multipart/form-data: ownerType, ownerId, purpose (mặc định Photo), file.</summary>
    [HttpPost, RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Upload([FromForm] string ownerType, [FromForm] int ownerId, IFormFile? file,
        [FromForm] string? purpose, CancellationToken ct)
    {
        byte[] content = Array.Empty<byte>();
        if (file is not null)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }
        var result = await _attachments.UploadAsync(ownerType, ownerId, purpose ?? "Photo",
            new FileUpload(content, file?.FileName ?? string.Empty), ct);
        return this.ToResponse(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string ownerType, [FromQuery] int ownerId, CancellationToken ct) =>
        this.ToResponse(await _attachments.ListAsync(ownerType, ownerId, ct));

    [HttpGet("{id:long}/content")]
    public async Task<IActionResult> Content(long id, CancellationToken ct)
    {
        var result = await _attachments.OpenAsync(id, ct);
        if (!result.IsSuccess || result.Data is null) return this.ToResponse(result);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Data.Content, result.Data.ContentType);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Remove(long id, CancellationToken ct) =>
        this.ToResponse(await _attachments.RemoveAsync(id, ct));
}
