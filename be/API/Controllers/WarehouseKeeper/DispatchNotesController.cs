using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC46: Thủ kho xác nhận xuất hàng (trừ kho thành phẩm) hoặc từ chối phiếu xuất.</summary>
public sealed class DispatchNotesController : WarehouseKeeperBaseController
{
    private readonly IDispatchNoteService _notes;
    private readonly ICurrentUser _user;

    public DispatchNotesController(IDispatchNoteService notes, ICurrentUser user)
    {
        _notes = notes; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] DispatchNoteQuery query, CancellationToken ct) =>
        this.ToResponse(await _notes.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _notes.GetByIdAsync(id, ct));

    [HttpPost("{id:int}/export")]
    public async Task<IActionResult> Export(int id, CancellationToken ct) =>
        this.ToResponse(await _notes.ExportAsync(id, _user.EmployeeId, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _notes.RejectAsync(id, dto, _user.EmployeeId, ct));
}
