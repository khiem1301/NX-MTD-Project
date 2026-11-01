using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Truy cập dữ liệu chấm công. Các thủ tục đã có sẵn trong DB (usp_Attendance_*) được gọi lại,
/// không viết lại bằng LINQ (Rules mục 6.4).
/// </summary>
public interface IAttendanceRepository
{
    /// <summary>Gọi usp_Attendance_IngestBatch: bỏ trùng, bỏ tháng khóa, map nhân viên, tính lại bảng công.</summary>
    Task<IngestResultDto> IngestBatchAsync(IReadOnlyList<AttendancePunchDto> punches, string source,
        string? fileName, CancellationToken ct);

    /// <summary>Gọi usp_Attendance_MapCode: gán mã cho nhân viên, nhận lượt cũ, tính lại công.</summary>
    Task MapCodeAsync(int attendanceCode, int employeeId, int changedBy, CancellationToken ct);

    /// <summary>CheckInOut.ID lớn nhất đã đọc được, lấy từ các phiên SUCCEEDED.</summary>
    Task<int?> GetLastSourceRecordIdAsync(CancellationToken ct);

    Task<DateTime?> GetLastSucceededAtAsync(CancellationToken ct);

    Task<IReadOnlyList<SyncSessionDto>> GetRecentSessionsAsync(int take, CancellationToken ct);

    Task<IReadOnlyList<UnmappedCodeDto>> GetUnmappedCodesAsync(CancellationToken ct);

    Task<int> CountUnmappedCodesAsync(CancellationToken ct);

    /// <summary>Có lượt quẹt nào mang mã này chưa (để chặn map mã không tồn tại).</summary>
    Task<bool> HasPunchesForCodeAsync(int attendanceCode, CancellationToken ct);

    Task<Employee?> GetEmployeeAsync(int employeeId, CancellationToken ct);

    /// <summary>Nhân viên đang giữ mã chấm công này, nếu có.</summary>
    Task<Employee?> GetEmployeeByAttendanceCodeAsync(int attendanceCode, CancellationToken ct);

    Task<(IReadOnlyList<DailyTimesheetDto> Items, int TotalItems)> GetTimesheetsAsync(
        TimesheetQuery query, CancellationToken ct);

    Task<TimesheetSummaryDto> GetTimesheetSummaryAsync(TimesheetQuery query, CancellationToken ct);

    // ---------- Chấm bù (UC29) ----------

    /// <summary>Ngày công có thuộc tháng đã khóa không (fn_IsPeriodLocked).</summary>
    Task<bool> IsPeriodLockedAsync(DateOnly workDate, CancellationToken ct);

    /// <summary>Nhân viên đã có chấm bù cho ngày này chưa (UQ_AA EmployeeId+WorkDate).</summary>
    Task<bool> HasAdjustmentAsync(int employeeId, DateOnly workDate, CancellationToken ct);

    /// <summary>Nhân viên (kèm trạng thái) để kiểm tra còn làm việc trước khi chấm bù.</summary>
    Task<Employee?> GetEmployeeStatusAsync(int employeeId, CancellationToken ct);

    /// <summary>Đơn xin chấm công bù: bảng Request không lưu nhân viên được bù (chỉ lưu người tạo
    /// là tổ trưởng), nên chỉ lấy theo id để service kiểm tra loại đơn và trạng thái đã duyệt.</summary>
    Task<Request?> GetAdjustmentRequestAsync(int requestId, CancellationToken ct);

    /// <summary>Gọi usp_Attendance_ApplyAdjustment rồi trả id vừa tạo.</summary>
    Task<int> ApplyAdjustmentAsync(CreateAttendanceAdjustmentDto dto, int changedBy, CancellationToken ct);

    Task<(IReadOnlyList<AttendanceAdjustmentDto> Items, int TotalItems)> GetAdjustmentsAsync(
        AdjustmentQuery query, CancellationToken ct);
}
