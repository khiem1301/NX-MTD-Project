using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Dọn ảnh mồ côi của mã hàng: file trong thư mục PRODUCT mà không còn Product.SampleImageUrl, ProductImage.ImageKey
/// nào trỏ tới.
/// Ảnh được tải lên ngay khi người dùng chọn, trước khi bấm Lưu, nên chỉ xóa file cũ hơn <see cref="GracePeriod"/>
/// để không xóa nhầm ảnh của form đang mở. Chỉ đụng file đúng dạng khóa ảnh mã hàng (<see cref="SampleImageKey"/>).
/// </summary>
public sealed class ProductImageCleanupService : IProductImageCleanupService
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _uow;
    private readonly IFileStorage _storage;
    private readonly ILogger<ProductImageCleanupService> _logger;

    public ProductImageCleanupService(IUnitOfWork uow, IFileStorage storage, ILogger<ProductImageCleanupService> logger)
    {
        _uow = uow; _storage = storage; _logger = logger;
    }

    public async Task<int> RunAsync(DateTime utcNow, CancellationToken ct)
    {
        var files = await _storage.ListAsync(SampleImageKey.Folder, ct);
        var old = files.Where(f => SampleImageKey.IsValid(f.Key) && utcNow - f.LastWriteUtc > GracePeriod).ToList();
        if (old.Count == 0) return 0;

        var mainImages = await _uow.Repository<Product>().Query().Where(p => p.SampleImageUrl != null)
            .Select(p => p.SampleImageUrl!).ToListAsync(ct);
        var extraImages = await _uow.Repository<ProductImage>().Query().Select(i => i.ImageKey).ToListAsync(ct);
        var used = mainImages.Concat(extraImages).ToHashSet(StringComparer.Ordinal);

        var deleted = 0;
        foreach (var file in old.Where(f => !used.Contains(f.Key)))
        {
            try
            {
                if (await _storage.DeleteAsync(file.Key, ct)) deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Không xóa được ảnh mồ côi {Key}", file.Key);
            }
        }
        _logger.LogInformation("Dọn ảnh mã hàng: {Checked} file quá {Hours} giờ, đã xóa {Deleted} file không còn mã hàng nào dùng",
            old.Count, GracePeriod.TotalHours, deleted);
        return deleted;
    }
}
