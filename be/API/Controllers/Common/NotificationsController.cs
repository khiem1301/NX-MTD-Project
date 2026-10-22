using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>UC05: hộp thư thông báo của mình (danh sách, số chưa đọc, đánh dấu đã đọc). Mọi vai trò.</summary>
[Authorize]
public sealed class NotificationsController : CommonBaseController
{
    private readonly INotificationInboxService _inbox;

    public NotificationsController(INotificationInboxService inbox) => _inbox = inbox;

    [HttpGet]
    public async Task<IActionResult> ListMine([FromQuery] NotificationQuery query, CancellationToken ct) =>
        this.ToResponse(await _inbox.ListMineAsync(query, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        this.ToResponse(await _inbox.UnreadCountAsync(ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct) =>
        this.ToResponse(await _inbox.MarkReadAsync(id, ct));

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct) =>
        this.ToResponse(await _inbox.MarkAllReadAsync(ct));
}
