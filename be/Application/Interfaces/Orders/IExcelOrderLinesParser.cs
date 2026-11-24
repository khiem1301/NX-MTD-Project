using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Đọc bảng dòng hàng của đơn (Mã hàng, Màu, Đơn giá CMT, các cột size) từ file Excel (DEV-ALL-118).</summary>
public interface IExcelOrderLinesParser
{
    /// <summary>Ném InvalidDataException khi không có bảng đúng mẫu.</summary>
    OrderImportExcelDto Parse(Stream stream);
}

/// <summary>DEV-ALL-118: xem trước dòng hàng của đơn từ Excel; không ghi gì (form đơn hàng lưu như tạo tay).</summary>
public interface IOrderImportService
{
    Task<Result<OrderImportPreviewDto>> PreviewAsync(Stream stream, CancellationToken ct);
    /// <summary>DEV-ALL-178: kiểm lại các dòng đã đọc và sửa trên màn hình (không đọc lại file), không ghi gì.</summary>
    Task<Result<OrderImportPreviewDto>> PreviewAsync(OrderImportExcelDto excel, CancellationToken ct);
}
