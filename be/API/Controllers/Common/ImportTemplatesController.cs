using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Common;

/// <summary>
/// File mẫu nhập Excel theo từng chức năng: bảng nào đã có dữ liệu (mã hàng / đơn hàng đang mở) thì ghi dữ liệu đó,
/// bảng chưa có thì ghi dòng ví dụ chữ khác màu.
/// </summary>
[Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Accountant}")]
public sealed class ImportTemplatesController : CommonBaseController
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly IImportTemplateService _templates;

    public ImportTemplatesController(IImportTemplateService templates) => _templates = templates;

    /// <param name="kind">spec | order-lines | order-materials | order-markers | operations | product-form.</param>
    [HttpGet("{kind}")]
    public async Task<IActionResult> Get(string kind, [FromQuery] int? productId, [FromQuery] int? orderId,
        CancellationToken ct)
    {
        var result = await _templates.BuildAsync(kind, productId, orderId, ct);
        if (!result.IsSuccess) return this.ToResponse(result);
        return File(result.Data!.Content, Xlsx, result.Data.FileName);
    }
}
