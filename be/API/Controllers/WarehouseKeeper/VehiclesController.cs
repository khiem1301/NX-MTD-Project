using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>DEV-ALL-127: Thủ kho chọn xe công ty khi xuất kho — chỉ xe đang hoạt động và chưa có chuyến đang chạy.</summary>
public sealed class VehiclesController : WarehouseKeeperBaseController
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(CancellationToken ct) =>
        this.ToResponse(await _vehicles.GetAvailableVehiclesAsync(ct));
}
