using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<PagedResult<CustomerDto>> ListAsync(CustomerQuery query, string? statusCode, CancellationToken ct);
    Task<IReadOnlyList<CustomerOptionDto>> ListOptionsAsync(CancellationToken ct);
    Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct);
}

public interface IProductRepository : IGenericRepository<Product>
{
    Task<PagedResult<ProductListItemDto>> ListAsync(ProductQuery query, string? statusCode, CancellationToken ct);
    Task<ProductDetailDto?> GetDetailAsync(int productId, CancellationToken ct);   // History rỗng, service điền
    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct);
    Task<bool> IsUsedAsync(int productId, CancellationToken ct);

    /// <summary>Mã hàng KHÔNG active trong danh sách; Id không tồn tại trả "#id".</summary>
    Task<IReadOnlyList<string>> FindInactiveCodesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);
    Task<PagedResult<ProductLookupDto>> LookupAsync(PageQuery query, CancellationToken ct);

    /// <summary>Các màu đã khai ở mọi mã hàng (ProductVariant.Color), bỏ trùng không phân biệt hoa thường.</summary>
    /// <param name="productId">Có giá trị = chỉ màu của mã hàng này (DEV-ALL-132).</param>
    Task<IReadOnlyList<string>> ListColorsAsync(int? productId, CancellationToken ct);
}

public interface ICustomerOrderRepository : IGenericRepository<CustomerOrder>
{
    /// <summary>Tracked, kèm OrderLines.</summary>
    Task<CustomerOrder?> GetWithLinesAsync(int orderId, CancellationToken ct);
    /// <summary>Trạng thái (mã DB) của khách; null nếu không tồn tại.</summary>
    Task<string?> GetCustomerStatusAsync(int customerId, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct);
    Task<string?> GetMaxCodeAsync(string prefix, CancellationToken ct);
    Task<PagedResult<OrderListItemDto>> ListAsync(OrderQuery query, string? statusCode, CancellationToken ct);
    Task<OrderDetailDto?> GetDetailAsync(int orderId, CancellationToken ct);       // History rỗng
    Task<PagedResult<OrderLookupDto>> LookupAsync(OrderLookupQuery query, IReadOnlyCollection<string> statuses, int? sewingTeamId, CancellationToken ct);
    Task<bool> ExistsAsync(int orderId, CancellationToken ct);
    Task<IReadOnlyList<OrderLineLookupDto>> LookupLinesAsync(int orderId, int? sewingTeamId, CancellationToken ct);
}

public interface ICustomerService
{
    Task<Result<PagedResult<CustomerDto>>> ListAsync(CustomerQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<CustomerOptionDto>>> GetOptionsAsync(CancellationToken ct);
    Task<Result<int>> CreateAsync(SaveCustomerDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int customerId, SaveCustomerDto dto, CancellationToken ct);
    Task<Result> DiscontinueAsync(int customerId, DiscontinueCustomerDto dto, CancellationToken ct);
    Task<Result> ReactivateAsync(int customerId, CancellationToken ct);
}

public interface IProductService
{
    Task<Result<PagedResult<ProductListItemDto>>> ListAsync(ProductQuery query, CancellationToken ct);
    Task<Result<ProductDetailDto>> GetAsync(int productId, CancellationToken ct);
    Task<Result<int>> CreateAsync(SaveProductDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int productId, SaveProductDto dto, CancellationToken ct);
    Task<Result> DiscontinueAsync(int productId, CancellationToken ct);
    Task<Result> ReactivateAsync(int productId, CancellationToken ct);

    /// <summary>Tải ảnh mẫu từ máy: kiểm chữ ký ảnh + dung lượng, trả khóa lưu trữ để ghi vào SampleImageUrl.</summary>
    Task<Result<string>> UploadSampleImageAsync(FileUpload file, CancellationToken ct);
    Task<Result<AttachmentContent>> OpenSampleImageAsync(string key, CancellationToken ct);
}

/// <summary>Dọn ảnh mã hàng không còn mã hàng nào dùng (ảnh đã bị thay hoặc gỡ, ảnh tải lên nhưng chưa bao giờ được lưu).</summary>
public interface IProductImageCleanupService
{
    /// <param name="utcNow">Giờ UTC hiện tại, so với giờ ghi cuối của file (truyền vào để kiểm thử được).</param>
    /// <returns>Số file đã xóa.</returns>
    Task<int> RunAsync(DateTime utcNow, CancellationToken ct);
}

public interface IProductSpecService
{
    Task<Result<ProductSpecDto>> GetAsync(int productId, CancellationToken ct);
    Task<Result<ProductSpecDto>> SaveAsync(int productId, SaveProductSpecDto dto, CancellationToken ct);
    /// <summary>Mọi kiểm tra của Lưu định mức, không ghi (DEV-ALL-115).</summary>
    Task<Result> CheckAsync(int productId, SaveProductSpecDto dto, CancellationToken ct);
    /// <summary>Ghi định mức đã kiểm, KHÔNG mở giao dịch: người gọi bọc giao dịch (DEV-ALL-115).</summary>
    Task<Result<int>> WriteAsync(int productId, SaveProductSpecDto dto, CancellationToken ct);
}

public interface ICustomerOrderService
{
    Task<Result<PagedResult<OrderListItemDto>>> ListAsync(OrderQuery query, CancellationToken ct);
    Task<Result<string>> SuggestCodeAsync(CancellationToken ct);
    Task<Result<OrderDetailDto>> GetAsync(int orderId, CancellationToken ct);

    /// <summary>Danh sách đơn không có thành tiền (GBR-02).</summary>
    Task<Result<PagedResult<OrderOverviewListItemDto>>> ListOverviewAsync(OrderQuery query, CancellationToken ct);

    /// <summary>Chi tiết đơn không có đơn giá, không có lịch sử (lịch sử chứa đơn giá, GBR-02).</summary>
    Task<Result<OrderOverviewDto>> GetOverviewAsync(int orderId, CancellationToken ct);
    Task<Result<int>> CreateAsync(CreateOrderDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int orderId, UpdateOrderDto dto, CancellationToken ct);
    Task<Result> CancelAsync(int orderId, CancelOrderDto dto, CancellationToken ct);
    Task<Result> StopAsync(int orderId, StopOrderDto dto, CancellationToken ct);

    /// <summary>NEW → IN_PRODUCTION, chỉ khi MaterialStatus khác AWAITING (UC09, kiểm ở backend).</summary>
    Task<Result> StartProductionAsync(int orderId, StartOrderProductionDto dto, CancellationToken ct);
}

public interface IOrderLookupService
{
    Task<Result<PagedResult<OrderLookupDto>>> OrdersAsync(OrderLookupQuery query, CancellationToken ct);
    /// <summary>Tổ may chỉ thấy dòng chia cho tổ; Quản đốc / Admin truyền sewingTeamId để lọc theo tổ đang chọn.</summary>
    Task<Result<IReadOnlyList<OrderLineLookupDto>>> LinesAsync(int orderId, int? sewingTeamId, CancellationToken ct);
    Task<Result<PagedResult<ProductLookupDto>>> ProductsAsync(PageQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<string>>> ColorsAsync(int? productId, CancellationToken ct);
}

/// <summary>Bìa cắt của đơn (DEV-ALL-85): xem theo đơn, lưu theo từng mã hàng của đơn.</summary>
/// <summary>DEV-ALL-115: nhập Excel định mức mã hàng (thành phần, định mức NVL, nhóm bìa) và bìa của đơn trong một file.</summary>
public interface ISpecImportService
{
    /// <summary>Xem trước: kiểm từng dòng, gộp với dữ liệu đang có và chạy mọi kiểm tra của nút Lưu. Không ghi gì.</summary>
    /// <param name="parts">DEV-ALL-121: chỉ lấy các bảng này trong file (components, norms, groups, markers — xem SpecImportParts); null = mọi bảng.</param>
    Task<Result<SpecImportPreviewDto>> PreviewAsync(int productId, int? orderId, Stream stream, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null);
    /// <summary>Nhập: lập lại bản gộp từ file rồi lưu định mức và bìa của đơn trong một giao dịch.</summary>
    Task<Result<SpecImportResultDto>> ImportAsync(int productId, int? orderId, Stream stream, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null);
    /// <summary>DEV-ALL-122: xem trước / nhập từ bảng đã đọc file và sửa trên màn hình (popup kiểm tra Excel).</summary>
    Task<Result<SpecImportPreviewDto>> PreviewAsync(int productId, int? orderId, SpecWorkbookDto workbook, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null);
    Task<Result<SpecImportResultDto>> ImportAsync(int productId, int? orderId, SpecWorkbookDto workbook, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null);
    /// <summary>DEV-ALL-122: kiểm cho popup, kể cả mã hàng chưa tạo; dòng cần màu / đơn chưa có trả Pending. Không ghi gì.</summary>
    Task<Result<SpecImportPreviewDto>> CheckAsync(int? productId, int? orderId, SpecWorkbookDto workbook, CancellationToken ct);
}

public interface IOrderMarkerService
{
    Task<Result<OrderMarkersDto>> GetAsync(int orderId, CancellationToken ct);
    Task<Result<OrderMarkersDto>> SaveAsync(int orderId, int productId, SaveOrderMarkersDto dto, CancellationToken ct);
    /// <summary>Mọi kiểm tra của Lưu bìa, không ghi (DEV-ALL-115).</summary>
    Task<Result> CheckAsync(int orderId, int productId, SaveOrderMarkersDto dto, CancellationToken ct);
    /// <summary>Ghi bìa đã kiểm, KHÔNG mở giao dịch: người gọi bọc giao dịch (DEV-ALL-115).</summary>
    Task<Result<int>> WriteAsync(int orderId, int productId, SaveOrderMarkersDto dto, CancellationToken ct);
}
