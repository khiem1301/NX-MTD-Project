using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>NVL chờ nhận: NVL khách sẽ cấp (ngày hẹn) và đơn mua đã đặt mà kho chưa nhận đủ, để Thủ kho lập phiếu nhập.</summary>
public sealed class IncomingMaterialsController : WarehouseKeeperBaseController
{
    private readonly IIncomingMaterialService _incoming;

    public IncomingMaterialsController(IIncomingMaterialService incoming) => _incoming = incoming;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _incoming.GetAsync(ct));
}
