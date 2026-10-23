using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC48: Thủ kho lập phiếu kiểm kê, sửa và gửi lại phiếu bị trả.</summary>
public sealed class StocktakesController : WarehouseKeeperBaseController
{
    private readonly IStocktakeService _stocktakes;

    public StocktakesController(IStocktakeService stocktakes) => _stocktakes = stocktakes;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] StocktakeQuery query, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveStocktakeDto dto, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveStocktakeDto dto, CancellationToken ct) =>
        this.ToResponse(await _stocktakes.UpdateAsync(id, dto, ct));
}
