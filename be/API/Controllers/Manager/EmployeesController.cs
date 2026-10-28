using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC16: Quản đốc quản lý nhân viên và tài khoản của mọi người trừ Admin và Kế toán: xem, thêm, sửa, nghỉ việc,
/// khóa / mở khóa / đặt lại mật khẩu. Mật khẩu tạm chỉ trả một lần, không cache.</summary>
public sealed class EmployeesController : ManagerBaseController
{
    private readonly IEmployeeService _employees;
    private readonly IAccountAdminService _accounts;

    public EmployeesController(IEmployeeService employees, IAccountAdminService accounts)
    {
        _employees = employees; _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] EmployeeQuery query, CancellationToken ct) =>
        this.ToResponse(await _employees.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => this.ToResponse(await _employees.GetAsync(id, ct));

    [HttpPost, ResponseCache(NoStore = true)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto, CancellationToken ct) =>
        this.ToResponse(await _employees.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ManagerUpdateEmployeeDto dto, CancellationToken ct) =>
        this.ToResponse(await _employees.ManagerUpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/resign")]
    public async Task<IActionResult> Resign(int id, [FromBody] ResignEmployeeDto dto, CancellationToken ct) =>
        this.ToResponse(await _employees.ResignAsync(id, dto, ct));

    [HttpPost("{id:int}/account/lock")]
    public async Task<IActionResult> Lock(int id, [FromBody] LockAccountDto dto, CancellationToken ct) =>
        this.ToResponse(await _accounts.LockAsync(id, dto, ct));

    [HttpPost("{id:int}/account/unlock")]
    public async Task<IActionResult> Unlock(int id, CancellationToken ct) => this.ToResponse(await _accounts.UnlockAsync(id, ct));

    [HttpPost("{id:int}/account/reset-password"), ResponseCache(NoStore = true)]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken ct) =>
        this.ToResponse(await _accounts.ResetPasswordAsync(id, ct));
}
