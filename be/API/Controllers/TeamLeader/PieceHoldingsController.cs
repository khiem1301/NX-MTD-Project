using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>BTP tổ may của mình đang giữ theo dòng đơn (đã nhận từ kho − đã giao CN + đã thu hồi).</summary>
public sealed class PieceHoldingsController : TeamLeaderBaseController
{
    private readonly IInventoryService _inventory;
    private readonly ICurrentUser _user;

    public PieceHoldingsController(IInventoryService inventory, ICurrentUser user)
    {
        _inventory = inventory; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? orderId, CancellationToken ct) =>
        this.ToResponse(await _inventory.GetTeamHoldingsAsync(_user.TeamId ?? 0, orderId, ct));
}
