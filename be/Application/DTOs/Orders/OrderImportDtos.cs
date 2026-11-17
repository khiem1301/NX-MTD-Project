namespace NX_MTD.Application.DTOs;

// ================= DEV-ALL-118: nhập dòng hàng của đơn từ Excel (ma trận màu × size) =================

/// <summary>Số lượng một size của một dòng màu (chỉ size có số &gt; 0).</summary>
public sealed record OrderImportQtyDto(string SizeCode, int Qty);

/// <summary>Một dòng thô đọc từ file: mã hàng × màu, đơn giá CMT và số lượng từng size.</summary>
public sealed record OrderImportExcelRow(int RowNumber, string ProductCode, string Color, decimal? CmtUnitPrice,
    IReadOnlyList<OrderImportQtyDto> Quantities, string? ParseError);

public sealed record OrderImportExcelDto(IReadOnlyList<string> SizeColumns, IReadOnlyList<OrderImportExcelRow> Rows);

/// <param name="ProductId">null khi mã hàng không có / đã ngừng (Error có lý do).</param>
public sealed record OrderImportRowDto(int RowNumber, string ProductCode, int? ProductId, string? ProductName, string Color,
    decimal? CmtUnitPrice, IReadOnlyList<OrderImportQtyDto> Quantities, string? Error);

/// <summary>Kết quả xem trước; màn hình điền các dòng hợp lệ vào form, người dùng bấm Lưu như tạo tay.</summary>
public sealed record OrderImportPreviewDto(IReadOnlyList<string> SizeColumns, IReadOnlyList<OrderImportRowDto> Rows,
    int ValidCount, int ErrorCount, int DuplicateCount);
