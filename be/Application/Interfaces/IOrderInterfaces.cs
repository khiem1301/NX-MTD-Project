using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<IReadOnlyList<CustomerDto>> ListAsync(string? keyword, string? statusCode, CancellationToken ct);
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
    Task<IReadOnlyList<string>> ListColorsAsync(CancellationToken ct);
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
    Task<PagedResult<OrderLookupDto>> LookupAsync(OrderLookupQuery query, IReadOnlyCollection<string> statuses, CancellationToken ct);
    Task<bool> ExistsAsync(int orderId, CancellationToken ct);
    Task<IReadOnlyList<OrderLineLookupDto>> LookupLinesAsync(int orderId, CancellationToken ct);
}

public interface ICustomerService
{
    Task<Result<IReadOnlyList<CustomerDto>>> ListAsync(CustomerQuery query, CancellationToken ct);
    Task<Result<int>> CreateAsync(SaveCustomerDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int customerId, SaveCustomerDto dto, CancellationToken ct);
    Task<Result> DiscontinueAsync(int customerId, CancellationToken ct);
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

public interface IProductSpecService
{
    Task<Result<ProductSpecDto>> GetAsync(int productId, CancellationToken ct);
    Task<Result<ProductSpecDto>> SaveAsync(int productId, SaveProductSpecDto dto, CancellationToken ct);
}

public interface ICustomerOrderService
{
    Task<Result<PagedResult<OrderListItemDto>>> ListAsync(OrderQuery query, CancellationToken ct);
    Task<Result<string>> SuggestCodeAsync(CancellationToken ct);
    Task<Result<OrderDetailDto>> GetAsync(int orderId, CancellationToken ct);
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
    Task<Result<IReadOnlyList<OrderLineLookupDto>>> LinesAsync(int orderId, CancellationToken ct);
    Task<Result<PagedResult<ProductLookupDto>>> ProductsAsync(PageQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<string>>> ColorsAsync(CancellationToken ct);
}
