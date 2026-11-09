namespace NX_MTD.Domain.Constants;

/// <summary>Bộ lọc nguồn cấp ở bảng NVL của đơn (giá trị FE gửi lên).</summary>
public static class OrderMaterialSupplies
{
    public const string Customer = "Customer";
    public const string Purchase = "Purchase";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Customer, Purchase };
}

/// <summary>Bộ lọc tình trạng nhập kho ở bảng NVL của đơn (giá trị FE gửi lên).</summary>
public static class OrderMaterialReceiptStates
{
    public const string Pending = "Pending";
    public const string Done = "Done";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Pending, Done };
}

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

/// <summary>
/// Ảnh của mã hàng: một ảnh chính (Product.SampleImageUrl) và tối đa 5 ảnh phụ (ProductImage). Ảnh theo màu áo đã bỏ (DEV-ALL-80).
/// </summary>
public static class ProductImageLimits
{
    public const int MaxExtra = 5;
}
