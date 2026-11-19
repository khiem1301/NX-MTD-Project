using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-120: tạo nhiều mã hàng một lần từ Excel (Mã hàng, Tên mã hàng, Mô tả). Mã đã có thì bỏ qua (không sửa); mỗi dòng kiểm như
/// form thêm mã hàng (SaveProductDtoValidator). Các mã mới được tạo trong một giao dịch, mỗi mã một dòng nhật ký như tạo tay.
/// </summary>
public sealed class ProductImportService : IProductImportService
{
    public const string ActionNew = "New";
    public const string ActionExists = "Exists";
    public const string ActionError = "Error";

    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;
    private readonly IExcelProductParser _parser;

    public ProductImportService(IUnitOfWork uow, IAuditLogService audit, IClock clock, IExcelProductParser parser)
    {
        _uow = uow; _audit = audit; _clock = clock; _parser = parser;
    }

    public async Task<Result<ProductImportPreviewDto>> PreviewAsync(Stream stream, CancellationToken ct)
    {
        var rows = await PlanAsync(stream, ct);
        if (!rows.IsSuccess) return Fail.As<ProductImportPreviewDto>(rows);
        return Result<ProductImportPreviewDto>.Success(Summary(rows.Data!));
    }

    public async Task<Result<ProductImportResultDto>> ImportAsync(Stream stream, CancellationToken ct)
    {
        var rows = await PlanAsync(stream, ct);
        if (!rows.IsSuccess) return Fail.As<ProductImportResultDto>(rows);
        var fresh = rows.Data!.Where(r => r.Action == ActionNew).ToList();
        if (fresh.Count == 0)
            return Result<ProductImportResultDto>.Failure(ErrorCodes.ProductImportInvalid, Messages.ProductImportNothing, ResultKind.Validation);

        return await _uow.InTransactionAsync(async () =>
        {
            var products = fresh.Select(r => new Product
            {
                ProductCode = r.ProductCode,
                ProductName = r.ProductName,
                Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description,
                Status = RecordStatus.Active,
                CreatedAt = _clock.Now,
            }).ToList();
            await _uow.Repository<Product>().AddRangeAsync(products);
            await _uow.SaveChangesAsync();   // cần ProductId để ghi nhật ký (D17)
            foreach (var p in products)
                _audit.Add(new AuditEntry(nameof(Product), p.ProductId, AuditActions.Insert, null,
                    new { p.ProductCode, p.ProductName, p.Description, p.Status }));
            await _uow.SaveChangesAsync();
            var skipped = rows.Data!.Count(r => r.Action == ActionExists);
            return Result<ProductImportResultDto>.Success(new ProductImportResultDto(products.Count, skipped),
                string.Format(Messages.ProductImportDone, products.Count, skipped));
        });
    }

    private sealed class Plan : List<ProductImportRowDto>
    {
        public int Duplicates { get; set; }
    }

    private static ProductImportPreviewDto Summary(Plan rows)
    {
        var errors = rows.Count(r => r.Action == ActionError);
        return new ProductImportPreviewDto(rows, rows.Count(r => r.Action == ActionNew), rows.Count(r => r.Action == ActionExists),
            errors - rows.Duplicates, rows.Duplicates);
    }

    /// <summary>DEV-ALL-122: xem trước từ dòng đã đọc và sửa trên màn hình (popup kiểm tra Excel), không ghi gì.</summary>
    public async Task<Result<ProductImportPreviewDto>> PreviewAsync(IReadOnlyList<ProductExcelRow> rows, CancellationToken ct) =>
        Result<ProductImportPreviewDto>.Success(Summary(await PlanRowsAsync(rows, ct)));

    private async Task<Result<Plan>> PlanAsync(Stream stream, CancellationToken ct)
    {
        IReadOnlyList<ProductExcelRow> excel;
        try
        {
            excel = _parser.Parse(stream);
        }
        catch (InvalidDataException ex)
        {
            return Result<Plan>.Failure(ErrorCodes.ProductImportInvalid, ex.Message, ResultKind.Validation);
        }
        return Result<Plan>.Success(await PlanRowsAsync(excel, ct));
    }

    private async Task<Plan> PlanRowsAsync(IReadOnlyList<ProductExcelRow> excel, CancellationToken ct)
    {
        var codes = excel.Select(r => r.ProductCode).Where(c => c.Length > 0).Distinct().ToList();
        var existing = (await _uow.Repository<Product>().Query().AsNoTracking().Where(p => codes.Contains(p.ProductCode))
            .Select(p => p.ProductCode).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var validator = new SaveProductDtoValidator();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rows = new Plan();
        foreach (var row in excel)
        {
            var error = validator.Validate(new SaveProductDto(row.ProductCode, row.ProductName, null, row.Description))
                .Errors.Select(e => e.ErrorMessage).FirstOrDefault();
            if (error is null && seen.TryGetValue(row.ProductCode, out var first))
            {
                error = string.Format(Messages.SpecImportDuplicateRow, first);
                rows.Duplicates++;
            }
            else if (error is null) seen[row.ProductCode] = row.RowNumber;

            var action = error is not null ? ActionError : existing.Contains(row.ProductCode) ? ActionExists : ActionNew;
            rows.Add(new ProductImportRowDto(row.RowNumber, action,
                action == ActionExists ? Messages.ProductImportExists : error, row.ProductCode, row.ProductName, row.Description));
        }
        return rows;
    }
}
