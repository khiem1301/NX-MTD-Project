using System.Collections.Concurrent;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Tạo <see cref="ProductVariant"/> cho test (05/11: màu nằm ở <see cref="ProductColor"/>). Cùng mã hàng và cùng tên màu
/// (không phân biệt hoa thường) dùng chung một màu như database (UQ_PCol), để định mức theo màu (DEV-ALL-87) khớp mọi size.
/// </summary>
internal static class TestVariant
{
    private static int _next = 900;
    private static readonly ConcurrentDictionary<(int, string), ProductColor> Colors = new();

    /// <summary>Màu áo dùng chung của mã hàng cho test.</summary>
    public static ProductColor ColorOf(int productId, string color) =>
        Colors.GetOrAdd((productId, color.Trim().ToUpperInvariant()),
            _ => new ProductColor { ProductColorId = Interlocked.Increment(ref _next), ProductId = productId, Color = color });

    public static ProductVariant Make(Product product, string color = "Đen", string size = "M")
    {
        var productColor = ColorOf(product.ProductId, color);
        return new ProductVariant
        {
            ProductVariantId = Interlocked.Increment(ref _next), ProductId = product.ProductId, Product = product,
            ProductColorId = productColor.ProductColorId, ProductColor = productColor,
            SizeCode = size, Status = RecordStatus.Active,
        };
    }

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
