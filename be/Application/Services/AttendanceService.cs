using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC61 (đồng bộ Ronald Jack Pro) và UC28 (map mã chấm công, bảng công ngày).
/// Bỏ trùng, bỏ tháng khóa và tính lại bảng công do thủ tục usp_Attendance_IngestBatch lo;
/// service lo kiểm tra đầu vào, phạm vi xem theo vai trò và dựng kết quả trả về.
/// </summary>
public sealed class AttendanceService : IAttendanceService
{
    /// <summary>Số phiên đồng bộ hiện trên màn hình theo dõi.</summary>
    private const int RecentSessionTake = 20;

    private readonly IAttendanceRepository _attendance;
    private readonly ISystemSettingReader _settings;
    private readonly IClock _clock;
    private readonly IExcelAttendanceParser _excelParser;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(IAttendanceRepository attendance, ISystemSettingReader settings, IClock clock,
        IExcelAttendanceParser excelParser, ILogger<AttendanceService> logger)
    {
        _attendance = attendance;
        _settings = settings;
        _clock = clock;
        _excelParser = excelParser;
        _logger = logger;
    }

    public async Task<Result<IngestResultDto>> IngestAsync(IngestPunchesDto dto, string source, CancellationToken ct)
    {
        if (dto.Punches.Count == 0)
            return Result<IngestResultDto>.Failure(ErrorCodes.AttendanceEmptyBatch, Messages.AttendanceEmptyBatch);

        var result = await _attendance.IngestBatchAsync(dto.Punches, source, dto.FileName, ct);

        _logger.LogInformation(
            "Attendance ingest {Source}: session {SessionId}, received {Received}, inserted {Inserted}, " +
            "duplicates {Duplicates}, locked {Locked}, unmapped {Unmapped}",
            source, result.SyncSessionId, result.Received, result.Inserted, result.Duplicates,
            result.SkippedLockedMonth, result.Unmapped);

        return Result<IngestResultDto>.Success(result, BuildIngestMessage(result));
    }

    public async Task<Result<SyncCursorDto>> GetCursorAsync(CancellationToken ct)
    {
        var lastId = await _attendance.GetLastSourceRecordIdAsync(ct);
        var lastAt = await _attendance.GetLastSucceededAtAsync(ct);
        var interval = await _settings.GetIntAsync(SettingKeys.AttendanceSyncMinutes, 10, ct);

        return Result<SyncCursorDto>.Success(new SyncCursorDto(lastId, lastAt, interval));
    }

    public async Task<Result<SyncHealthDto>> GetHealthAsync(CancellationToken ct)
    {
        var lastAt = await _attendance.GetLastSucceededAtAsync(ct);
        var threshold = await _settings.GetIntAsync(SettingKeys.AttendanceAlertMinutes, 30, ct);
        var unmapped = await _attendance.CountUnmappedCodesAsync(ct);

        var minutes = lastAt is null ? (int?)null : (int)Math.Floor((_clock.Now - lastAt.Value).TotalMinutes);
        var overdue = minutes is null || minutes > threshold;

        return Result<SyncHealthDto>.Success(
            new SyncHealthDto(lastAt, minutes, threshold, overdue, unmapped),
            overdue ? Messages.AttendanceSyncOverdue : null);
    }

    public async Task<Result<IReadOnlyList<SyncSessionDto>>> GetSessionsAsync(int take, CancellationToken ct)
    {
        var limit = take is < 1 or > Paging.MaxPageSize ? RecentSessionTake : take;
        var sessions = await _attendance.GetRecentSessionsAsync(limit, ct);
        return Result<IReadOnlyList<SyncSessionDto>>.Success(sessions);
    }

    public async Task<Result<IReadOnlyList<UnmappedCodeDto>>> GetUnmappedCodesAsync(CancellationToken ct)
    {
        var codes = await _attendance.GetUnmappedCodesAsync(ct);
        return Result<IReadOnlyList<UnmappedCodeDto>>.Success(codes);
    }

    public async Task<Result> MapCodeAsync(MapAttendanceCodeDto dto, int changedBy, CancellationToken ct)
    {
        var employee = await _attendance.GetEmployeeAsync(dto.EmployeeId, ct);
        if (employee is null)
            return Result.Failure(ErrorCodes.AttendanceEmployeeNotFound, Messages.AttendanceEmployeeNotFound,
                ResultKind.NotFound);

        if (employee.AttendanceCode is { } existing && existing != dto.AttendanceCode)
            return Result.Failure(ErrorCodes.AttendanceEmployeeHasCode, Messages.AttendanceEmployeeHasCode);

        var holder = await _attendance.GetEmployeeByAttendanceCodeAsync(dto.AttendanceCode, ct);
        if (holder is not null && holder.EmployeeId != dto.EmployeeId)
            return Result.Failure(ErrorCodes.AttendanceCodeAlreadyMapped, Messages.AttendanceCodeAlreadyMapped);

        if (!await _attendance.HasPunchesForCodeAsync(dto.AttendanceCode, ct))
            return Result.Failure(ErrorCodes.AttendanceCodeNotFound, Messages.AttendanceCodeNotFound,
                ResultKind.NotFound);

        await _attendance.MapCodeAsync(dto.AttendanceCode, dto.EmployeeId, changedBy, ct);

        _logger.LogInformation("Mapped attendance code {Code} to employee {EmployeeId} by {ChangedBy}",
            dto.AttendanceCode, dto.EmployeeId, changedBy);

        return Result.Success(Messages.Saved);
    }

    public async Task<Result<PagedResult<DailyTimesheetDto>>> GetTimesheetsAsync(
        TimesheetQuery query, TimesheetScope scope, CancellationToken ct)
    {
        var scoped = Normalize(query, scope);
        var (items, total) = await _attendance.GetTimesheetsAsync(scoped, ct);

        return Result<PagedResult<DailyTimesheetDto>>.Success(
            new PagedResult<DailyTimesheetDto>(items, scoped.Page, scoped.PageSize, total),
            items.Count == 0 ? Messages.NoData : null);
    }

    public async Task<Result<TimesheetSummaryDto>> GetSummaryAsync(
        TimesheetQuery query, TimesheetScope scope, CancellationToken ct)
    {
        var summary = await _attendance.GetTimesheetSummaryAsync(Normalize(query, scope), ct);
        return Result<TimesheetSummaryDto>.Success(summary);
    }

    // ---------- Chấm bù (UC29) ----------

    public async Task<Result> CreateAdjustmentAsync(CreateAttendanceAdjustmentDto dto, int changedBy, CancellationToken ct)
    {
        if (await _attendance.IsPeriodLockedAsync(dto.WorkDate, ct))
            return Result.Failure(ErrorCodes.AttendancePeriodLocked,
                string.Format(Messages.AttendancePeriodLockedMessage, dto.WorkDate.ToString("yyyy-MM")));

        var employee = await _attendance.GetEmployeeStatusAsync(dto.EmployeeId, ct);
        if (employee is null)
            return Result.Failure(ErrorCodes.AttendanceEmployeeNotFound, Messages.AttendanceEmployeeNotFound,
                ResultKind.NotFound);

        if (employee.Status != RecordStatus.Active)
            return Result.Failure(ErrorCodes.AttendanceEmployeeInactive, Messages.AttendanceEmployeeInactive);

        if (await _attendance.HasAdjustmentAsync(dto.EmployeeId, dto.WorkDate, ct))
            return Result.Failure(ErrorCodes.AttendanceAdjustmentExists, Messages.AttendanceAdjustmentExists);

        if (dto.AdjustmentType == AdjustmentType.ApprovedRequest)
        {
            var request = await _attendance.GetAdjustmentRequestAsync(dto.RequestId!.Value, ct);
            if (request is null || request.RequestTypeCode != RequestTypes.AttendanceCorrection)
                return Result.Failure(ErrorCodes.AttendanceRequestInvalid, Messages.AttendanceAdjustmentRequestRequired,
                    ResultKind.NotFound);

            if (request.Status != RequestStatus.Approved)
                return Result.Failure(ErrorCodes.AttendanceRequestInvalid, Messages.AttendanceRequestInvalid);
        }

        await _attendance.ApplyAdjustmentAsync(dto, changedBy, ct);

        _logger.LogInformation("Applied attendance adjustment for employee {EmployeeId} on {WorkDate} by {ChangedBy}",
            dto.EmployeeId, dto.WorkDate, changedBy);

        return Result.Success(Messages.Saved);
    }

    public async Task<Result<PagedResult<AttendanceAdjustmentDto>>> GetAdjustmentsAsync(
        AdjustmentQuery query, CancellationToken ct)
    {
        var scoped = query with
        {
            Page = Paging.NormalizePage(query.Page),
            PageSize = Paging.NormalizePageSize(query.PageSize),
        };

        var (items, total) = await _attendance.GetAdjustmentsAsync(scoped, ct);

        return Result<PagedResult<AttendanceAdjustmentDto>>.Success(
            new PagedResult<AttendanceAdjustmentDto>(items, scoped.Page, scoped.PageSize, total),
            items.Count == 0 ? Messages.NoData : null);
    }

    // ---------- Import Excel chấm công (UC29) ----------

    public Task<Result<AttendanceExcelPreviewDto>> PreviewExcelAsync(Stream stream, string fileName,
        CancellationToken ct)
    {
        if (!TryParseExcel(stream, out var rows, out var errorCode, out var errorMessage))
            return Task.FromResult(Result<AttendanceExcelPreviewDto>.Failure(
                errorCode!, errorMessage!, ResultKind.Validation));

        if (rows!.Count == 0)
            return Task.FromResult(Result<AttendanceExcelPreviewDto>.Failure(
                ErrorCodes.AttendanceExcelEmpty, Messages.AttendanceExcelEmpty));

        var punches = rows.SelectMany(ToPunches).ToList();
        return Task.FromResult(Result<AttendanceExcelPreviewDto>.Success(
            new AttendanceExcelPreviewDto(rows.Count, punches.Count, rows)));
    }

    public async Task<Result<IngestResultDto>> ImportExcelAsync(Stream stream, string fileName,
        CancellationToken ct)
    {
        if (!TryParseExcel(stream, out var rows, out var errorCode, out var errorMessage))
            return Result<IngestResultDto>.Failure(errorCode!, errorMessage!, ResultKind.Validation);

        if (rows!.Count == 0)
            return Result<IngestResultDto>.Failure(ErrorCodes.AttendanceExcelEmpty, Messages.AttendanceExcelEmpty);

        var punches = rows.SelectMany(ToPunches).ToList();
        if (punches.Count == 0)
            return Result<IngestResultDto>.Failure(ErrorCodes.AttendanceExcelEmpty, Messages.AttendanceExcelEmpty);

        var result = await _attendance.IngestBatchAsync(punches, SyncSource.ExcelImport, fileName, ct);

        _logger.LogInformation(
            "Attendance Excel import {FileName}: received {Received}, inserted {Inserted}, duplicates {Duplicates}, " +
            "locked {Locked}, unmapped {Unmapped}",
            fileName, result.Received, result.Inserted, result.Duplicates, result.SkippedLockedMonth, result.Unmapped);

        return Result<IngestResultDto>.Success(result, BuildIngestMessage(result));
    }

    /// <summary>Parse file Excel; file sai định dạng trả về false kèm mã lỗi để Service dựng 400.</summary>
    private bool TryParseExcel(Stream stream, out IReadOnlyList<AttendanceExcelRowDto>? rows,
        out string? errorCode, out string? errorMessage)
    {
        rows = null;
        errorCode = null;
        errorMessage = null;
        try
        {
            rows = _excelParser.Parse(stream);
            return true;
        }
        catch (InvalidDataException ex)
        {
            errorCode = ErrorCodes.AttendanceExcelInvalid;
            errorMessage = ex.Message;
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            _logger.LogWarning(ex, "Attendance Excel parse failed");
            errorCode = ErrorCodes.AttendanceExcelInvalid;
            errorMessage = Messages.AttendanceExcelInvalid;
            return false;
        }
    }

    /// <summary>Một dòng Excel (mã, ngày, vào, ra) → tối đa 2 lượt quẹt: giờ vào và giờ ra.</summary>
    private static IEnumerable<AttendancePunchDto> ToPunches(AttendanceExcelRowDto row)
    {
        if (row.CheckIn is { } checkIn)
            yield return new AttendancePunchDto(null, row.AttendanceCode,
                row.WorkDate.ToDateTime(checkIn));
        if (row.CheckOut is { } checkOut)
            yield return new AttendancePunchDto(null, row.AttendanceCode,
                row.WorkDate.ToDateTime(checkOut));
    }

    /// <summary>Ép bộ lọc về đúng phạm vi vai trò được xem (GBR-01) và chuẩn hóa phân trang.</summary>
    private static TimesheetQuery Normalize(TimesheetQuery query, TimesheetScope scope) => query with
    {
        TeamId = scope.OnlyTeamId ?? query.TeamId,
        EmployeeId = scope.OnlyEmployeeId ?? query.EmployeeId,
        Page = Paging.NormalizePage(query.Page),
        PageSize = Paging.NormalizePageSize(query.PageSize),
    };

    /// <summary>MSG24: {a} dòng hợp lệ, {b} dòng lỗi, {c} dòng trùng.</summary>
    private static string BuildIngestMessage(IngestResultDto r) =>
        $"{r.Inserted} dòng hợp lệ, {r.SkippedLockedMonth} dòng thuộc tháng đã khóa, {r.Duplicates} dòng trùng. " +
        "Chỉ các dòng hợp lệ được nhập.";
}
