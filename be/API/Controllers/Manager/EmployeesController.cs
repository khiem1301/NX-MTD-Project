using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC16: Quản đốc xem / sửa hồ sơ nhân viên, trừ Admin / Quản đốc / Kế toán. Không có thêm, nghỉ việc, tài khoản.</summary>
public sealed class EmployeesController : ManagerBaseController
{
    private readonly IEmployeeService _employees;
    public EmployeesController(IEmployeeService employees) => _employees = employees;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] EmployeeQuery query, CancellationToken ct) =>
        this.ToResponse(await _employees.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => this.ToResponse(await _employees.GetAsync(id, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ManagerUpdateEmployeeDto dto, CancellationToken ct) =>
        this.ToResponse(await _employees.ManagerUpdateAsync(id, dto, ct));
}
