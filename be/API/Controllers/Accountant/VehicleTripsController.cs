using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC35: Kế toán ghi sổ chuyến xe và nhiên liệu; cảnh báo lệch km / xăng vượt định mức (MSG30).</summary>
public sealed class VehicleTripsController : AccountantBaseController
{
    private readonly IVehicleService _vehicles;

    public VehicleTripsController(IVehicleService vehicles) => _vehicles = vehicles;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] VehicleTripQuery query, CancellationToken ct) =>
        this.ToResponse(await _vehicles.GetTripsAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _vehicles.GetTripAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartVehicleTripDto dto, CancellationToken ct) =>
        this.ToResponse(await _vehicles.StartTripAsync(dto, ct));

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteVehicleTripDto dto, CancellationToken ct) =>
        this.ToResponse(await _vehicles.CompleteTripAsync(id, dto, ct));
}
