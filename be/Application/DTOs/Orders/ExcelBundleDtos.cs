namespace NX_MTD.Application.DTOs;

// ================= DEV-ALL-122: popup kiểm tra file Excel nhiều sheet trước khi lưu mã hàng =================

/// <summary>
/// Mọi bảng đọc được từ một file nhiều sheet (chưa đối chiếu hệ thống). Màn hình sửa trực tiếp rồi gửi lại để kiểm / nhập, nên
/// đây cũng là body của các API kiểm tra và nhập "-data". Bảng không có trong file: cờ Has… = false, danh sách rỗng.
/// </summary>
public sealed record ExcelBundleDto(
    bool HasProducts, IReadOnlyList<ProductExcelRow> Products,
    SpecWorkbookDto Spec,
    bool HasOrderMaterials, IReadOnlyList<OrderMaterialExcelRow> OrderMaterials,
    bool HasMaterials = false, IReadOnlyList<MaterialExcelRow>? Materials = null);

/// <summary>
/// DEV-ALL-192: một dòng sheet "NVL" (thông tin đầy đủ của NVL: mã, tên, loại, đơn vị, màu, khổ vải). Mã đã có trong hệ thống thì mọi
/// thông tin phải trùng; mã chưa có thì NVL được thêm vào danh mục khi lưu.
/// </summary>
public sealed record MaterialExcelRow(int RowNumber, string MaterialCode, string? MaterialName, string? MaterialType, string? Unit,
    string? Color, decimal? WidthM, string? ParseError);

/// <summary>Kết quả kiểm một dòng sheet NVL: Action = New | Unchanged | Error.</summary>
public sealed record MaterialCheckRowDto(int RowNumber, string Action, string? Error);

/// <param name="ProductId">Mã hàng đang sửa; null = đang thêm mới (chưa có màu, chưa có đơn).</param>
/// <param name="OrderId">Đơn nhận bìa cắt, NVL của đơn; null = chưa có đơn (các dòng đó trả Pending).</param>
/// <param name="Replace">DEV-ALL-155: thay thế — dòng cũ không có trong file sẽ bị xóa (trừ dòng đã dùng).</param>
public sealed record ExcelBundleCheckDto(int? ProductId, int? OrderId, ExcelBundleDto Bundle, bool Replace = false);

/// <summary>Kết quả kiểm từng bảng; Action thêm giá trị Pending (đúng định dạng, kiểm phần còn lại khi Lưu đơn).</summary>
public sealed record ExcelBundleCheckResultDto(
    ProductImportPreviewDto? Products,
    SpecImportPreviewDto Spec,
    OrderMaterialImportPreviewDto? OrderMaterials,
    IReadOnlyList<MissingMaterialDto> MissingMaterials,
    IReadOnlyList<MaterialCheckRowDto>? Materials = null);

/// <summary>
/// DEV-ALL-123: mã NVL trong file chưa có trong danh mục. SuggestedType = FABRIC khi mã dùng cho chi tiết PANEL ở bảng định mức
/// (còn lại TRIM); Colors = các màu áo dùng mã này (gợi ý màu NVL). Màn hình hỏi admin có thêm vào danh mục khi lưu không.
/// </summary>
public sealed record MissingMaterialDto(string MaterialCode, string SuggestedType, IReadOnlyList<string> Colors,
    string? SuggestedName = null, string? SuggestedUnit = null, decimal? SuggestedWidthM = null, string? SuggestedColor = null,
    bool FromSheet = false);
