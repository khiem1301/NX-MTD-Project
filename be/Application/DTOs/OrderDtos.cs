using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

// ---- Khách hàng ----
public sealed record CustomerDto(int CustomerId, string CustomerName, string? Phone, string? Email, string? DefaultAddress);
public sealed record SaveCustomerDto(string CustomerName, string? Phone, string? Email, string? DefaultAddress);

// ---- Mã hàng ----
public sealed record ProductQuery : PageQuery
{
    public string? Status { get; init; }   // PascalCase: Active / Discontinued; null = tất cả
}

public sealed record ProductListItemDto(int ProductId, string ProductCode, string ProductName, string? SampleImageUrl,
    string Status, bool IsUsed, DateTime CreatedAt);

public sealed record ProductDetailDto(int ProductId, string ProductCode, string ProductName, string? SampleImageUrl,
    string? Description, string? LastSizeRatio, string Status, bool IsUsed, DateTime CreatedAt,
    IReadOnlyList<AuditLogDto> History);

/// <param name="SampleImageUrl">D28: đường dẫn ảnh (http/https), chưa upload.</param>
public sealed record SaveProductDto(string ProductCode, string ProductName, string? SampleImageUrl, string? Description);

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

// ---- Tra cứu cho luồng khác (D26: KHÔNG có đơn giá) ----
public sealed record OrderLookupQuery : PageQuery
{
    public string? Status { get; init; }      // null = OrderStatuses.Running
}

public sealed record OrderLookupDto(int OrderId, string OrderCode, string CustomerName, DateOnly DueDate, string Status, string MaterialStatus);
public sealed record OrderLineLookupDto(int OrderLineId, int OrderId, int ProductId, string ProductCode, string ProductName,
    string Color, string SizeCode, int OrderedQty);
public sealed record ProductLookupDto(int ProductId, string ProductCode, string ProductName);
