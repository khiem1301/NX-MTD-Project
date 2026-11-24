using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-118: xem trước dòng hàng của đơn đọc từ Excel (ma trận màu × size). Không ghi gì: màn hình điền các dòng hợp lệ vào form
/// tạo / sửa đơn và người dùng bấm Lưu, nên mọi kiểm tra khi lưu đơn (validator, mã hàng đang sản xuất, trùng dòng...) giữ nguyên.
/// Màu mới được phép (lưu đơn sẽ tạo màu như gõ tay).
/// </summary>
public sealed class OrderImportService : IOrderImportService
{
    private const int MaxColorLength = 50;
    private const decimal MaxCmtPrice = 100_000_000m;
    private const int MaxQty = 1_000_000;
    private const int MaxSizeLength = 10;

    private readonly IUnitOfWork _uow;
    private readonly IExcelOrderLinesParser _parser;

    public OrderImportService(IUnitOfWork uow, IExcelOrderLinesParser parser)
    {
        _uow = uow; _parser = parser;
    }

    public async Task<Result<OrderImportPreviewDto>> PreviewAsync(Stream stream, CancellationToken ct)
    {
        OrderImportExcelDto excel;
        try
        {
            excel = _parser.Parse(stream);
        }
        catch (InvalidDataException ex)
        {
            return Result<OrderImportPreviewDto>.Failure(ErrorCodes.OrderImportInvalid, ex.Message, ResultKind.Validation);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidOperationException
                                       or NotImplementedException)
        {
            return Result<OrderImportPreviewDto>.Failure(ErrorCodes.OrderImportInvalid, Messages.SpecImportExcelInvalid,
                ResultKind.Validation);
        }

        return await PreviewAsync(excel, ct);
    }

    /// <summary>
    /// Kiểm các dòng đã đọc (hoặc đã sửa trên màn hình, DEV-ALL-178): mã hàng, màu, đơn giá, số lượng từng size, trùng dòng. Không ghi gì.
    /// </summary>
    public async Task<Result<OrderImportPreviewDto>> PreviewAsync(OrderImportExcelDto input, CancellationToken ct)
    {
        // Dòng sửa trên màn hình có thể còn khoảng trắng thừa: chuẩn hóa trước khi đối chiếu.
        var excel = new OrderImportExcelDto(input.SizeColumns, input.Rows.Select(r => r with
        {
            ProductCode = (r.ProductCode ?? string.Empty).Trim(),
            Color = (r.Color ?? string.Empty).Trim(),
            Quantities = (r.Quantities ?? []).Select(q => q with { SizeCode = (q.SizeCode ?? string.Empty).Trim().ToUpperInvariant() }).ToList(),
        }).ToList());
        var codes = excel.Rows.Select(r => r.ProductCode).Where(c => c.Length > 0).Distinct().ToList();
        var products = (await _uow.Repository<Product>().Query().AsNoTracking().Where(p => codes.Contains(p.ProductCode))
                .Select(p => new { p.ProductId, p.ProductCode, p.Status }).ToListAsync(ct))
            .ToDictionary(p => p.ProductCode, StringComparer.OrdinalIgnoreCase);

        // Form đơn có một đơn giá cho mỗi mã hàng: các dòng cùng mã hàng phải cùng giá (lấy giá của dòng đầu làm chuẩn).
        var priceOf = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        var rows = new List<OrderImportRowDto>(excel.Rows.Count);
        foreach (var row in excel.Rows)
        {
            products.TryGetValue(row.ProductCode, out var product);
            var inactive = product is not null && product.Status != RecordStatus.Active;
            // Mã chưa có vẫn kiểm số lượng, giá, trùng màu. Chỉ khi phần đó ổn mới báo thiếu mã (form đơn nhận dòng sau khi khai mã).
            var error = row.ParseError
                        ?? (row.ProductCode.Length == 0 ? Messages.Required("mã hàng") : null)
                        ?? (row.Color.Length == 0 ? Messages.Required("màu") : null)
                        ?? (row.Color.Length > MaxColorLength ? Messages.OrderImportColorTooLong : null)
                        ?? (row.CmtUnitPrice is null ? Messages.OrderImportPriceInvalid : PriceProblem(row.CmtUnitPrice.Value))
                        ?? (row.Quantities.Count == 0 ? Messages.OrderImportNoQty : null)
                        ?? (row.Quantities.Any(q => q.SizeCode.Length == 0 || q.SizeCode.Length > MaxSizeLength) ? Messages.OrderImportSizeInvalid : null)
                        ?? (row.Quantities.Any(q => q.Qty < 1) ? Messages.OrderImportQtyInvalid : null)
                        ?? (row.Quantities.GroupBy(q => q.SizeCode).Any(g => g.Count() > 1) ? Messages.OrderImportSizeDuplicate : null)
                        ?? (row.Quantities.Any(q => q.Qty > MaxQty) ? Messages.OrderImportQtyTooLarge : null)
                        ?? (priceOf.TryGetValue(row.ProductCode, out var first) && first != row.CmtUnitPrice
                            ? string.Format(Messages.OrderImportPriceMismatch, row.ProductCode) : null);
            if (error is null && seen.TryGetValue($"{row.ProductCode}|{row.Color.Trim()}", out var firstRow))
            {
                error = string.Format(Messages.SpecImportDuplicateRow, firstRow);
                duplicates++;
            }
            if (error is null)
            {
                seen[$"{row.ProductCode}|{row.Color.Trim()}"] = row.RowNumber;
                if (row.CmtUnitPrice is decimal price) priceOf.TryAdd(row.ProductCode, price);
                if (inactive)
                    error = string.Format(Messages.OrderImportProductInactive, row.ProductCode);
                else if (product is null)
                    error = string.Format(Messages.OrderImportProductUnknown, row.ProductCode);
            }
            var missing = product is null && error == string.Format(Messages.OrderImportProductUnknown, row.ProductCode);
            rows.Add(new OrderImportRowDto(row.RowNumber, product?.ProductCode ?? row.ProductCode,
                error is null ? product!.ProductId : null, row.Color, row.CmtUnitPrice, row.Quantities, error, missing));
        }

        var errors = rows.Count(r => r.Error is not null);
        return Result<OrderImportPreviewDto>.Success(new OrderImportPreviewDto(SizeOrder.Sort(excel.SizeColumns), rows,
            rows.Count - errors, errors - duplicates, duplicates));
    }

    private static string? PriceProblem(decimal price) =>
        price < 0 || price > MaxCmtPrice || decimal.Round(price, 2) != price ? Messages.OrderImportPriceInvalid : null;
}
