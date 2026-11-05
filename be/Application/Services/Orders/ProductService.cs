using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _products;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IFileStorage _storage;
    private readonly AttachmentOptions _options;

    public ProductService(IProductRepository products, IAuditLogService audit, IUnitOfWork uow, IClock clock,
        IFileStorage storage, IOptions<AttachmentOptions> options)
    {
        _products = products; _audit = audit; _uow = uow; _clock = clock; _storage = storage; _options = options.Value;
    }

    private const string SampleImageFolder = SampleImageKey.Folder;

    public async Task<Result<string>> UploadSampleImageAsync(FileUpload file, CancellationToken ct)
    {
        if (file.Content.Length == 0)
            return Result<string>.Failure(ErrorCodes.AttachmentFileEmpty, Messages.AttachmentFileEmpty, ResultKind.Validation);
        if (file.Content.Length > _options.MaxFileBytes)
            return Result<string>.Failure(ErrorCodes.AttachmentFileTooLarge,
                string.Format(Messages.AttachmentFileTooLarge, _options.MaxFileMb), ResultKind.Validation);
        var kind = FileSignatures.DetectImage(file.Content.AsSpan(0, Math.Min(12, file.Content.Length)));
        if (kind is null)
            return Result<string>.Failure(ErrorCodes.AttachmentFileTypeNotAllowed, Messages.AttachmentFileTypeNotAllowed,
                ResultKind.Validation);
        return Result<string>.Success(await _storage.SaveAsync(file.Content, SampleImageFolder, kind.Extension, ct));
    }

    public async Task<Result<AttachmentContent>> OpenSampleImageAsync(string key, CancellationToken ct)
    {
        if (!SampleImageKey.IsValid(key)) return Fail.NotFound<AttachmentContent>();
        var stream = await _storage.OpenReadAsync(key, ct);
        if (stream is null) return Fail.NotFound<AttachmentContent>();
        var type = key.EndsWith(".png") ? "image/png" : key.EndsWith(".webp") ? "image/webp" : "image/jpeg";
        return Result<AttachmentContent>.Success(new AttachmentContent(stream, type, Path.GetFileName(key)));
    }

    public async Task<Result<PagedResult<ProductListItemDto>>> ListAsync(ProductQuery query, CancellationToken ct)
    {
        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = CodeText.ToCode(query.Status);
            if (status is not (RecordStatus.Active or RecordStatus.Discontinued))
                return Result<PagedResult<ProductListItemDto>>.Failure(ErrorCodes.CommonValidation, "Dữ liệu không hợp lệ.",
                    ResultKind.Validation, new Dictionary<string, string[]> { ["status"] = new[] { "Giá trị không hợp lệ." } });
        }
        return Result<PagedResult<ProductListItemDto>>.Success(await _products.ListAsync(query, status, ct));
    }

    public async Task<Result<ProductDetailDto>> GetAsync(int productId, CancellationToken ct)
    {
        var detail = await _products.GetDetailAsync(productId, ct);
        if (detail is null) return Fail.NotFound<ProductDetailDto>();
        var history = await _audit.GetHistoryAsync(nameof(Product), productId, ct);
        return Result<ProductDetailDto>.Success(detail with { History = history });
    }

    public async Task<Result<int>> CreateAsync(SaveProductDto dto, CancellationToken ct)
    {
        var code = dto.ProductCode.Trim().ToUpperInvariant();
        if (await _products.CodeExistsAsync(code, null, ct))
            return Result<int>.Failure(ErrorCodes.ProductCodeDuplicate, Messages.ProductCodeDuplicate);

        var product = new Product
        {
            ProductCode = code,
            ProductName = dto.ProductName.Trim(),
            SampleImageUrl = Clean(dto.SampleImageUrl),
            Description = Clean(dto.Description),
            Status = RecordStatus.Active,
            CreatedAt = _clock.Now,
        };
        foreach (var image in ExtraImages(dto) ?? []) product.ProductImages.Add(image);
        foreach (var row in dto.ColorImages ?? [])   // mã hàng mới: màu nào cũng là màu mới
        {
            var color = NewColor(product.ProductId, row.Color);
            foreach (var image in NewColorImages(row)) color.ProductColorImages.Add(image);
            product.ProductColors.Add(color);
        }
        return await _uow.InTransactionAsync(async () =>   // D17: log cần ProductId
        {
            await _products.AddAsync(product);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(Product), product.ProductId, AuditActions.Insert, null,
                Snapshot(product, product.ProductImages.Count, product.ProductColors.Sum(c => c.ProductColorImages.Count))));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(product.ProductId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int productId, SaveProductDto dto, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(productId);
        if (product is null) return Fail.NotFound();

        var code = dto.ProductCode.Trim().ToUpperInvariant();
        var codeError = await CheckCodeChangeAsync(product, code, ct);
        if (codeError is not null) return codeError;

        var extras = await LoadExtraImagesAsync(productId, ExtraImages(dto, productId), ct);
        var colors = await LoadColorImagesAsync(productId, dto.ColorImages, ct);
        var before = Snapshot(product, extras.Current.Count, colors.Current.Count);
        product.ProductCode = code;
        product.ProductName = dto.ProductName.Trim();
        product.SampleImageUrl = Clean(dto.SampleImageUrl);
        product.Description = Clean(dto.Description);
        _audit.Add(new AuditEntry(nameof(Product), productId, AuditActions.Update, before,
            Snapshot(product, extras.FinalCount, colors.FinalCount)));
        if (!extras.Changed && !colors.Changed)
        {
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.Saved);
        }
        return await _uow.InTransactionAsync(async () =>
        {
            // UQ_ProductImage_Sort, UQ_ProductColorImage_Sort: gỡ hết ảnh cũ trước rồi mới ghi danh sách mới.
            if (extras.Changed) _uow.Repository<ProductImage>().RemoveRange(extras.Current);
            if (colors.Changed) _uow.Repository<ProductColorImage>().RemoveRange(colors.Current);
            await _uow.SaveChangesAsync();
            if (extras.Changed) await _uow.Repository<ProductImage>().AddRangeAsync(extras.Replacement!);
            if (colors.Changed) await WriteColorImagesAsync(productId, colors, ct);
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.Saved);
        });
    }

    private async Task<Result?> CheckCodeChangeAsync(Product product, string code, CancellationToken ct)
    {
        if (code == product.ProductCode) return null;
        if (await _products.IsUsedAsync(product.ProductId, ct))
            return Result.Failure(ErrorCodes.ProductCodeLocked, Messages.ProductCodeLocked);
        if (await _products.CodeExistsAsync(code, product.ProductId, ct))
            return Result.Failure(ErrorCodes.ProductCodeDuplicate, Messages.ProductCodeDuplicate);
        return null;
    }

    /// <summary>Ảnh đang có và danh sách thay thế (null = giữ nguyên, kể cả khi gửi lên đúng như cũ).</summary>
    private sealed record ImageSet<T>(List<T> Current, List<T>? Replacement)
    {
        public bool Changed => Replacement is not null;
        public int FinalCount => Replacement?.Count ?? Current.Count;
    }

    private async Task<ImageSet<ProductImage>> LoadExtraImagesAsync(int productId, List<ProductImage>? requested, CancellationToken ct)
    {
        var current = await _uow.Repository<ProductImage>().Query().Where(i => i.ProductId == productId)
            .OrderBy(i => i.SortOrder).ToListAsync(ct);
        var same = requested is null || current.Select(i => i.ImageKey).SequenceEqual(requested.Select(i => i.ImageKey));
        return new(current, same ? null : requested);
    }

    /// <summary>Màu áo của mã hàng, ảnh theo màu đang có và danh sách thay thế (null = giữ nguyên).</summary>
    private sealed record ColorImageSet(List<ProductColor> Colors, List<ProductColorImage> Current,
        IReadOnlyList<ProductColorImagesDto>? Replacement)
    {
        public bool Changed => Replacement is not null;
        public int FinalCount => Replacement?.Sum(r => r.Images.Count) ?? Current.Count;
    }

    private async Task<ColorImageSet> LoadColorImagesAsync(int productId, IReadOnlyList<ProductColorImagesDto>? requested,
        CancellationToken ct)
    {
        var colors = await _uow.Repository<ProductColor>().Query().Include(c => c.ProductColorImages)
            .Where(c => c.ProductId == productId).ToListAsync(ct);
        var current = colors.SelectMany(c => c.ProductColorImages).ToList();
        var same = requested is null || SameColorImages(colors, requested);
        return new(colors, current, same ? null : requested);
    }

    /// <summary>Gửi lên đúng ảnh của từng màu như đang có (thứ tự màu không quan trọng) thì không ghi lại.</summary>
    private static bool SameColorImages(List<ProductColor> colors, IReadOnlyList<ProductColorImagesDto> requested)
    {
        var now = colors.Where(c => c.ProductColorImages.Count > 0).ToDictionary(c => ColorKey(c.Color),
            c => c.ProductColorImages.OrderBy(i => i.SortOrder).Select(i => i.ImageKey).ToList());
        return now.Count == requested.Count && requested.All(r =>
            now.TryGetValue(ColorKey(r.Color), out var keys) && keys.SequenceEqual(r.Images.Select(k => k.Trim())));
    }

    /// <summary>
    /// Ghi ảnh theo màu (ảnh cũ đã gỡ): màu đã có thì dùng lại (khớp tên không phân biệt hoa thường), chưa có thì tạo.
    /// Màu bị bỏ khỏi danh sách mà không có biến thể, vải của bìa (tức chỉ được tạo để khai ảnh) thì xóa luôn.
    /// </summary>
    private async Task WriteColorImagesAsync(int productId, ColorImageSet set, CancellationToken ct)
    {
        var byKey = set.Colors.ToDictionary(c => ColorKey(c.Color));
        foreach (var row in set.Replacement!)
        {
            if (!byKey.TryGetValue(ColorKey(row.Color), out var color))
            {
                color = NewColor(productId, row.Color);
                await _uow.Repository<ProductColor>().AddAsync(color);
            }
            var images = NewColorImages(row).ToList();
            images.ForEach(i => { i.ProductColor = color; i.ProductColorId = color.ProductColorId; });
            await _uow.Repository<ProductColorImage>().AddRangeAsync(images);
        }

        var kept = set.Replacement!.Select(r => ColorKey(r.Color)).ToHashSet();
        var dropped = set.Colors.Where(c => !kept.Contains(ColorKey(c.Color))).Select(c => c.ProductColorId).ToList();
        if (dropped.Count == 0) return;
        var used = (await _uow.Repository<ProductVariant>().Query().Where(v => dropped.Contains(v.ProductColorId))
                .Select(v => v.ProductColorId).ToListAsync(ct))
            .Concat(await _uow.Repository<ProductMarkerFabric>().Query().Where(f => dropped.Contains(f.ProductColorId))
                .Select(f => f.ProductColorId).ToListAsync(ct))
            .ToHashSet();
        _uow.Repository<ProductColor>().RemoveRange(set.Colors.Where(c => dropped.Contains(c.ProductColorId) && !used.Contains(c.ProductColorId)));
    }

    private static string ColorKey(string color) => color.Trim().ToUpperInvariant();

    public Task<Result> DiscontinueAsync(int productId, CancellationToken ct) =>
        ChangeStatusAsync(productId, RecordStatus.Active, RecordStatus.Discontinued);

    public Task<Result> ReactivateAsync(int productId, CancellationToken ct) =>
        ChangeStatusAsync(productId, RecordStatus.Discontinued, RecordStatus.Active);

    private async Task<Result> ChangeStatusAsync(int productId, string from, string to)
    {
        var product = await _products.GetByIdAsync(productId);
        if (product is null) return Fail.NotFound();
        if (product.Status != from) return Result.Failure(ErrorCodes.ProductInvalidStatus, Messages.ProductInvalidStatus);

        product.Status = to;
        _audit.Add(new AuditEntry(nameof(Product), productId, AuditActions.Update, new { Status = from }, new { Status = to }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(Product p, int extraImages, int colorImages) =>
        new { p.ProductCode, p.ProductName, p.SampleImageUrl, p.Description, p.Status, ExtraImages = extraImages, ColorImages = colorImages };

    /// <summary>Ảnh phụ theo thứ tự gửi lên; null = không gửi (giữ nguyên).</summary>
    private List<ProductImage>? ExtraImages(SaveProductDto dto, int productId = 0) =>
        dto.ExtraImages?.Select((key, index) => new ProductImage
        {
            ProductId = productId,
            ImageKey = key.Trim(),
            SortOrder = (byte)(index + 1),
            CreatedAt = _clock.Now,
        }).ToList();

    private ProductColor NewColor(int productId, string color) =>
        new() { ProductId = productId, Color = color.Trim(), CreatedAt = _clock.Now };

    /// <summary>Ảnh của một màu theo thứ tự gửi lên, SortOrder 1-3.</summary>
    private IEnumerable<ProductColorImage> NewColorImages(ProductColorImagesDto row) =>
        row.Images.Select((key, index) => new ProductColorImage
        {
            ImageKey = key.Trim(),
            SortOrder = (byte)(index + 1),
            CreatedAt = _clock.Now,
        });
}
