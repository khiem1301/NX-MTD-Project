namespace NX_MTD.Application.DTOs;

/// <summary>
/// TH-16: nội dung một chứng từ kho để in / xuất Excel — tiêu đề, các dòng thông tin chung, bảng dòng hàng và nơi ký tên.
/// Writer chỉ việc đổ ra file, không biết loại chứng từ.
/// </summary>
public sealed record PrintableDocument(
    string Title, string Code, IReadOnlyList<KeyValuePair<string, string>> Info,
    IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, IReadOnlyList<string> Signers);
