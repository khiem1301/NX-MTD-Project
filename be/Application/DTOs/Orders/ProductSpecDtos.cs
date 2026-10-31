namespace NX_MTD.Application.DTOs;

// ---- Định mức theo mã hàng (UC10): thành phần (PANEL / TRIM) + biến thể (màu × size) + NVL của từng biến thể ----

/// <param name="ComponentType">Panel (chi tiết cắt từ vải, qua tổ cắt) hoặc Trim (phụ liệu cấp thẳng cho tổ may).</param>
/// <param name="PiecesPerGarment">Số chi tiết trên 1 sản phẩm, vd tay áo = 2 (dùng khi cắt bù một chi tiết).</param>
public sealed record ProductComponentDto(int ProductComponentId, string ComponentName, string ComponentType,
    short PiecesPerGarment, string Status);

/// <summary>Biến thể (màu × size) do đơn hàng tạo ra (chỉ đọc ở màn định mức); LengthCm / WidthCm là kích thước thành phẩm (29/10).</summary>
public sealed record ProductVariantDto(int ProductVariantId, string Color, string SizeCode, decimal? LengthCm, decimal? WidthCm, string Status);

/// <summary>Một dòng định mức = một NVL của một thành phần cho một biến thể (29/10: không còn lượng / hao hụt), kèm kích thước chi tiết.
/// MaterialColor = Material.Color (26/10: màu nằm trong NVL).</summary>
public sealed record ProductNormDto(int ProductComponentMaterialId, string ComponentName, string Color, string SizeCode,
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal? LengthCm, decimal? WidthCm, string? SpecNote);

public sealed record ProductSpecDto(int ProductId, IReadOnlyList<ProductComponentDto> Components,
    IReadOnlyList<ProductVariantDto> Variants, IReadOnlyList<ProductNormDto> Norms);

/// <param name="ProductComponentId">null = thành phần mới (hoặc khớp theo tên với thành phần đã có).</param>
public sealed record SaveProductComponentDto(int? ProductComponentId, string ComponentName, string ComponentType,
    short PiecesPerGarment);

/// <summary>Biến thể phải đã có (do đơn hàng tạo); lưu định mức chỉ cập nhật kích thước thành phẩm.</summary>
public sealed record SaveProductVariantDto(int? ProductVariantId, string Color, string SizeCode, decimal? LengthCm, decimal? WidthCm);

/// <summary>Dòng định mức tham chiếu thành phần và biến thể bằng tên / màu / size để lưu cùng lúc với thành phần và biến thể mới.</summary>
public sealed record SaveProductNormDto(string ComponentName, string Color, string SizeCode, int MaterialId,
    decimal? LengthCm, decimal? WidthCm, string? SpecNote);

/// <summary>
/// Gửi đủ thành phần, biến thể (đã có, chỉ cập nhật kích thước) và định mức muốn giữ. Thành phần bỏ khỏi danh sách: xóa nếu chưa dùng ở đâu,
/// ngược lại chuyển sang ngừng dùng. Biến thể không bao giờ bị xóa ở đây. Định mức của các biến thể có trong danh sách được thay bằng danh sách gửi lên.
/// </summary>
public sealed record SaveProductSpecDto(IReadOnlyList<SaveProductComponentDto> Components,
    IReadOnlyList<SaveProductVariantDto> Variants, IReadOnlyList<SaveProductNormDto> Norms);
