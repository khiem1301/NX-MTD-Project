namespace Application.Tests;

/// <summary>
/// Các test đọc / ghi file Excel (ClosedXML, OpenXml) chạy tuần tự: chạy song song trên máy CI Linux
/// thỉnh thoảng tạo gói xlsx lỗi ("An item with the same key has already been added. Key: rels").
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExcelTestCollection
{
    public const string Name = "Excel";
}
