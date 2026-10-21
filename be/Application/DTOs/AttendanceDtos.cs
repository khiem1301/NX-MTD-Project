namespace NX_MTD.Application.DTOs;

// ---------- Ingest: agent RJ gửi lô lượt quẹt lên (UC61) ----------

/// <summary>Một lượt quẹt đọc từ CheckInOut của Ronald Jack Pro.</summary>
public sealed record AttendancePunchDto(int? SourceRecordId, int AttendanceCode, DateTime PunchTime);

/// <summary>Lô lượt quẹt agent gửi lên; FileName chỉ dùng khi nhập từ file Excel dự phòng.</summary>
public sealed record IngestPunchesDto(IReadOnlyList<AttendancePunchDto> Punches, string? FileName);

/// <summary>Tổng kết một lần nhận lô (dùng cho MSG24).</summary>
public sealed record IngestResultDto(
    int SyncSessionId, int Received, int Inserted, int SkippedLockedMonth, int Duplicates, int Unmapped);

/// <summary>Mốc để agent biết đọc tiếp từ CheckInOut.ID nào.</summary>
public sealed record SyncCursorDto(int? LastSourceRecordId, DateTime? LastSucceededAt, int SyncIntervalMinutes);

// ---------- Theo dõi đồng bộ (UC28) ----------

public sealed record SyncSessionDto(
    int SyncSessionId, string Source, DateTime StartedAt, DateTime? FinishedAt,
    int? LastSourceRecordId, int RecordsReceived, int RecordsInserted,
    string Status, string? ErrorMessage, string? FileName);

/// <summary>Tình trạng đồng bộ hiện tại, để hiện cảnh báo MSG23 khi quá hạn.</summary>
public sealed record SyncHealthDto(
    DateTime? LastSucceededAt, int? MinutesSinceLastSuccess, int AlertThresholdMinutes,
    bool IsOverdue, int UnmappedCodeCount);

// ---------- Map mã chấm công (UC28) ----------

/// <summary>Mã chấm công có lượt quẹt nhưng chưa gắn với nhân viên nào.</summary>
public sealed record UnmappedCodeDto(int AttendanceCode, int PunchCount, DateTime FirstPunchAt, DateTime LastPunchAt);

public sealed record MapAttendanceCodeDto(int AttendanceCode, int EmployeeId);

// ---------- Bảng công ngày (UC28, UC58) ----------

public sealed record DailyTimesheetDto(
    int DailyTimesheetId, int EmployeeId, string EmployeeCode, string FullName, string? TeamName,
    DateOnly WorkDate, DateTime? CheckIn, DateTime? CheckOut,
    decimal WorkedHours, decimal WorkdayValue, decimal OvertimeHours,
    bool IsMissingPunch, bool HasExtraPunch, bool IsSunday,
    string Source, string? Note, DateTime UpdatedAt);

/// <summary>Bộ lọc bảng công: khoảng ngày bắt buộc, lọc thêm theo tổ / nhân viên / chỉ dòng thiếu giờ.</summary>
public sealed record TimesheetQuery(
    DateOnly FromDate, DateOnly ToDate, int? TeamId, int? EmployeeId, bool? OnlyMissingPunch,
    string? Keyword, int Page = 1, int PageSize = 20);

/// <summary>Tổng hợp cho thẻ số liệu đầu màn hình bảng công.</summary>
public sealed record TimesheetSummaryDto(
    int EmployeeCount, decimal TotalWorkdays, decimal TotalOvertimeHours, int MissingPunchCount);

// ---------- Chấm bù (UC29) ----------

/// <summary>Kế toán chấm bù cho một nhân viên trong một ngày (GBR-25: bắt buộc lý do).</summary>
public sealed record CreateAttendanceAdjustmentDto(
    int EmployeeId, DateOnly WorkDate, string AdjustmentType, string Reason,
    int? RequestId, TimeOnly? CheckIn, TimeOnly? CheckOut);

/// <summary>Một dòng chấm bù để hiển thị trong danh sách.</summary>
public sealed record AttendanceAdjustmentDto(
    int AttendanceAdjustmentId, int EmployeeId, string EmployeeCode, string FullName,
    DateOnly WorkDate, string AdjustmentType, string Reason, int? RequestId,
    TimeOnly? CheckIn, TimeOnly? CheckOut, DateTime RecordedAt);

/// <summary>Bộ lọc danh sách chấm bù của Kế toán.</summary>
public sealed record AdjustmentQuery(
    DateOnly FromDate, DateOnly ToDate, int? EmployeeId, string? Keyword, int Page = 1, int PageSize = 20);

// ---------- Import Excel chấm công (UC29) ----------

/// <summary>Một dòng đọc từ file Excel dự phòng (cột: mã chấm công, ngày, giờ vào, giờ ra).</summary>
public sealed record AttendanceExcelRowDto(int AttendanceCode, DateOnly WorkDate, TimeOnly? CheckIn, TimeOnly? CheckOut);

/// <summary>Kết quả xem trước file Excel trước khi nhập (chưa ghi DB).</summary>
public sealed record AttendanceExcelPreviewDto(int RowCount, int PunchCount, IReadOnlyList<AttendanceExcelRowDto> Rows);
