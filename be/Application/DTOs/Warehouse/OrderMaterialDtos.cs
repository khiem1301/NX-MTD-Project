namespace NX_MTD.Application.DTOs;

/// <summary>
/// PurchaseQty, Source là cột tính của DB (chỉ đọc). ReceivedQty = tổng số đạt của các phiếu nhập đã xác nhận.
/// ExpectedDeliveryCount / ExpectedDeliveryDate / ExpectedDeliveryDateTo / ReceivedCount: lịch khách giao NVL của cả đơn hàng
/// (chỉ đọc, chỉ có khi khách cấp NVL này — DEV-ALL-95); ReceivedCount = số phiếu khách cấp thường đã xác nhận của đơn.
/// MaterialColor = Material.Color (26/10: màu nằm trong NVL, chọn NVL là đã chọn màu).
/// </summary>
public sealed record OrderMaterialDto(
    int OrderMaterialId, int OrderId, int MaterialId, string MaterialCode, string MaterialName, string MaterialType,
    string Unit, string MaterialColor, decimal RequiredQty, decimal CustomerQty, short? ExpectedDeliveryCount,
    int ReceivedCount, decimal? PurchaseQty, string? Source, string? Note, decimal ReceivedQty,
    DateOnly? ExpectedDeliveryDate = null, DateOnly? ExpectedDeliveryDateTo = null,
    decimal CustomerReceivedQty = 0, decimal PurchasedReceivedQty = 0, decimal ShortQty = 0);
// DEV-ALL-119: ReceivedQty = CustomerReceivedQty (phiếu khách giao) + PurchasedReceivedQty (phiếu nhập theo đơn mua);
// ShortQty = max(RequiredQty − ReceivedQty, 0).

// ---- DEV-ALL-119: nhập Excel NVL của đơn ----

/// <summary>
/// Một dòng thô đọc từ file: mã NVL, số cần, khách cấp, ghi chú. MaterialName / Unit (DEV-ALL-123, cột tùy chọn "Tên NVL",
/// "Đơn vị") chỉ để hiện và gợi ý khi thêm NVL chưa có vào danh mục; NVL đã có thì lấy theo danh mục.
/// </summary>
public sealed record OrderMaterialExcelRow(int RowNumber, string MaterialCode, decimal? RequiredQty, decimal? CustomerQty,
    string? Note, string? ParseError, string? MaterialName = null, string? Unit = null);

/// <summary>Action: New | Update | Unchanged | Error. PurchaseQty = max(Số cần − Khách cấp, 0) tính trước để xem.</summary>
public sealed record OrderMaterialImportRowDto(int RowNumber, string Action, string? Error, string MaterialCode, string? MaterialName,
    string? MaterialColor, string? Unit, decimal? RequiredQty, decimal? CustomerQty, decimal? PurchaseQty, string? Note);

/// <param name="RemoveCount">Chế độ thay thế: số NVL của đơn sẽ bị xóa vì không có trong file.</param>
/// <param name="Removed">Mã NVL sẽ bị xóa.</param>
/// <param name="Blocked">Mã NVL không có trong file nhưng không xóa được (đã có phiếu nhập hoặc đơn mua): giữ nguyên.</param>
public sealed record OrderMaterialImportPreviewDto(IReadOnlyList<OrderMaterialImportRowDto> Rows, int ValidCount, int ErrorCount,
    int DuplicateCount, int ChangeCount, int RemoveCount = 0, IReadOnlyList<string>? Removed = null, IReadOnlyList<string>? Blocked = null);

public sealed record OrderMaterialImportResultDto(int Created, int Updated, int Removed = 0);

/// <summary>Phần chung của tạo / sửa NVL của đơn (dùng chung validator). Lịch khách giao khai ở cả đơn, không theo từng NVL.</summary>
public interface IOrderMaterialInput
{
    int MaterialId { get; }
    decimal RequiredQty { get; }
    decimal CustomerQty { get; }
    string? Note { get; }
}

public sealed record CreateOrderMaterialDto(
    int OrderId, int MaterialId, decimal RequiredQty, decimal CustomerQty, string? Note) : IOrderMaterialInput;

public sealed record UpdateOrderMaterialDto(
    int MaterialId, decimal RequiredQty, decimal CustomerQty, string? Note) : IOrderMaterialInput;

/// <summary>Một NVL trong định mức của mã hàng.</summary>
public sealed record ProductMaterialOptionDto(
    int MaterialId, string MaterialCode, string MaterialName, string MaterialType, string Unit, string Color);

/// <summary>Mã hàng trong đơn và các NVL định mức của những màu × size đơn đặt.</summary>
public sealed record OrderProductMaterialsDto(
    int ProductId, string ProductCode, string ProductName, IReadOnlyList<ProductMaterialOptionDto> Materials);

public sealed record CreateOrderMaterialLineDto(
    int MaterialId, decimal RequiredQty, decimal CustomerQty, string? Note) : IOrderMaterialInput;

/// <summary>Thêm nhiều NVL của đơn cùng lúc: một dòng lỗi thì không lưu dòng nào.</summary>
public sealed record CreateOrderMaterialBatchDto(int OrderId, IReadOnlyList<CreateOrderMaterialLineDto> Lines);

/// <summary>
/// Lịch khách giao NVL của cả đơn (DEV-ALL-95): số lần, ngày dự kiến từng lần (DeliveryDates; DeliveryDate / DeliveryDateTo là ngày đầu / cuối); ReceivedCount = số phiếu khách cấp thường đã
/// xác nhận; HasCustomerMaterials = đơn có NVL khách cấp (khi đó cần khai lịch giao).
/// </summary>
public sealed record OrderCustomerDeliveryDto(
    int OrderId, short? DeliveryCount, DateOnly? DeliveryDate, DateOnly? DeliveryDateTo, int ReceivedCount,
    bool HasCustomerMaterials, IReadOnlyList<DateOnly>? DeliveryDates = null);

/// <summary>Khai / sửa lịch khách giao NVL của cả đơn: số lần giao = số ngày giao phải nhập (DEV-ALL-113).</summary>
public sealed record SaveOrderCustomerDeliveryDto(short DeliveryCount, IReadOnlyList<DateOnly> DeliveryDates);
