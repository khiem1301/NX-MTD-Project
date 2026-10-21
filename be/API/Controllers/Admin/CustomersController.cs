using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC09: danh mục khách hàng. Không có xóa.</summary>
public sealed class CustomersController : AdminBaseController
{
    private readonly ICustomerService _customers;
    public CustomersController(ICustomerService customers) => _customers = customers;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? keyword, CancellationToken ct) =>
        this.ToResponse(await _customers.ListAsync(keyword, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCustomerDto dto, CancellationToken ct) =>
        this.ToResponse(await _customers.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveCustomerDto dto, CancellationToken ct) =>
        this.ToResponse(await _customers.UpdateAsync(id, dto, ct));
}
