using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC21: Admin xem tiến độ cắt – may – QC – giao theo đơn hàng và cờ nguy cơ trễ.</summary>
public sealed class OrderProgressController : AdminBaseController
{
    private readonly IOrderProgressService _progress;

    public OrderProgressController(IOrderProgressService progress) => _progress = progress;

    [HttpGet]
    public async Task<IActionResult> Overview([FromQuery] bool includeClosed, CancellationToken ct) =>
        this.ToResponse(await _progress.GetOverviewAsync(includeClosed, ct));

    [HttpGet("{orderId:int}/lines")]
    public async Task<IActionResult> Lines(int orderId, CancellationToken ct) =>
        this.ToResponse(await _progress.GetLinesAsync(orderId, ct));
}
