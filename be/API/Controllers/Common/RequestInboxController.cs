using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Common;

/// <summary>D12: phía người xử lý đơn. Chi tiết dùng chung GET /common/requests/{id}.</summary>
[Authorize(Roles = Roles.Handlers)]
public sealed class RequestInboxController : CommonBaseController
{
    private readonly IRequestService _requests;
    private readonly IRequestQueryService _queries;

    public RequestInboxController(IRequestService requests, IRequestQueryService queries)
    {
        _requests = requests; _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] RequestQuery query, CancellationToken ct) =>
        this.ToResponse(await _queries.ListInboxAsync(query, ct));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveRequestDto dto, CancellationToken ct) =>
        this.ToResponse(await _requests.ApproveAsync(id, dto, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRequestDto dto, CancellationToken ct) =>
        this.ToResponse(await _requests.RejectAsync(id, dto, ct));

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteRequestDto dto, CancellationToken ct) =>
        this.ToResponse(await _requests.CompleteAsync(id, dto, ct));
}
