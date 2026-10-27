namespace NX_MTD.Application.DTOs;

// ---- Định mức theo mã hàng (UC10): thành phần (PANEL / TRIM) + biến thể (màu × size) + NVL của từng biến thể ----

/// <param name="ComponentType">Panel (chi tiết cắt từ vải, qua tổ cắt) hoặc Trim (phụ liệu cấp thẳng cho tổ may).</param>
/// <param name="PiecesPerGarment">Số chi tiết trên 1 sản phẩm, vd tay áo = 2 (dùng khi cắt bù một chi tiết).</param>
public sealed record ProductComponentDto(int ProductComponentId, string ComponentName, string ComponentType,
    short PiecesPerGarment, short SortOrder, string Status);

public sealed record ProductVariantDto(int ProductVariantId, string Color, string SizeCode, string Status);

/// <summary>Một dòng định mức = một NVL của một thành phần cho một biến thể; QtyPerGarment tính cho cả 1 sản phẩm (đã nhân số chi tiết).</summary>
public sealed record ProductNormDto(int ProductComponentMaterialId, string ComponentName, string Color, string SizeCode,
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor, decimal QtyPerGarment,
    decimal WastagePercent, decimal? LengthCm, decimal? WidthCm, string? SpecNote);

public sealed record ProductSpecDto(int ProductId, IReadOnlyList<ProductComponentDto> Components,
    IReadOnlyList<ProductVariantDto> Variants, IReadOnlyList<ProductNormDto> Norms);

/// <param name="ProductComponentId">null = thành phần mới (hoặc khớp theo tên với thành phần đã có).</param>
public sealed record SaveProductComponentDto(int? ProductComponentId, string ComponentName, string ComponentType,
    short PiecesPerGarment, short SortOrder);

public sealed record SaveProductVariantDto(int? ProductVariantId, string Color, string SizeCode);

/// <summary>Dòng định mức tham chiếu thành phần và biến thể bằng tên / màu / size để lưu cùng lúc với thành phần và biến thể mới.</summary>
public sealed record SaveProductNormDto(string ComponentName, string Color, string SizeCode, int MaterialId,
    string? MaterialColor, decimal QtyPerGarment, decimal WastagePercent, decimal? LengthCm, decimal? WidthCm, string? SpecNote);

/// <summary>
/// Gửi đủ thành phần, biến thể và định mức muốn giữ. Thành phần / biến thể bỏ khỏi danh sách: xóa nếu chưa dùng ở đâu,
/// ngược lại chuyển sang ngừng dùng. Định mức của các biến thể có trong danh sách được thay bằng danh sách gửi lên.
/// </summary>
public sealed record SaveProductSpecDto(IReadOnlyList<SaveProductComponentDto> Components,
    IReadOnlyList<SaveProductVariantDto> Variants, IReadOnlyList<SaveProductNormDto> Norms);
