using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-121: tên các bảng của file định mức để nhập riêng từng bảng (form mã hàng có nút riêng cho mỗi bảng). Bảng không được
/// chọn coi như không có trong file nên dữ liệu đang có của bảng đó giữ nguyên.
/// </summary>
public static class SpecImportParts
{
    public const string Components = "components";
    public const string Norms = "norms";
    public const string Groups = "groups";
    public const string Markers = "markers";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>([Components, Norms, Groups, Markers], StringComparer.OrdinalIgnoreCase);

    /// <summary>Tên bảng hiện trong thông báo lỗi.</summary>
    public static string Label(string part) => part switch
    {
        Components => "Thành phần",
        Norms => "Định mức NVL",
        Groups => "Nhóm bìa",
        Markers => "Bìa của đơn",
        _ => part,
    };

    /// <summary>Bỏ các bảng không được chọn khỏi kết quả đọc file.</summary>
    public static SpecWorkbookDto Keep(SpecWorkbookDto workbook, IReadOnlyCollection<string> parts)
    {
        bool Selected(string name) => parts.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        var components = Selected(Components) && workbook.HasComponents;
        var norms = Selected(Norms) && workbook.HasNorms;
        var groups = Selected(Groups) && workbook.HasMarkerGroups;
        var markers = Selected(Markers) && workbook.HasOrderMarkers;
        return new SpecWorkbookDto(
            components, components ? workbook.Components : [],
            norms, norms ? workbook.Norms : [],
            groups, groups ? workbook.MarkerGroups : [],
            markers, markers ? workbook.OrderMarkers : [],
            markers ? workbook.SizeColumns : []);
    }
}
