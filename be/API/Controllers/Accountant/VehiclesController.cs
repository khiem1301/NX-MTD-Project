using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC35: Kế toán quản lý danh mục xe (biển số, loại xe, định mức xăng / 100 km).</summary>
public sealed class VehiclesController : AccountantBaseController
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] VehicleQuery query, CancellationToken ct) =>
        this.ToResponse(await _vehicles.GetVehiclesAsync(query, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveVehicleDto dto, CancellationToken ct) =>
        this.ToResponse(await _vehicles.CreateVehicleAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveVehicleDto dto, CancellationToken ct) =>
        this.ToResponse(await _vehicles.UpdateVehicleAsync(id, dto, ct));

    [HttpPost("{id:int}/discontinue")]
    public async Task<IActionResult> Discontinue(int id, CancellationToken ct) =>
        this.ToResponse(await _vehicles.ChangeVehicleStatusAsync(id, false, ct));

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct) =>
        this.ToResponse(await _vehicles.ChangeVehicleStatusAsync(id, true, ct));
}
