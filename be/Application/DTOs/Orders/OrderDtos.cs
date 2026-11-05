using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

// ---- Khách hàng ----
public sealed record CustomerDto(int CustomerId, string CustomerName, string? Phone, string? Email, string? DefaultAddress, string Status,
    string? DiscontinueReason = null);

/// <summary>Lọc và phân trang danh mục khách hàng: Status = Active | Discontinued (PascalCase như Supplier); Keyword tìm theo tên hoặc số điện thoại.</summary>
public sealed record CustomerQuery : PageQuery
{
    public string? Status { get; init; }
}

/// <summary>Khách hàng rút gọn cho ô chọn (form đơn hàng, bộ lọc đơn hàng): không phân trang, không có SĐT / email / địa chỉ.</summary>
public sealed record CustomerOptionDto(int CustomerId, string CustomerName, string Status);
/// <summary>Lý do ngừng hợp tác với khách hàng (bắt buộc, lưu vào hồ sơ và nhật ký).</summary>
public sealed record DiscontinueCustomerDto(string Reason);
public sealed record SaveCustomerDto(string CustomerName, string? Phone, string? Email, string? DefaultAddress);

// ---- Mã hàng ----
public sealed record ProductQuery : PageQuery
{
    public string? Status { get; init; }   // PascalCase: Active / Discontinued; null = tất cả
}

/// <summary>Ảnh của một màu áo (1-3 khóa ảnh tải từ máy, theo thứ tự hiển thị), DEV-ALL-71.</summary>
public sealed record ProductColorImagesDto(string Color, IReadOnlyList<string> Images);

/// <param name="ExtraImages">Khóa các ảnh phụ theo thứ tự hiển thị (tối đa 5); ảnh chính là SampleImageUrl.</param>
/// <param name="ColorImages">Ảnh theo màu áo, xếp theo tên màu.</param>
public sealed record ProductListItemDto(int ProductId, string ProductCode, string ProductName, string? SampleImageUrl,
    string Status, bool IsUsed, DateTime CreatedAt, IReadOnlyList<string> ExtraImages,
    IReadOnlyList<ProductColorImagesDto> ColorImages);

/// <param name="Colors">Các màu áo đã có biến thể (do đơn hàng tạo), để gợi ý khi khai ảnh theo màu.</param>
public sealed record ProductDetailDto(int ProductId, string ProductCode, string ProductName, string? SampleImageUrl,
    string? Description, string? LastSizeRatio, string Status, bool IsUsed, DateTime CreatedAt,
    IReadOnlyList<string> ExtraImages, IReadOnlyList<ProductColorImagesDto> ColorImages, IReadOnlyList<string> Colors,
    IReadOnlyList<AuditLogDto> History);

/// <param name="SampleImageUrl">Ảnh chính: khóa ảnh tải từ máy, hoặc đường dẫn http/https (dữ liệu cũ).</param>
/// <param name="ExtraImages">Ảnh phụ (tối đa 5, khóa ảnh tải từ máy, theo thứ tự hiển thị). null = giữ nguyên, danh sách rỗng = xóa hết.</param>
/// <param name="ColorImages">Ảnh theo màu áo (mỗi màu 1-3 ảnh, không trùng màu). null = giữ nguyên, danh sách rỗng = xóa hết.</param>
public sealed record SaveProductDto(string ProductCode, string ProductName, string? SampleImageUrl, string? Description,
    IReadOnlyList<string>? ExtraImages = null, IReadOnlyList<ProductColorImagesDto>? ColorImages = null);

// ---- Đơn hàng ----
/// <param name="OrderLineId">null = dòng mới; có giá trị = dòng cũ (sửa).</param>
public sealed record OrderLineInput(int? OrderLineId, int ProductId, string Color, string SizeCode, int OrderedQty, decimal CmtUnitPrice);

/// <summary>Phần chung của tạo / sửa đơn hàng, để dùng chung validator.</summary>
public interface IOrderInput
{
    string OrderCode { get; }
    int CustomerId { get; }
    DateOnly ReceivedDate { get; }
    DateOnly DueDate { get; }
    string? Note { get; }
    IReadOnlyList<OrderLineInput> Lines { get; }
}

public sealed record CreateOrderDto(string OrderCode, int CustomerId, DateOnly ReceivedDate, DateOnly DueDate, string? Note,
    IReadOnlyList<OrderLineInput> Lines) : IOrderInput;

/// <summary>Gửi đủ mọi dòng muốn giữ (D27). Dòng cũ không có trong danh sách = xóa (chỉ khi NEW).</summary>
public sealed record UpdateOrderDto(string OrderCode, int CustomerId, DateOnly ReceivedDate, DateOnly DueDate, string? Note,
    IReadOnlyList<OrderLineInput> Lines, byte[] RowVer) : IOrderInput;

public sealed record CancelOrderDto(string? Reason, byte[] RowVer);
public sealed record StopOrderDto(string Reason, byte[] RowVer);
public sealed record StartOrderProductionDto(byte[] RowVer);

public sealed record OrderQuery : PageQuery
{
    public string? Status { get; init; }      // PascalCase
    public int? CustomerId { get; init; }
    public DateOnly? DueFrom { get; init; }
    public DateOnly? DueTo { get; init; }
}

public sealed record OrderListItemDto(int OrderId, string OrderCode, string CustomerName, DateOnly ReceivedDate, DateOnly DueDate,
    string Status, string MaterialStatus, bool IsDelayRisk, int TotalQty, decimal TotalAmount, string ProductCodes);

public sealed record OrderLineDto(int OrderLineId, int ProductId, string ProductCode, string ProductName, string Color,
    string SizeCode, int OrderedQty, decimal CmtUnitPrice);

public sealed record OrderDetailDto
{
    public int OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateOnly ReceivedDate { get; init; }
    public DateOnly DueDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string MaterialStatus { get; init; } = string.Empty;
    public bool IsDelayRisk { get; init; }
    public string? StopReason { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public byte[] RowVer { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<OrderLineDto> Lines { get; init; } = Array.Empty<OrderLineDto>();
    public IReadOnlyList<AuditLogDto> History { get; init; } = Array.Empty<AuditLogDto>();
}

// ---- Xem đơn không có dữ liệu mật (GBR-02: KHÔNG có đơn giá, thành tiền, lịch sử; không có RowVer vì chỉ đọc) ----
public sealed record OrderOverviewListItemDto(int OrderId, string OrderCode, string CustomerName, DateOnly ReceivedDate, DateOnly DueDate,
    string Status, string MaterialStatus, bool IsDelayRisk, int TotalQty, string ProductCodes);

public sealed record OrderOverviewDto(int OrderId, string OrderCode, string CustomerName, DateOnly ReceivedDate, DateOnly DueDate,
    string Status, string MaterialStatus, bool IsDelayRisk, string? StopReason, string? Note, DateTime CreatedAt,
    IReadOnlyList<OrderLineLookupDto> Lines);

// ---- Tra cứu cho luồng khác (D26: KHÔNG có đơn giá) ----
public sealed record OrderLookupQuery : PageQuery
{
    public string? Status { get; init; }      // null = OrderStatuses.Running
}

public sealed record OrderLookupDto(int OrderId, string OrderCode, string CustomerName, DateOnly DueDate, string Status, string MaterialStatus);
public sealed record OrderLineLookupDto(int OrderLineId, int OrderId, int ProductId, string ProductCode, string ProductName,
    string Color, string SizeCode, int OrderedQty);
public sealed record ProductLookupDto(int ProductId, string ProductCode, string ProductName);
