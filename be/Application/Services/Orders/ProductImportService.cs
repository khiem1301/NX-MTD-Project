using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Kiểm các dòng Mã hàng của file Excel nhiều sheet trên popup kiểm tra của form mã hàng (DEV-ALL-122): mã đã có thì bỏ qua, mỗi dòng
/// kiểm như form thêm mã hàng (SaveProductDtoValidator). Không ghi gì; mã hàng được tạo bằng nút Lưu của form.
/// </summary>
public sealed class ProductImportService : IProductImportService
{
    public const string ActionNew = "New";
    public const string ActionExists = "Exists";
    public const string ActionError = "Error";

    private readonly IUnitOfWork _uow;

    public ProductImportService(IUnitOfWork uow) => _uow = uow;

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
            var error = validator.Validate(new SaveProductDto(row.ProductCode, null, row.Description))
                .Errors.Select(e => e.ErrorMessage).FirstOrDefault();
            if (error is null && seen.TryGetValue(row.ProductCode, out var first))
            {
                error = string.Format(Messages.SpecImportDuplicateRow, first);
                rows.Duplicates++;
            }
            else if (error is null) seen[row.ProductCode] = row.RowNumber;

            var action = error is not null ? ActionError : existing.Contains(row.ProductCode) ? ActionExists : ActionNew;
            rows.Add(new ProductImportRowDto(row.RowNumber, action,
                action == ActionExists ? Messages.ProductImportExists : error, row.ProductCode, row.Description));
        }
        return rows;
    }
}
