using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>DEV-ALL-159: Admin xem danh sách xe và trạng thái đang chạy hay không (chỉ đọc).</summary>
public sealed class VehiclesController : AdminBaseController
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    [HttpGet]
    public async Task<IActionResult> GetLiveStatus([FromQuery] VehicleLiveStatusQuery query, CancellationToken ct) =>
        this.ToResponse(await _vehicles.GetLiveStatusAsync(query, ct));
}
