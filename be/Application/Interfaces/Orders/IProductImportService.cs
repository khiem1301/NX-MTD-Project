using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Kiểm các dòng Mã hàng đọc từ file Excel nhiều sheet (popup kiểm tra của form mã hàng); mã đã có thì bỏ qua.</summary>
public interface IProductImportService
{
    /// <summary>DEV-ALL-122: xem trước từ dòng đã đọc file (popup kiểm tra Excel), không ghi gì.</summary>
    Task<Result<ProductImportPreviewDto>> PreviewAsync(IReadOnlyList<ProductExcelRow> rows, CancellationToken ct);
}

/// <summary>Đọc bảng mã hàng (Mã hàng, Mô tả) từ Excel. Ném InvalidDataException khi không có bảng đúng mẫu.</summary>
public interface IExcelProductParser
{
    IReadOnlyList<ProductExcelRow> Parse(Stream stream);
}
