using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IAttendanceService
{
    /// <summary>UC61: nhận lô lượt quẹt từ agent RJ hoặc từ file Excel dự phòng.</summary>
    Task<Result<IngestResultDto>> IngestAsync(IngestPunchesDto dto, string source, CancellationToken ct);

    /// <summary>Mốc đọc tiếp cho agent.</summary>
    Task<Result<SyncCursorDto>> GetCursorAsync(CancellationToken ct);

    /// <summary>Tình trạng đồng bộ để cảnh báo MSG23.</summary>
    Task<Result<SyncHealthDto>> GetHealthAsync(CancellationToken ct);

    Task<Result<IReadOnlyList<SyncSessionDto>>> GetSessionsAsync(int take, CancellationToken ct);

    Task<Result<IReadOnlyList<UnmappedCodeDto>>> GetUnmappedCodesAsync(CancellationToken ct);

    /// <summary>UC28: Kế toán gán mã chấm công chưa có chủ cho nhân viên.</summary>
    Task<Result> MapCodeAsync(MapAttendanceCodeDto dto, int changedBy, CancellationToken ct);

    /// <summary>UC28 / UC58: bảng công ngày. Truyền scope để giới hạn phạm vi xem theo vai trò (R1).</summary>
    Task<Result<PagedResult<DailyTimesheetDto>>> GetTimesheetsAsync(
        TimesheetQuery query, TimesheetScope scope, CancellationToken ct);

    Task<Result<TimesheetSummaryDto>> GetSummaryAsync(TimesheetQuery query, TimesheetScope scope, CancellationToken ct);

    // ---------- Chấm bù (UC29) ----------

    /// <summary>Kế toán chấm bù cho nhân viên (GBR-25: bắt buộc lý do).</summary>
    Task<Result> CreateAdjustmentAsync(CreateAttendanceAdjustmentDto dto, int changedBy, CancellationToken ct);

    /// <summary>Danh sách chấm bù của Kế toán.</summary>
    Task<Result<PagedResult<AttendanceAdjustmentDto>>> GetAdjustmentsAsync(AdjustmentQuery query, CancellationToken ct);

    // ---------- Import Excel chấm công (UC29) ----------

    /// <summary>Xem trước file Excel dự phòng: parse ra các dòng, chưa ghi DB.</summary>
    Task<Result<AttendanceExcelPreviewDto>> PreviewExcelAsync(Stream stream, string fileName, CancellationToken ct);

    /// <summary>Nhập file Excel dự phòng vào AttendanceLog rồi tính lại bảng công (Source = EXCEL_IMPORT).</summary>
    Task<Result<IngestResultDto>> ImportExcelAsync(Stream stream, string fileName, CancellationToken ct);
}

/// <summary>
/// Giới hạn dữ liệu người gọi được xem (GBR-01). Service không biết role, chỉ nhận phạm vi:
/// Kế toán xem tất cả, tổ trưởng chỉ tổ mình, công nhân chỉ bản thân.
/// </summary>
public sealed record TimesheetScope(int? OnlyTeamId, int? OnlyEmployeeId)
{
    public static readonly TimesheetScope All = new(null, null);
    public static TimesheetScope ForTeam(int teamId) => new(teamId, null);
    public static TimesheetScope ForEmployee(int employeeId) => new(null, employeeId);
}
