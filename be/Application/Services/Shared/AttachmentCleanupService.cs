using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Dọn tệp chứng từ sau <see cref="RetentionDays"/> ngày, theo hai trường hợp:
/// (1) ảnh đã gỡ hoặc bị thay (IsCurrent = 0): xóa file rồi xóa dòng Attachment, tính từ lúc gỡ (RemovedAt);
/// (2) file trong kho không còn dòng Attachment nào trỏ tới (vd. tải lên xong nhưng chưa ghi được dòng): xóa file cũ hơn hạn.
/// Chỉ đụng loại chứng từ có tệp ở IFileStorage và file đúng dạng khóa ảnh; tệp bảng lương, file chấm công nhập có kho riêng.
/// </summary>
public sealed class AttachmentCleanupService : IAttachmentCleanupService
{
    public const int RetentionDays = 15;

    private static readonly Regex StoredKey = new(@"^[A-Z_]+/\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|webp)$", RegexOptions.Compiled);

    private readonly IUnitOfWork _uow;
    private readonly IFileStorage _storage;
    private readonly IClock _clock;
    private readonly ILogger<AttachmentCleanupService> _logger;

    public AttachmentCleanupService(IUnitOfWork uow, IFileStorage storage, IClock clock, ILogger<AttachmentCleanupService> logger)
    {
        _uow = uow; _storage = storage; _clock = clock; _logger = logger;
    }

    public async Task<int> RunAsync(DateTime utcNow, CancellationToken ct)
    {
        var removed = await DeleteRemovedAsync(ct);
        var unreferenced = await DeleteUnreferencedFilesAsync(utcNow, ct);
        _logger.LogInformation("Dọn tệp chứng từ: đã xóa {Removed} ảnh đã gỡ quá {Days} ngày, {Unreferenced} file không còn dòng nào trỏ tới",
            removed, RetentionDays, unreferenced);
        return removed + unreferenced;
    }

    private async Task<int> DeleteRemovedAsync(CancellationToken ct)
    {
        var cutoff = _clock.Now.AddDays(-RetentionDays);
        var owners = AttachmentOwnerTypes.InFileStorage.ToArray();
        var attachments = _uow.Repository<Attachment>();
        var rows = await attachments.Query()
            .Where(a => !a.IsCurrent && a.RemovedAt != null && a.RemovedAt < cutoff && owners.Contains(a.OwnerType))
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;

        var finished = new List<Attachment>();
        var deletedFiles = 0;
        foreach (var row in rows)
        {
            try
            {
                if (await _storage.DeleteAsync(row.FileUrl, ct)) deletedFiles++;
                finished.Add(row);   // file đã mất sẵn thì dòng cũng không còn để giữ
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Giữ dòng lại để lần chạy sau thử xóa tiếp.
                _logger.LogWarning(ex, "Không xóa được ảnh đã gỡ {Key} (Attachment {Id})", row.FileUrl, row.AttachmentId);
            }
        }
        attachments.RemoveRange(finished);
        await _uow.SaveChangesAsync();
        return deletedFiles;
    }

    private async Task<int> DeleteUnreferencedFilesAsync(DateTime utcNow, CancellationToken ct)
    {
        var cutoff = utcNow.AddDays(-RetentionDays);
        var old = new List<StoredFile>();
        foreach (var folder in AttachmentOwnerTypes.InFileStorage)
            old.AddRange((await _storage.ListAsync(folder, ct)).Where(f => StoredKey.IsMatch(f.Key) && f.LastWriteUtc < cutoff));
        if (old.Count == 0) return 0;

        // Mọi dòng Attachment (kể cả ảnh đã gỡ còn trong hạn giữ) đều giữ file của nó.
        var used = (await _uow.Repository<Attachment>().Query().Select(a => a.FileUrl).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var deleted = 0;
        foreach (var file in old.Where(f => !used.Contains(f.Key)))
        {
            try
            {
                if (await _storage.DeleteAsync(file.Key, ct)) deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Không xóa được file mồ côi {Key}", file.Key);
            }
        }
        return deleted;
    }
}
