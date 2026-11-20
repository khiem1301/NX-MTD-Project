namespace NX_MTD.Application.DTOs;

// ================= DEV-ALL-115: nhập Excel định mức mã hàng + bìa của đơn (một file, 4 bảng) =================

// ---- Dòng thô đọc từ file (parser trả về, chưa đối chiếu dữ liệu hệ thống) ----

/// <param name="ComponentType">PANEL / TRIM, null khi ô sai (ParseError có lý do).</param>
public sealed record SpecComponentExcelRow(int RowNumber, string Name, string? ComponentType, short? PiecesPerGarment,
    string? ParseError);

public sealed record SpecNormExcelRow(int RowNumber, string Color, string ComponentName, string MaterialName, string? SpecNote,
    string? ParseError);

public sealed record SpecMarkerGroupExcelRow(int RowNumber, int? MarkerNo, IReadOnlyList<string> ComponentNames,
    IReadOnlyList<string> Colors, string? ParseError);

/// <param name="Status">ACTIVE / DISCONTINUED (ô trống = ACTIVE).</param>
/// <param name="Ratios">Số sản phẩm mỗi size trên một lớp, chỉ các cột size có số &gt; 0.</param>
public sealed record SpecOrderMarkerExcelRow(int RowNumber, string? ProductCode, int? MarkerNo, string MarkerCode, decimal? LengthM,
    decimal? WidthM, string Status, IReadOnlyList<MarkerRatioDto> Ratios, string? ParseError);

/// <summary>Bảng nào có trong file thì cờ Has… bật (kể cả khi không có dòng). SizeColumns = tên các cột size của bảng bìa của đơn.</summary>
public sealed record SpecWorkbookDto(
    bool HasComponents, IReadOnlyList<SpecComponentExcelRow> Components,
    bool HasNorms, IReadOnlyList<SpecNormExcelRow> Norms,
    bool HasMarkerGroups, IReadOnlyList<SpecMarkerGroupExcelRow> MarkerGroups,
    bool HasOrderMarkers, IReadOnlyList<SpecOrderMarkerExcelRow> OrderMarkers,
    IReadOnlyList<string> SizeColumns);

// ---- Kết quả xem trước trả cho màn hình ----

/// <summary>Action: New | Update | Unchanged | Error (PascalCase như nhập công đoạn).</summary>
public sealed record SpecImportComponentRowDto(int RowNumber, string Action, string? Error, string ComponentName,
    string? ComponentType, short? PiecesPerGarment);

public sealed record SpecImportNormRowDto(int RowNumber, string Action, string? Error, string Color, string ComponentName,
    string? MaterialName, string? Unit, string? MaterialColor, string? SpecNote);

public sealed record SpecImportMarkerGroupRowDto(int RowNumber, string Action, string? Error, int? MarkerNo,
    IReadOnlyList<string> ComponentNames, IReadOnlyList<string> Colors);

public sealed record SpecImportOrderMarkerRowDto(int RowNumber, string Action, string? Error, int? MarkerNo, string MarkerCode,
    decimal? LengthM, decimal? WidthM, string Status, IReadOnlyList<MarkerRatioDto> Ratios);

/// <param name="OrderId">Đơn nhận bìa của đơn; null = không nhập bìa của đơn (bảng bìa trong file bị bỏ qua, xem Notes).</param>
/// <param name="CrossErrors">Lỗi chỉ thấy khi gộp file với dữ liệu đang có (vd nhóm bìa có hai vải ở một màu): còn lỗi thì không nhập được.</param>
/// <param name="Notes">Ghi chú không chặn nhập: cột size bị bỏ, dòng của mã hàng khác, bảng bị bỏ qua.</param>
public sealed record SpecImportPreviewDto(
    int ProductId, string ProductCode, int? OrderId, string? OrderCode,
    IReadOnlyList<SpecImportComponentRowDto> Components,
    IReadOnlyList<SpecImportNormRowDto> Norms,
    IReadOnlyList<SpecImportMarkerGroupRowDto> MarkerGroups,
    IReadOnlyList<SpecImportOrderMarkerRowDto> OrderMarkers,
    bool HasComponents, bool HasNorms, bool HasMarkerGroups, bool HasOrderMarkers,
    int ValidCount, int ErrorCount, int DuplicateCount, int ChangeCount,
    IReadOnlyList<string> CrossErrors, IReadOnlyList<string> Notes, bool CanImport,
    int RemovedCount = 0, IReadOnlyList<string>? RemovedNames = null, IReadOnlyList<string>? OrderSizes = null);

/// <param name="Removed">Chế độ thay thế: số dòng cũ bị bỏ vì không có trong file (xóa, hoặc ngừng dùng nếu đang được dùng).</param>
public sealed record SpecImportResultDto(int Components, int Norms, int MarkerGroups, int OrderMarkers, int Removed = 0);
