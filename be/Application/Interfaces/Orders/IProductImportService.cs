using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>DEV-ALL-120: tạo mã hàng hàng loạt từ Excel; mã đã có thì bỏ qua.</summary>
public interface IProductImportService
{
    Task<Result<ProductImportPreviewDto>> PreviewAsync(Stream stream, CancellationToken ct);
    Task<Result<ProductImportResultDto>> ImportAsync(Stream stream, CancellationToken ct);
}

/// <summary>Đọc bảng mã hàng (Mã hàng, Tên mã hàng, Mô tả) từ Excel. Ném InvalidDataException khi không có bảng đúng mẫu.</summary>
public interface IExcelProductParser
{
    IReadOnlyList<ProductExcelRow> Parse(Stream stream);
}
