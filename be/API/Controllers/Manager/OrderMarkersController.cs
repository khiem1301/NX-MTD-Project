using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>Bìa cắt của đơn (DEV-ALL-85): xem bìa theo đơn, lưu bìa của một mã hàng trong đơn.</summary>
public sealed class OrderMarkersController : ManagerBaseController
{
    private readonly IOrderMarkerService _markers;
    private readonly ISpecImportService _import;
    private readonly IExcelBundleService _bundle;
    private readonly IProductSpecService _specs;

    public OrderMarkersController(IOrderMarkerService markers, ISpecImportService import, IExcelBundleService bundle,
        IProductSpecService specs)
    {
        _markers = markers; _import = import; _bundle = bundle; _specs = specs;
    }

    /// <summary>Định mức của mã hàng (thành phần, màu, định mức, nhóm bìa) để popup nhập Excel gợi ý và nạp dữ liệu đang có; Quản đốc chỉ đọc.</summary>
    [HttpGet("product-spec/{productId:int}")]
    public async Task<IActionResult> GetProductSpec(int productId, CancellationToken ct) =>
        this.ToResponse(await _specs.GetAsync(productId, ct));

    /// <summary>Đọc mọi bảng của file Excel (multipart: file) để popup kiểm tra sửa trực tiếp; không ghi gì. Cùng việc với API Admin của mã hàng, mở cho Quản đốc.</summary>
    [HttpPost("excel/parse")]
    public IActionResult ParseExcel(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        return this.ToResponse(_bundle.Parse(stream));
    }

    /// <summary>Kiểm lại bản đã sửa trên popup (đơn có thể có, mã hàng có thể chưa); không ghi gì.</summary>
    [HttpPost("excel/check")]
    public async Task<IActionResult> CheckExcel([FromBody] ExcelBundleCheckDto dto, CancellationToken ct) =>
        this.ToResponse(await _bundle.CheckAsync(dto, ct));

    /// <summary>
    /// Nhập bìa của đơn từ bảng đã sửa trên popup (body JSON), một giao dịch. Chỉ nhận bảng markers: nhập bìa cắt không được đổi định mức
    /// mã hàng, nên các bảng khác (components, norms, groups) trong body bị bỏ qua dù client có gửi; không có markers thì từ chối.
    /// </summary>
    [HttpPost("{orderId:int}/products/{productId:int}/import-data")]
    public async Task<IActionResult> ImportData(int orderId, int productId, [FromBody] SpecWorkbookDto workbook, [FromQuery] string? parts,
        CancellationToken ct, [FromQuery] bool replace = false)
    {
        if (SplitParts(parts)?.Contains("markers") != true)
            return this.ToResponse(Result.Failure(ErrorCodes.SpecImportInvalid,
                string.Format(Messages.SpecImportPartMissing, SpecImportParts.Label(SpecImportParts.Markers)), ResultKind.Validation));
        return this.ToResponse(await _import.ImportAsync(productId, orderId, workbook, ct, [SpecImportParts.Markers], replace));
    }

    private static IReadOnlyCollection<string>? SplitParts(string? parts) =>
        string.IsNullOrWhiteSpace(parts)
            ? null
            : parts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => p.ToLowerInvariant()).Distinct().ToList();

    /// <summary>DEV-ALL-115: xem trước file Excel định mức + bìa của đơn cho một mã hàng của đơn (multipart: file).</summary>
    [HttpPost("{orderId:int}/products/{productId:int}/import/preview")]
    public async Task<IActionResult> PreviewImport(int orderId, int productId, IFormFile file, CancellationToken ct, [FromQuery] bool replace = false)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.PreviewAsync(productId, orderId, stream, ct, replace: replace));
    }

    /// <summary>DEV-ALL-115 / DEV-ALL-117: Quản đốc và Admin nhập định mức mã hàng + bìa của đơn trong một giao dịch.</summary>
    [HttpPost("{orderId:int}/products/{productId:int}/import")]
    public async Task<IActionResult> Import(int orderId, int productId, IFormFile file, CancellationToken ct, [FromQuery] bool replace = false)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.ImportAsync(productId, orderId, stream, ct, replace: replace));
    }

    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> Get(int orderId, CancellationToken ct) =>
        this.ToResponse(await _markers.GetAsync(orderId, ct));

    [HttpPut("{orderId:int}/products/{productId:int}")]
    public async Task<IActionResult> Save(int orderId, int productId, [FromBody] SaveOrderMarkersDto dto, CancellationToken ct) =>
        this.ToResponse(await _markers.SaveAsync(orderId, productId, dto, ct));
}
