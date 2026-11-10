using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <summary>Sắp size theo thứ tự chuẩn (<see cref="SizeCodes.Ordered"/>); size lạ (vd "4XL") xếp sau, theo chữ cái.</summary>
public static class SizeOrder
{
    public static int Rank(string size)
    {
        var index = IndexOf(size.Trim());
        return index < 0 ? SizeCodes.Ordered.Count : index;
    }

    public static List<string> Sort(IEnumerable<string> sizes) =>
        sizes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Rank).ThenBy(s => s, StringComparer.Ordinal).ToList();

    private static int IndexOf(string size)
    {
        for (var i = 0; i < SizeCodes.Ordered.Count; i++)
            if (string.Equals(SizeCodes.Ordered[i], size, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
