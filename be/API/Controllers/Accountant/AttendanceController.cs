using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC28 — Admin / Quản đốc / Kế toán: đồng bộ RJ, xem snapshot NV, bảng công.
/// Chỉ Admin / Quản đốc tạo tài khoản (qua API Nhân viên); Kế toán chỉ xem danh sách RJ.</summary>
[ApiController]
[Route("api/v1/accountant/[controller]")]
[Authorize(Roles = Roles.Accountant + "," + Roles.Admin + "," + Roles.Manager)]
public sealed class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendance;
    private readonly ICurrentUser _user;

    public AttendanceController(IAttendanceService attendance, ICurrentUser user)
    {
        _attendance = attendance;
        _user = user;
    }

    /// <summary>Tình trạng đồng bộ; IsOverdue = true thì FE hiện cảnh báo MSG23.</summary>
    [HttpGet("sync-health")]
    public async Task<IActionResult> GetSyncHealth(CancellationToken ct) =>
        this.ToResponse(await _attendance.GetHealthAsync(ct));

    [HttpGet("sync-sessions")]
    public async Task<IActionResult> GetSyncSessions([FromQuery] int take, [FromQuery] string? status, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetSessionsAsync(take, status, ct));

    /// <summary>Danh sách NV trên máy RJ (snapshot agent). onlyUnmapped = chỉ mã chưa tạo trên hệ thống.</summary>
    [HttpGet("rj-employees")]
    public async Task<IActionResult> GetRjEmployees([FromQuery] bool onlyUnmapped, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetRjEmployeesAsync(onlyUnmapped, ct));

    /// <summary>Yêu cầu agent đồng bộ lại danh sách NHANVIEN từ máy RJ.</summary>
    [HttpPost("rj-employees/sync")]
    public async Task<IActionResult> RequestRjEmployeeSync(CancellationToken ct) =>
        this.ToResponse(await _attendance.RequestRjEmployeeSyncAsync(ct));

    [HttpGet("timesheets")]
    public async Task<IActionResult> GetTimesheets([FromQuery] TimesheetQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetTimesheetsAsync(query, TimesheetScope.All, ct));

    /// <summary>Số liệu tổng hợp cho dải thẻ trên đầu màn hình bảng công.</summary>
    [HttpGet("timesheets/summary")]
    public async Task<IActionResult> GetSummary([FromQuery] TimesheetQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetSummaryAsync(query, TimesheetScope.All, ct));

    // ---------- Chấm bù (UC29) ----------

    /// <summary>Chấm bù cho nhân viên (GBR-25: bắt buộc lý do).</summary>
    [HttpPost("adjustments")]
    public async Task<IActionResult> CreateAdjustment([FromBody] CreateAttendanceAdjustmentDto dto, CancellationToken ct) =>
        this.ToResponse(await _attendance.CreateAdjustmentAsync(dto, _user.EmployeeId, ct));

    /// <summary>Danh sách chấm bù đã tạo.</summary>
    [HttpGet("adjustments")]
    public async Task<IActionResult> GetAdjustments([FromQuery] AdjustmentQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetAdjustmentsAsync(query, ct));

    // ---------- Import Excel chấm công (UC29) ----------

    /// <summary>Xem trước file Excel dự phòng (chưa ghi DB).</summary>
    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewExcel(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _attendance.PreviewExcelAsync(stream, file.FileName, ct));
    }

    /// <summary>Nhập file Excel dự phòng (Source = EXCEL_IMPORT).</summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportExcel(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _attendance.ImportExcelAsync(stream, file.FileName, ct));
    }
}
