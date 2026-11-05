using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Dòng đơn không còn lưu màu / size mà trỏ biến thể (ProductVariant): helper dựng biến thể giả để test gán
/// <c>OrderLine.ProductVariant</c> như dữ liệu thật (có sẵn Product và Id).
/// </summary>
internal static class TestVariant
{
    private static int _next = 900;

    public static ProductVariant Make(Product product, string color = "Đen", string size = "M") => new()
    {
        ProductVariantId = Interlocked.Increment(ref _next), ProductId = product.ProductId, Product = product,
        Color = color, SizeCode = size, Status = RecordStatus.Active,
    };

    public static ProductVariant Make(int productId, string productCode = "MH001", string color = "Đen", string size = "M") =>
        Make(new Product { ProductId = productId, ProductCode = productCode, ProductName = productCode }, color, size);

    /// <summary>Gán biến thể cho dòng đơn, giữ ProductId khớp với biến thể (FK ghép ProductVariantId + ProductId).</summary>
    public static OrderLine WithVariant(this OrderLine line, ProductVariant variant)
    {
        line.ProductVariant = variant;
        line.ProductVariantId = variant.ProductVariantId;
        line.ProductId = variant.ProductId;
        return line;
    }
}
