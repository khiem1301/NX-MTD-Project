using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC39: Quản đốc xác nhận (tăng tồn NVL) hoặc trả lại phiếu nhập.</summary>
public sealed class MaterialReceiptsController : ManagerBaseController
{
    private readonly IMaterialReceiptService _receipts;
    private readonly ICurrentUser _user;

    public MaterialReceiptsController(IMaterialReceiptService receipts, ICurrentUser user)
    {
        _receipts = receipts; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] MaterialReceiptQuery query, CancellationToken ct) =>
        this.ToResponse(await _receipts.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _receipts.GetByIdAsync(id, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct) =>
        this.ToResponse(await _receipts.ConfirmAsync(id, _user.EmployeeId, ct));

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _receipts.ReturnAsync(id, dto, ct));
}
