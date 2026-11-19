using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>
/// DEV-ALL-117: Admin chỉ XEM nhóm công đoạn của mã hàng (Kế toán quản lý nhóm). Trước đây Admin thêm / đổi tên / xóa / xếp nhóm được
/// (DEV-ALL-50).
/// </summary>
public sealed class OperationGroupsController : AdminBaseController
{
    private readonly IOperationGroupService _groups;

    public OperationGroupsController(IOperationGroupService groups) => _groups = groups;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int productId, CancellationToken ct) =>
        this.ToResponse(await _groups.ListAsync(productId, ct));
}
