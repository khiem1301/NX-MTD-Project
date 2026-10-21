using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

public sealed class AttachmentRepository : IAttachmentRepository
{
    private readonly NxMtdDbContext _db;
    public AttachmentRepository(NxMtdDbContext db) => _db = db;

    public async Task AddAsync(Attachment attachment, CancellationToken ct) => await _db.Attachments.AddAsync(attachment, ct);

    public Task<Attachment?> GetAsync(long attachmentId, CancellationToken ct) =>
        _db.Attachments.FirstOrDefaultAsync(a => a.AttachmentId == attachmentId, ct);

    public Task<int> CountCurrentAsync(string ownerType, int ownerId, CancellationToken ct) =>
        _db.Attachments.CountAsync(a => a.OwnerType == ownerType && a.OwnerId == ownerId && a.IsCurrent, ct);

    public async Task<IReadOnlyList<AttachmentDto>> ListCurrentAsync(string ownerType, int ownerId, CancellationToken ct)
    {
        var rows = await _db.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId && a.IsCurrent)
            .OrderBy(a => a.UploadedAt).ThenBy(a => a.AttachmentId)
            .Select(a => new { a.AttachmentId, a.OwnerType, a.OwnerId, a.Purpose, a.FileName, a.ContentType, a.UploadedAt })
            .ToListAsync(ct);
        return rows.Select(a => new AttachmentDto(a.AttachmentId, CodeText.ToPascal(a.OwnerType), a.OwnerId,
            CodeText.ToPascal(a.Purpose), a.FileName, a.ContentType, a.UploadedAt)).ToList();
    }
}
