using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC49: Quản đốc duyệt phiếu kiểm kê (điều chỉnh kho) hoặc trả lại có lý do.</summary>
public sealed class StocktakesController : ManagerBaseController
{
    private readonly IStocktakeService _stocktakes;

    public StocktakesController(IStocktakeService stocktakes) => _stocktakes = stocktakes;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] StocktakeQuery query, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.GetByIdAsync(id, ct));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.ApproveAsync(id, ct));

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.ReturnAsync(id, dto, ct));
}
