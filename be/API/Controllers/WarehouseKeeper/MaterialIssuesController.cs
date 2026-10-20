using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC40: Thủ kho cấp vải cho tổ cắt (theo lệnh cắt) và BTP cho tổ may; số cấp được giữ chỗ.</summary>
public sealed class MaterialIssuesController : WarehouseKeeperBaseController
{
    private readonly IMaterialIssueService _issues;
    private readonly ICurrentUser _user;

    public MaterialIssuesController(IMaterialIssueService issues, ICurrentUser user)
    {
        _issues = issues; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] MaterialIssueQuery query, CancellationToken ct) =>
        this.ToResponse(await _issues.GetPagedAsync(query, null, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _issues.GetByIdAsync(id, null, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaterialIssueDto dto, CancellationToken ct) =>
        this.ToResponse(await _issues.CreateAsync(dto, _user.EmployeeId, ct));
}
