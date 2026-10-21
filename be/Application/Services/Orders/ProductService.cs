using Microsoft.Extensions.Options;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Orders;

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

    private const string SampleImageFolder = "PRODUCT";

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
        return await _uow.InTransactionAsync(async () =>   // D17: log cần ProductId
        {
            await _products.AddAsync(product);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(Product), product.ProductId, AuditActions.Insert, null, Snapshot(product)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(product.ProductId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int productId, SaveProductDto dto, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(productId);
        if (product is null) return Fail.NotFound();

        var code = dto.ProductCode.Trim().ToUpperInvariant();
        if (code != product.ProductCode)
        {
            if (await _products.IsUsedAsync(productId, ct))
                return Result.Failure(ErrorCodes.ProductCodeLocked, Messages.ProductCodeLocked);
            if (await _products.CodeExistsAsync(code, productId, ct))
                return Result.Failure(ErrorCodes.ProductCodeDuplicate, Messages.ProductCodeDuplicate);
        }

        var before = Snapshot(product);
        product.ProductCode = code;
        product.ProductName = dto.ProductName.Trim();
        product.SampleImageUrl = Clean(dto.SampleImageUrl);
        product.Description = Clean(dto.Description);
        _audit.Add(new AuditEntry(nameof(Product), productId, AuditActions.Update, before, Snapshot(product)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

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

    private static object Snapshot(Product p) => new { p.ProductCode, p.ProductName, p.SampleImageUrl, p.Description, p.Status };
}
