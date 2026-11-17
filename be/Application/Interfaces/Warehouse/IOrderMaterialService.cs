using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IOrderMaterialService
{
    Task<Result<IReadOnlyList<OrderMaterialDto>>> GetByOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Bảng NVL của đơn có tìm kiếm, lọc, phân trang; GetByOrderAsync vẫn trả đủ dòng cho các form.</summary>
    Task<Result<PagedResult<OrderMaterialDto>>> GetPagedAsync(OrderMaterialPageQuery query, CancellationToken ct);
    /// <summary>Mã hàng trong đơn, mỗi mã kèm các NVL định mức để khai báo cho đơn.</summary>
    Task<Result<IReadOnlyList<OrderProductMaterialsDto>>> GetProductMaterialsAsync(int orderId, CancellationToken ct);
    Task<Result<OrderMaterialDto>> CreateAsync(CreateOrderMaterialDto dto, CancellationToken ct);
    Task<Result<IReadOnlyList<OrderMaterialDto>>> CreateBatchAsync(CreateOrderMaterialBatchDto dto, CancellationToken ct);
    Task<Result<OrderMaterialDto>> UpdateAsync(int orderMaterialId, UpdateOrderMaterialDto dto, CancellationToken ct);
    Task<Result> DeleteAsync(int orderMaterialId, CancellationToken ct);
    /// <summary>DEV-ALL-119: xem trước file Excel NVL của đơn (gộp theo mã NVL), không ghi gì.</summary>
    Task<Result<OrderMaterialImportPreviewDto>> PreviewImportAsync(int orderId, Stream stream, CancellationToken ct);
    /// <summary>DEV-ALL-119: nhập các dòng hợp lệ có thay đổi, lưu một lần (lỗi thì không lưu gì).</summary>
    Task<Result<OrderMaterialImportResultDto>> ImportAsync(int orderId, Stream stream, CancellationToken ct);
}

/// <summary>Đọc bảng NVL của đơn (Mã NVL, Số cần, Khách cấp, Ghi chú) từ Excel (DEV-ALL-119).</summary>
public interface IExcelOrderMaterialParser
{
    /// <summary>Ném InvalidDataException khi không có bảng đúng mẫu.</summary>
    IReadOnlyList<OrderMaterialExcelRow> Parse(Stream stream);
}
