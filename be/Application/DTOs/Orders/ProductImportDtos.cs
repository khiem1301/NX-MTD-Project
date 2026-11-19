namespace NX_MTD.Application.DTOs;

// ================= Dòng Mã hàng của file Excel nhiều sheet (popup kiểm tra form mã hàng) =================

public sealed record ProductExcelRow(int RowNumber, string ProductCode, string ProductName, string? Description);

/// <param name="Action">New | Exists (đã có, bỏ qua) | Error.</param>
/// <param name="Note">Lý do lỗi, hoặc "đã có" khi Exists.</param>
public sealed record ProductImportRowDto(int RowNumber, string Action, string? Note, string ProductCode, string ProductName,
    string? Description)
{
    public string? Error => Action == "Error" ? Note : null;
}

public sealed record ProductImportPreviewDto(IReadOnlyList<ProductImportRowDto> Rows, int NewCount, int ExistsCount, int ErrorCount,
    int DuplicateCount);

