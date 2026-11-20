namespace NX_MTD.Application.DTOs;

// ================= DEV-ALL-122: popup kiểm tra file Excel nhiều sheet trước khi lưu mã hàng =================

/// <summary>
/// Mọi bảng đọc được từ một file nhiều sheet (chưa đối chiếu hệ thống). Màn hình sửa trực tiếp rồi gửi lại để kiểm / nhập, nên
/// đây cũng là body của các API kiểm tra và nhập "-data". Bảng không có trong file: cờ Has… = false, danh sách rỗng.
/// </summary>
public sealed record ExcelBundleDto(
    bool HasProducts, IReadOnlyList<ProductExcelRow> Products,
    SpecWorkbookDto Spec,
    bool HasOrderMaterials, IReadOnlyList<OrderMaterialExcelRow> OrderMaterials);

/// <param name="ProductId">Mã hàng đang sửa; null = đang thêm mới (chưa có màu, chưa có đơn).</param>
/// <param name="OrderId">Đơn nhận bìa cắt, NVL của đơn; null = chưa có đơn (các dòng đó trả Pending).</param>
public sealed record ExcelBundleCheckDto(int? ProductId, int? OrderId, ExcelBundleDto Bundle);

/// <summary>Kết quả kiểm từng bảng; Action thêm giá trị Pending (đúng định dạng, kiểm phần còn lại khi Lưu đơn).</summary>
public sealed record ExcelBundleCheckResultDto(
    ProductImportPreviewDto? Products,
    SpecImportPreviewDto Spec,
    OrderMaterialImportPreviewDto? OrderMaterials,
    IReadOnlyList<MissingMaterialDto> MissingMaterials);

/// <summary>
/// DEV-ALL-123: tên NVL trong file chưa có trong danh mục. SuggestedType = FABRIC khi NVL dùng cho chi tiết PANEL ở bảng định mức
/// (còn lại TRIM); Colors = các màu áo dùng NVL này (gợi ý màu NVL). Màn hình hỏi admin có thêm vào danh mục khi lưu không.
/// </summary>
public sealed record MissingMaterialDto(string MaterialName, string SuggestedType, IReadOnlyList<string> Colors,
    string? SuggestedUnit = null);
