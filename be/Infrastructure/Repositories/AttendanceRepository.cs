using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

/// <summary>
/// Chấm công: phần nghiệp vụ nặng đã nằm trong thủ tục của DB nên repository gọi lại thủ tục
/// (Rules mục 6.4), phần đọc danh sách dùng EF với AsNoTracking + projection.
/// </summary>
public sealed class AttendanceRepository : IAttendanceRepository
{
    private const string PunchTableTypeName = "dbo.AttendancePunchTableType";

    private readonly NxMtdDbContext _context;

    public AttendanceRepository(NxMtdDbContext context) => _context = context;

    public async Task<IngestResultDto> IngestBatchAsync(IReadOnlyList<AttendancePunchDto> punches, string source,
        string? fileName, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) await _context.Database.OpenConnectionAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_Attendance_IngestBatch";
            command.CommandType = CommandType.StoredProcedure;

            var rows = new SqlParameter("@Rows", SqlDbType.Structured)
            {
                TypeName = PunchTableTypeName,
                Value = BuildPunchTable(punches),
            };
            command.Parameters.Add(rows);
            command.Parameters.Add(new SqlParameter("@Source", SqlDbType.VarChar, 15) { Value = source });
            command.Parameters.Add(new SqlParameter("@FileName", SqlDbType.NVarChar, 300)
            {
                Value = (object?)fileName ?? DBNull.Value,
            });

            var current = _context.Database.CurrentTransaction;
            if (current is not null) command.Transaction = current.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return new IngestResultDto(0, punches.Count, 0, 0, 0, 0);

            return new IngestResultDto(
                reader.GetInt32(reader.GetOrdinal("SyncSessionId")),
                reader.GetInt32(reader.GetOrdinal("Received")),
                reader.GetInt32(reader.GetOrdinal("Inserted")),
                reader.GetInt32(reader.GetOrdinal("SkippedLockedMonth")),
                reader.GetInt32(reader.GetOrdinal("Duplicates")),
                reader.GetInt32(reader.GetOrdinal("Unmapped")));
        }
        finally
        {
            if (wasClosed) await _context.Database.CloseConnectionAsync();
        }
    }

    public Task MapCodeAsync(int attendanceCode, int employeeId, int changedBy, CancellationToken ct) =>
        _context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.usp_Attendance_MapCode @AttendanceCode = {attendanceCode}, @EmployeeId = {employeeId}, @ChangedBy = {changedBy}",
            ct);

    public Task<int?> GetLastSourceRecordIdAsync(CancellationToken ct) =>
        _context.AttendanceSyncSessions.AsNoTracking()
            .Where(s => s.Status == SyncStatus.Succeeded && s.LastSourceRecordId != null)
            .MaxAsync(s => s.LastSourceRecordId, ct);

    public Task<DateTime?> GetLastSucceededAtAsync(CancellationToken ct) =>
        _context.AttendanceSyncSessions.AsNoTracking()
            .Where(s => s.Status == SyncStatus.Succeeded && s.FinishedAt != null)
            .MaxAsync(s => s.FinishedAt, ct);

    public async Task<IReadOnlyList<SyncSessionDto>> GetRecentSessionsAsync(int take, CancellationToken ct) =>
        await _context.AttendanceSyncSessions.AsNoTracking()
            .OrderByDescending(s => s.StartedAt)
            .Take(take)
            .Select(s => new SyncSessionDto(
                s.SyncSessionId, CodeText.ToPascal(s.Source), s.StartedAt, s.FinishedAt, s.LastSourceRecordId,
                s.RecordsReceived, s.RecordsInserted, CodeText.ToPascal(s.Status), s.ErrorMessage, s.FileName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UnmappedCodeDto>> GetUnmappedCodesAsync(CancellationToken ct) =>
        await UnmappedQuery()
            .Select(g => new UnmappedCodeDto(g.Key, g.Count(), g.Min(l => l.PunchTime), g.Max(l => l.PunchTime)))
            .OrderByDescending(x => x.LastPunchAt)
            .ToListAsync(ct);

    public Task<int> CountUnmappedCodesAsync(CancellationToken ct) => UnmappedQuery().CountAsync(ct);

    public Task<bool> HasPunchesForCodeAsync(int attendanceCode, CancellationToken ct) =>
        _context.AttendanceLogs.AsNoTracking().AnyAsync(l => l.AttendanceCode == attendanceCode, ct);

    // ---------- Chấm bù (UC29) ----------

    public Task<bool> IsPeriodLockedAsync(DateOnly workDate, CancellationToken ct) =>
        _context.AccountingPeriods.AsNoTracking()
            .AnyAsync(p => p.Status == RecordStatus.Locked && workDate >= p.StartDate && workDate <= p.EndDate, ct);

    public Task<bool> HasAdjustmentAsync(int employeeId, DateOnly workDate, CancellationToken ct) =>
        _context.AttendanceAdjustments.AsNoTracking()
            .AnyAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate, ct);

    public Task<Employee?> GetEmployeeStatusAsync(int employeeId, CancellationToken ct) =>
        _context.Employees.AsNoTracking()
            .Select(e => new Employee { EmployeeId = e.EmployeeId, Status = e.Status })
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);

    public Task<Request?> GetAdjustmentRequestAsync(int requestId, CancellationToken ct) =>
        _context.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.RequestId == requestId, ct);

    public async Task<int> ApplyAdjustmentAsync(CreateAttendanceAdjustmentDto dto, int changedBy, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) await _context.Database.OpenConnectionAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_Attendance_ApplyAdjustment";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add(new SqlParameter("@EmployeeId", SqlDbType.Int) { Value = dto.EmployeeId });
            command.Parameters.Add(new SqlParameter("@WorkDate", SqlDbType.Date) { Value = dto.WorkDate.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@AdjustmentType", SqlDbType.VarChar, 20) { Value = dto.AdjustmentType });
            command.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar, 500) { Value = dto.Reason });
            command.Parameters.Add(new SqlParameter("@RequestId", SqlDbType.Int)
            {
                Value = (object?)dto.RequestId ?? DBNull.Value,
            });
            command.Parameters.Add(new SqlParameter("@CheckIn", SqlDbType.Time)
            {
                Value = (object?)dto.CheckIn ?? DBNull.Value,
            });
            command.Parameters.Add(new SqlParameter("@CheckOut", SqlDbType.Time)
            {
                Value = (object?)dto.CheckOut ?? DBNull.Value,
            });
            command.Parameters.Add(new SqlParameter("@ChangedBy", SqlDbType.Int) { Value = changedBy });

            var current = _context.Database.CurrentTransaction;
            if (current is not null) command.Transaction = current.GetDbTransaction();

            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }
        finally
        {
            if (wasClosed) await _context.Database.CloseConnectionAsync();
        }
    }

    public async Task<(IReadOnlyList<AttendanceAdjustmentDto> Items, int TotalItems)> GetAdjustmentsAsync(
        AdjustmentQuery query, CancellationToken ct)
    {
        var filtered = _context.AttendanceAdjustments.AsNoTracking()
            .Where(a => a.WorkDate >= query.FromDate && a.WorkDate <= query.ToDate);

        if (query.EmployeeId is { } employeeId) filtered = filtered.Where(a => a.EmployeeId == employeeId);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            filtered = filtered.Where(a =>
                a.Employee.FullName.Contains(keyword) || a.Employee.EmployeeCode.Contains(keyword));
        }

        var total = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderByDescending(a => a.WorkDate).ThenBy(a => a.Employee.EmployeeCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AttendanceAdjustmentDto(
                a.AttendanceAdjustmentId, a.EmployeeId, a.Employee.EmployeeCode, a.Employee.FullName,
                a.WorkDate, a.AdjustmentType, a.Reason, a.RequestId, a.CheckIn, a.CheckOut, a.RecordedAt))
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Employee?> GetEmployeeAsync(int employeeId, CancellationToken ct) =>
        _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);

    public Task<Employee?> GetEmployeeByAttendanceCodeAsync(int attendanceCode, CancellationToken ct) =>
        _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.AttendanceCode == attendanceCode, ct);

    public async Task<(IReadOnlyList<DailyTimesheetDto> Items, int TotalItems)> GetTimesheetsAsync(
        TimesheetQuery query, CancellationToken ct)
    {
        var filtered = ApplyFilter(query);
        var total = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderByDescending(t => t.WorkDate).ThenBy(t => t.Employee.EmployeeCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new DailyTimesheetDto(
                t.DailyTimesheetId, t.EmployeeId, t.Employee.EmployeeCode, t.Employee.FullName,
                t.Employee.Team == null ? null : t.Employee.Team.TeamName,
                t.WorkDate, t.CheckIn, t.CheckOut, t.WorkedHours, t.WorkdayValue, t.OvertimeHours,
                t.IsMissingPunch, t.HasExtraPunch, t.IsSunday ?? false,
                t.Source, t.Note, t.UpdatedAt))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<TimesheetSummaryDto> GetTimesheetSummaryAsync(TimesheetQuery query, CancellationToken ct)
    {
        var filtered = ApplyFilter(query);
        var summary = await filtered
            .GroupBy(_ => 1)
            .Select(g => new TimesheetSummaryDto(
                g.Select(t => t.EmployeeId).Distinct().Count(),
                g.Sum(t => t.WorkdayValue),
                g.Sum(t => t.OvertimeHours),
                g.Count(t => t.IsMissingPunch)))
            .FirstOrDefaultAsync(ct);

        return summary ?? new TimesheetSummaryDto(0, 0, 0, 0);
    }

    private IQueryable<IGrouping<int, AttendanceLog>> UnmappedQuery() =>
        _context.AttendanceLogs.AsNoTracking()
            .Where(l => l.EmployeeId == null)
            .GroupBy(l => l.AttendanceCode);

    private IQueryable<DailyTimesheet> ApplyFilter(TimesheetQuery query)
    {
        var q = _context.DailyTimesheets.AsNoTracking()
            .Where(t => t.WorkDate >= query.FromDate && t.WorkDate <= query.ToDate);

        if (query.TeamId is { } teamId) q = q.Where(t => t.Employee.TeamId == teamId);
        if (query.EmployeeId is { } employeeId) q = q.Where(t => t.EmployeeId == employeeId);
        if (query.OnlyMissingPunch == true) q = q.Where(t => t.IsMissingPunch);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(t => t.Employee.FullName.Contains(keyword) || t.Employee.EmployeeCode.Contains(keyword));
        }

        return q;
    }

    private static DataTable BuildPunchTable(IReadOnlyList<AttendancePunchDto> punches)
    {
        var table = new DataTable();
        table.Columns.Add("SourceRecordId", typeof(int));
        table.Columns.Add("AttendanceCode", typeof(int));
        table.Columns.Add("PunchTime", typeof(DateTime));

        foreach (var punch in punches)
            table.Rows.Add((object?)punch.SourceRecordId ?? DBNull.Value, punch.AttendanceCode, punch.PunchTime);

        return table;
    }
}
