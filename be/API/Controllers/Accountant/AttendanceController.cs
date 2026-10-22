using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC28 — Kế toán theo dõi đồng bộ, map mã chấm công và xem bảng công toàn xưởng.</summary>
public sealed class AttendanceController : AccountantBaseController
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
    public async Task<IActionResult> GetSyncSessions([FromQuery] int take, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetSessionsAsync(take, ct));

    /// <summary>Các mã chấm công có lượt quẹt nhưng chưa gắn nhân viên.</summary>
    [HttpGet("unmapped-codes")]
    public async Task<IActionResult> GetUnmappedCodes(CancellationToken ct) =>
        this.ToResponse(await _attendance.GetUnmappedCodesAsync(ct));

    /// <summary>Gán mã chấm công cho nhân viên: nhận lại lượt quẹt cũ và tính lại bảng công.</summary>
    [HttpPost("map-code")]
    public async Task<IActionResult> MapCode([FromBody] MapAttendanceCodeDto dto, CancellationToken ct) =>
        this.ToResponse(await _attendance.MapCodeAsync(dto, _user.EmployeeId, ct));

    [HttpGet("timesheets")]
    public async Task<IActionResult> GetTimesheets([FromQuery] TimesheetQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetTimesheetsAsync(query, TimesheetScope.All, ct));

    /// <summary>Số liệu tổng hợp cho dải thẻ trên đầu màn hình bảng công.</summary>
    [HttpGet("timesheets/summary")]
    public async Task<IActionResult> GetSummary([FromQuery] TimesheetQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetSummaryAsync(query, TimesheetScope.All, ct));

    // ---------- Chấm bù (UC29) ----------

    /// <summary>Kế toán chấm bù cho nhân viên (GBR-25: bắt buộc lý do).</summary>
    [HttpPost("adjustments")]
    public async Task<IActionResult> CreateAdjustment([FromBody] CreateAttendanceAdjustmentDto dto, CancellationToken ct) =>
        this.ToResponse(await _attendance.CreateAdjustmentAsync(dto, _user.EmployeeId, ct));

    /// <summary>Danh sách chấm bù đã tạo.</summary>
    [HttpGet("adjustments")]
    public async Task<IActionResult> GetAdjustments([FromQuery] AdjustmentQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetAdjustmentsAsync(query, ct));

    // ---------- Import Excel chấm công (UC29) ----------

    /// <summary>Xem trước file Excel dự phòng (chưa ghi DB), để Kế toán kiểm tra trước khi nhập.</summary>
    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewExcel(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _attendance.PreviewExcelAsync(stream, file.FileName, ct));
    }

    /// <summary>Nhập file Excel dự phòng (Source = EXCEL_IMPORT). Trả về kết quả như đồng bộ RJ (MSG24).</summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportExcel(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _attendance.ImportExcelAsync(stream, file.FileName, ct));
    }
}
