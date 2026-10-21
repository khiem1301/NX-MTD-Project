using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC32 — Admin: xem bảng lương Chờ duyệt, duyệt (khóa tháng), từ chối, mở lại về Nháp.</summary>
public sealed class PayrollsController : AdminBaseController
{
    private readonly IPayrollService _payroll;

    public PayrollsController(IPayrollService payroll) => _payroll = payroll;

    /// <summary>Danh sách bảng lương, lọc theo trạng thái (vd. PendingApproval để xem "Chờ duyệt").</summary>
    [HttpGet]
    public async Task<IActionResult> GetPayrolls([FromQuery] PayrollQuery query, CancellationToken ct) =>
        this.ToResponse(await _payroll.GetPayrollsAsync(query, ct));

    /// <summary>Chi tiết bảng lương: tổng quỹ lương và từng phiếu lương theo nhân viên.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct) =>
        this.ToResponse(await _payroll.GetDetailAsync(id, ct));

    /// <summary>Tải nguyên file Excel bảng thanh toán lương Kế toán đã nhập, để giám đốc xem trước khi duyệt (DEV-MI-09-5).</summary>
    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> DownloadImportedFile(int id, CancellationToken ct)
    {
        var result = await _payroll.GetImportedFileAsync(id, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }

    /// <summary>Duyệt bảng lương: khóa bảng lương + khóa luôn tháng kế toán ("chốt tháng").</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct) =>
        this.ToResponse(await _payroll.ApproveAsync(id, ct));

    /// <summary>Từ chối bảng lương (bắt buộc lý do).</summary>
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectPayrollDto dto, CancellationToken ct) =>
        this.ToResponse(await _payroll.RejectAsync(id, dto, ct));

    /// <summary>Mở lại bảng lương đã duyệt về Nháp (bắt buộc lý do), mở khóa luôn tháng.</summary>
    [HttpPost("{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id, [FromBody] ReopenPayrollDto dto, CancellationToken ct) =>
        this.ToResponse(await _payroll.ReopenAsync(id, dto, ct));
}
