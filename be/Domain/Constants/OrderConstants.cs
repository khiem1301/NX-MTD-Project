namespace NX_MTD.Domain.Constants;

/// <summary>CustomerOrder.MaterialStatus (khác MaterialStatuses của danh mục NVL).</summary>
public static class OrderMaterialStatuses
{
    public const string Awaiting = "AWAITING";
    public const string Partial = "PARTIAL";
    public const string Complete = "COMPLETE";
}

public static class SizeCodes
{
    /// <summary>Thứ tự cột size trên lưới. Size khác (vd. "4XL") được phép, xếp sau cùng theo chữ cái.</summary>
    public static readonly IReadOnlyList<string> Ordered = new[] { "XS", "S", "M", "L", "XL", "XXL", "3XL", "FREE" };
}
