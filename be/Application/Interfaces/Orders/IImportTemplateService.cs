using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public static class ImportTemplateKinds
{
    /// <summary>Hộp thoại Nhập từ Excel ở Định mức mã hàng / tab Bìa cắt của đơn.</summary>
    public const string Spec = "spec";
    /// <summary>Dòng hàng của đơn (form tạo / sửa đơn hàng).</summary>
    public const string OrderLines = "order-lines";
    /// <summary>NVL của đơn.</summary>
    public const string OrderMaterials = "order-materials";
    /// <summary>DEV-ALL-153: chỉ Bìa của đơn (form đơn / tab Bìa cắt), không kèm định mức mã hàng.</summary>
    public const string OrderMarkers = "order-markers";
    /// <summary>Công đoạn của mã hàng.</summary>
    public const string Operations = "operations";
    /// <summary>Form Thêm mới / Sửa mã hàng: một file chung nhiều sheet.</summary>
    public const string ProductForm = "product-form";

    public static readonly IReadOnlyList<string> All =
        [Spec, OrderLines, OrderMaterials, OrderMarkers, Operations, ProductForm];
}

/// <summary>
/// File mẫu nhập Excel theo từng chức năng. Bảng nào đã có dữ liệu (của mã hàng / đơn hàng đang mở) thì file mẫu chứa dữ liệu đó;
/// bảng chưa có dữ liệu thì chứa dòng ví dụ (chữ khác màu).
/// </summary>
public interface IImportTemplateService
{
    /// <param name="kind">Một giá trị của <see cref="ImportTemplateKinds"/>.</param>
    /// <param name="productId">Mã hàng đang mở (null = chưa có).</param>
    /// <param name="orderId">Đơn hàng đang mở (null = chưa có).</param>
    Task<Result<ExcelFileDto>> BuildAsync(string kind, int? productId, int? orderId, CancellationToken ct);
}

public interface IImportTemplateWriter
{
    byte[] Write(ImportTemplateModel model);
}
