using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC45: Quản đốc lập phiếu xuất hàng kiêm lệnh giao (thành phẩm được giữ chỗ), duyệt / trả lại phiếu Thủ kho đã lập, hủy phiếu chưa xuất.</summary>
public sealed class DispatchNotesController : ManagerBaseController
{
    private readonly IDispatchNoteService _notes;

    public DispatchNotesController(IDispatchNoteService notes) => _notes = notes;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] DispatchNoteQuery query, CancellationToken ct) =>
        this.ToResponse(await _notes.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _notes.GetByIdAsync(id, ct));

    /// <summary>Xuất Excel phiếu xuất hàng để in (TH-16).</summary>
    [HttpGet("{id:int}/excel")]
    public async Task<IActionResult> ExportExcel(int id,
        [FromServices] IWarehouseDocumentExportService exports, CancellationToken ct) =>
        this.ToExcelFile(await exports.ExportDispatchNoteAsync(id, ct));

    [HttpGet("dispatchable")]
    public async Task<IActionResult> GetDispatchable([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _notes.GetDispatchableAsync(orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDispatchNoteDto dto, CancellationToken ct) =>
        this.ToResponse(await _notes.CreateAsync(dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _notes.CancelAsync(id, ct));
}
