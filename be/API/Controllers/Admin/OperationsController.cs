using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>
/// DEV-ALL-116: Admin chỉ XEM công đoạn của mã hàng và duyệt đơn giá (PieceRatesController). Thêm, sửa, ngừng dùng, dùng lại và nhập
/// Excel công đoạn do Kế toán làm. Trước đây Admin quản lý công đoạn như Kế toán (DEV-ALL-50).
/// </summary>
public sealed class OperationsController : AdminBaseController
{
    private readonly IOperationService _operations;

    public OperationsController(IOperationService operations) => _operations = operations;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int productId, [FromQuery] bool includeDiscontinued, CancellationToken ct) =>
        this.ToResponse(await _operations.ListAsync(productId, includeDiscontinued, ct));
}
