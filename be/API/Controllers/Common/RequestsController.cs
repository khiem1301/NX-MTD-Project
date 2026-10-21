using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>D12: phía người tạo đơn. Quyền theo (vai trò, loại đơn) kiểm trong service.</summary>
[Authorize]
public sealed class RequestsController : CommonBaseController
{
    private readonly IRequestService _requests;
    private readonly IRequestQueryService _queries;

    public RequestsController(IRequestService requests, IRequestQueryService queries)
    {
        _requests = requests; _queries = queries;
    }

    [HttpGet("types")]
    public IActionResult Types() =>
        this.ToResponse(Result<IReadOnlyList<RequestTypeOptionDto>>.Success(_queries.GetCreatableTypes()));

    [HttpGet]
    public async Task<IActionResult> Mine([FromQuery] RequestQuery query, CancellationToken ct) =>
        this.ToResponse(await _queries.ListMineAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct) =>
        this.ToResponse(await _queries.GetDetailAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequestDto dto, CancellationToken ct) =>
        this.ToResponse(await _requests.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRequestDto dto, CancellationToken ct) =>
        this.ToResponse(await _requests.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequestDto dto, CancellationToken ct) =>
        this.ToResponse(await _requests.CancelAsync(id, dto, ct));
}
