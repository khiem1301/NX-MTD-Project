using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-117: xem trước dòng hàng của đơn đọc từ Excel (ma trận màu × size). Không ghi gì: màn hình điền các dòng hợp lệ vào form
/// tạo / sửa đơn và người dùng bấm Lưu, nên mọi kiểm tra khi lưu đơn (validator, mã hàng đang sản xuất, trùng dòng...) giữ nguyên.
/// Màu mới được phép (lưu đơn sẽ tạo màu như gõ tay).
/// </summary>
public sealed class OrderImportService : IOrderImportService
{
    private const int MaxColorLength = 50;
    private const decimal MaxCmtPrice = 100_000_000m;
    private const int MaxQty = 1_000_000;

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

        var codes = excel.Rows.Select(r => r.ProductCode).Where(c => c.Length > 0).Distinct().ToList();
        var products = (await _uow.Repository<Product>().Query().AsNoTracking().Where(p => codes.Contains(p.ProductCode))
                .Select(p => new { p.ProductId, p.ProductCode, p.ProductName, p.Status }).ToListAsync(ct))
            .ToDictionary(p => p.ProductCode, StringComparer.OrdinalIgnoreCase);

        // Form đơn có một đơn giá cho mỗi mã hàng: các dòng cùng mã hàng phải cùng giá (lấy giá của dòng đầu làm chuẩn).
        var priceOf = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        var rows = new List<OrderImportRowDto>(excel.Rows.Count);
        foreach (var row in excel.Rows)
        {
            products.TryGetValue(row.ProductCode, out var product);
            var error = row.ParseError
                        ?? (product is null ? string.Format(Messages.OrderImportProductUnknown, row.ProductCode) : null)
                        ?? (product!.Status != RecordStatus.Active ? string.Format(Messages.OrderImportProductInactive, row.ProductCode) : null)
                        ?? (row.Color.Length > MaxColorLength ? Messages.OrderImportColorTooLong : null)
                        ?? PriceProblem(row.CmtUnitPrice!.Value)
                        ?? (row.Quantities.Count == 0 ? Messages.OrderImportNoQty : null)
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
                priceOf.TryAdd(row.ProductCode, row.CmtUnitPrice!.Value);
            }
            rows.Add(new OrderImportRowDto(row.RowNumber, product?.ProductCode ?? row.ProductCode, error is null ? product!.ProductId : null,
                product?.ProductName, row.Color, row.CmtUnitPrice, row.Quantities, error));
        }

        var errors = rows.Count(r => r.Error is not null);
        return Result<OrderImportPreviewDto>.Success(new OrderImportPreviewDto(SizeOrder.Sort(excel.SizeColumns), rows,
            rows.Count - errors, errors - duplicates, duplicates));
    }

    private static string? PriceProblem(decimal price) =>
        price < 0 || price > MaxCmtPrice || decimal.Round(price, 2) != price ? Messages.OrderImportPriceInvalid : null;
}
