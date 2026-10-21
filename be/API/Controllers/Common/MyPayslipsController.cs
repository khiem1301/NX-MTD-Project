using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>UC06 — công nhân (hoặc bất kỳ nhân viên nào) xem phiếu lương của chính mình, chỉ khi bảng
/// lương tháng đó đã duyệt.</summary>
[Authorize]
public sealed class MyPayslipsController : CommonBaseController
{
    private readonly IPayrollService _payroll;
    private readonly ICurrentUser _user;

    public MyPayslipsController(IPayrollService payroll, ICurrentUser user)
    {
        _payroll = payroll;
        _user = user;
    }

    /// <summary>Danh sách các tháng đã có phiếu lương được duyệt.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyPayslips(CancellationToken ct) =>
        this.ToResponse(await _payroll.GetMyPayslipsAsync(_user.EmployeeId, ct));

    /// <summary>Chi tiết phiếu lương một tháng: từng khoản cộng/trừ/bảo hiểm, thực nhận.</summary>
    [HttpGet("{periodMonth}")]
    public async Task<IActionResult> GetMyPayslip(string periodMonth, CancellationToken ct) =>
        this.ToResponse(await _payroll.GetMyPayslipAsync(_user.EmployeeId, periodMonth, ct));
}
