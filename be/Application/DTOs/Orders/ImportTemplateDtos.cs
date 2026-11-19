namespace NX_MTD.Application.DTOs;

/// <summary>
/// Cột của bảng trong file mẫu. Kind: R = bắt buộc (xanh đậm), O = tùy chọn (xanh nhạt), G = cột size / tùy chọn mới (xanh lá).
/// Options: có thì ô của cột là danh sách chọn (dropdown) gồm các giá trị này.
/// </summary>
public sealed record TemplateColumn(string Name, char Kind, double Width, IReadOnlyList<string>? Options = null);

/// <summary>
/// Một bảng (sheet) của file mẫu. IsDemo = true khi bảng chưa có dữ liệu trong hệ thống nên các dòng là ví dụ (chữ xám nghiêng);
/// false = dòng là dữ liệu thật đang có (chữ thường).
/// </summary>
public sealed record TemplateTable(string Sheet, string Title, string Note, IReadOnlyList<TemplateColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows, bool IsDemo);

public sealed record TemplateGuideRow(string Key, string Value);

/// <summary>Nội dung một file mẫu: sheet Hướng dẫn + các bảng dữ liệu.</summary>
public sealed record ImportTemplateModel(string FileName, string GuideTitle, IReadOnlyList<TemplateGuideRow> Guide,
    IReadOnlyList<TemplateTable> Tables);
