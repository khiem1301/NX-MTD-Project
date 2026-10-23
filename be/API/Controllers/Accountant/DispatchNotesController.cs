using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC47: Kế toán xác nhận khách đã nhận hàng (ngày nhận, số nhận từng dòng, ảnh biên bản giao có khách ký).</summary>
public sealed class DispatchNotesController : AccountantBaseController
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

    [HttpPost("{id:int}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(int id, [FromBody] ConfirmDeliveryDto dto, CancellationToken ct) =>
        this.ToResponse(await _notes.ConfirmDeliveryAsync(id, dto, ct));
}
