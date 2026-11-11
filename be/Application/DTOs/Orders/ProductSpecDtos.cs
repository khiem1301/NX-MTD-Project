namespace NX_MTD.Application.DTOs;

// ---- Định mức theo mã hàng (UC10): thành phần (PANEL / TRIM) + biến thể (màu × size) + NVL của từng biến thể ----

/// <param name="ComponentType">Panel (chi tiết cắt từ vải, qua tổ cắt) hoặc Trim (phụ liệu cấp thẳng cho tổ may).</param>
/// <param name="PiecesPerGarment">Số chi tiết trên 1 sản phẩm, vd tay áo = 2 (dùng khi cắt bù một chi tiết).</param>
public sealed record ProductComponentDto(int ProductComponentId, string ComponentName, string ComponentType,
    short PiecesPerGarment, string Status);

/// <summary>Biến thể (màu × size) do đơn hàng tạo ra, chỉ đọc ở màn định mức.</summary>
public sealed record ProductVariantDto(int ProductVariantId, string Color, string SizeCode, string Status);

/// <summary>Một dòng định mức = một NVL của một thành phần cho một màu áo, dùng chung mọi size (29/10: không còn lượng / hao hụt;
/// 09/11: không còn kích thước chi tiết, DEV-ALL-85; theo màu, DEV-ALL-87). MaterialColor = Material.Color (26/10: màu nằm trong NVL).</summary>
public sealed record ProductNormDto(int ProductComponentMaterialId, string ComponentName, string Color,
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor, string? SpecNote,
    string MaterialType = "");

/// <summary>Chi tiết nằm trên nhóm bìa nào (DEV-ALL-85). Bìa (chiều dài, tỉ lệ size) khai theo đơn ở OrderMarkerService.</summary>
public sealed record ProductMarkerComponentDto(int MarkerNo, string ComponentName);

/// <summary>Màu áo dùng nhóm bìa và vải nhóm trải ở màu đó; vải do backend lấy từ định mức của các chi tiết trên nhóm (DEV-ALL-89).</summary>
public sealed record ProductMarkerFabricDto(int MarkerNo, string Color, int MaterialId, string MaterialCode, string MaterialName,
    string MaterialColor);

public sealed record ProductSpecDto(int ProductId, IReadOnlyList<ProductComponentDto> Components,
    IReadOnlyList<ProductVariantDto> Variants, IReadOnlyList<ProductNormDto> Norms,
    IReadOnlyList<ProductMarkerComponentDto> MarkerComponents, IReadOnlyList<ProductMarkerFabricDto> MarkerFabrics);

/// <param name="ProductComponentId">null = thành phần mới (hoặc khớp theo tên với thành phần đã có).</param>
public sealed record SaveProductComponentDto(int? ProductComponentId, string ComponentName, string ComponentType,
    short PiecesPerGarment);

/// <summary>Biến thể phải đã có (do đơn hàng tạo); gửi lên để dòng định mức tham chiếu, lưu định mức không sửa biến thể.</summary>
public sealed record SaveProductVariantDto(int? ProductVariantId, string Color, string SizeCode);

/// <summary>Dòng định mức tham chiếu thành phần bằng tên và màu áo (phải có trong danh sách màu – size) để lưu cùng lúc với thành phần mới.</summary>
public sealed record SaveProductNormDto(string ComponentName, string Color, int MaterialId, string? SpecNote);

/// <summary>Chi tiết (theo tên, loại PANEL) nằm trên nhóm bìa MarkerNo.</summary>
public sealed record SaveProductMarkerComponentDto(int MarkerNo, string ComponentName);

/// <summary>Màu áo dùng nhóm bìa MarkerNo (DEV-ALL-89). Không gửi vải: backend lấy vải định mức của các chi tiết trên nhóm ở màu đó.</summary>
public sealed record SaveProductMarkerColorDto(int MarkerNo, string Color);

/// <summary>
/// Gửi đủ thành phần, biến thể (đã có, không sửa) và định mức muốn giữ. Thành phần bỏ khỏi danh sách: xóa nếu chưa dùng ở đâu,
/// ngược lại chuyển sang ngừng dùng. Biến thể không bao giờ bị xóa ở đây. Định mức của các biến thể có trong danh sách được thay bằng danh sách gửi lên.
/// Nhóm bìa (chi tiết + màu dùng nhóm) thay nguyên theo danh sách gửi lên.
/// </summary>
public sealed record SaveProductSpecDto(IReadOnlyList<SaveProductComponentDto> Components,
    IReadOnlyList<SaveProductVariantDto> Variants, IReadOnlyList<SaveProductNormDto> Norms,
    IReadOnlyList<SaveProductMarkerComponentDto> MarkerComponents, IReadOnlyList<SaveProductMarkerColorDto> MarkerColors);
