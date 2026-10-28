using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Hr;

/// <summary>
/// KH-22 (UC17 import): Admin tải file Excel nhân viên; xem trước 3 loại hợp lệ / lỗi / trùng (MSG24) rồi chỉ nhập các dòng
/// hợp lệ bằng đúng luồng tạo nhân viên của màn Nhân viên (mật khẩu tạm, ghi AuditLog, kiểm tổ trưởng / mã chấm công).
/// </summary>
public sealed class EmployeeImportService : IEmployeeImportService
{
    public const string StatusValid = "VALID";
    public const string StatusError = "ERROR";
    public const string StatusDuplicate = "DUPLICATE";
    private const int MaxRows = 500;

    private readonly IUnitOfWork _uow;
    private readonly IExcelEmployeeParser _parser;
    private readonly IEmployeeService _employees;
    private readonly IValidator<CreateEmployeeDto> _validator;
    private readonly EmployeeScope _scope;

    public EmployeeImportService(IUnitOfWork uow, IExcelEmployeeParser parser, IEmployeeService employees,
        IValidator<CreateEmployeeDto> validator, EmployeeScope scope)
    {
        _uow = uow; _parser = parser; _employees = employees; _validator = validator; _scope = scope;
    }

    public async Task<Result<EmployeeImportPreviewDto>> PreviewAsync(Stream stream, CancellationToken ct)
    {
        var rows = ReadRows(stream, out var failure);
        if (failure is not null) return Result<EmployeeImportPreviewDto>.Failure(ErrorCodes.EmployeeImportInvalid, failure, ResultKind.Validation);
        var classified = await ClassifyAsync(rows!, ct);
        var valid = classified.Count(c => c.Row.Status == StatusValid);
        var errors = classified.Count(c => c.Row.Status == StatusError);
        var dup = classified.Count(c => c.Row.Status == StatusDuplicate);
        return Result<EmployeeImportPreviewDto>.Success(new EmployeeImportPreviewDto(valid, errors, dup,
            string.Format(Messages.EmployeeImportSummary, valid, errors, dup), classified.Select(c => c.Row).ToList()));
    }

    public async Task<Result<EmployeeImportResultDto>> ImportAsync(Stream stream, CancellationToken ct)
    {
        var rows = ReadRows(stream, out var failure);
        if (failure is not null) return Result<EmployeeImportResultDto>.Failure(ErrorCodes.EmployeeImportInvalid, failure, ResultKind.Validation);
        var classified = await ClassifyAsync(rows!, ct);

        var created = new List<EmployeeImportCreatedDto>();
        var failed = classified.Where(c => c.Row.Status != StatusValid).Select(c => c.Row).ToList();
        foreach (var (row, dto) in classified.Where(c => c.Row.Status == StatusValid))
        {
            var result = await _employees.CreateAsync(dto!, ct);
            if (result.IsSuccess) created.Add(new EmployeeImportCreatedDto(result.Data!.Username, dto!.FullName.Trim(), result.Data.TemporaryPassword));
            else failed.Add(row with { Status = StatusError, Message = result.Message });
        }
        return Result<EmployeeImportResultDto>.Success(new EmployeeImportResultDto(created.Count, failed.Count, created,
            failed.OrderBy(f => f.RowNumber).ToList()), string.Format(Messages.EmployeeImportDone, created.Count, failed.Count));
    }

    // ---------- Nội bộ ----------

    private IReadOnlyList<EmployeeExcelRowDto>? ReadRows(Stream stream, out string? failure)
    {
        failure = null;
        try
        {
            var rows = _parser.Parse(stream);
            if (rows.Count == 0) { failure = Messages.EmployeeImportEmpty; return null; }
            if (rows.Count > MaxRows) { failure = string.Format(Messages.EmployeeImportTooMany, MaxRows); return null; }
            return rows;
        }
        catch (InvalidDataException ex)
        {
            failure = ex.Message;
            return null;
        }
    }

    private async Task<List<(EmployeeImportRowDto Row, CreateEmployeeDto? Dto)>> ClassifyAsync(IReadOnlyList<EmployeeExcelRowDto> rows,
        CancellationToken ct)
    {
        var existingCodes = (await _uow.Repository<Employee>().Query().AsNoTracking().Select(e => e.EmployeeCode).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var teams = await _uow.Repository<Team>().Query().AsNoTracking().Where(t => t.Status == RecordStatus.Active)
            .ToDictionaryAsync(t => t.TeamCode.ToUpperInvariant(), t => t.TeamId, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenAttendance = new HashSet<int>();
        var result = new List<(EmployeeImportRowDto, CreateEmployeeDto?)>();

        foreach (var r in rows)
        {
            EmployeeImportRowDto Row(string status, string? message) =>
                new(r.RowNumber, r.Code, r.FullName, r.Role, r.TeamCode, status, message);

            if (r.ParseError is not null) { result.Add((Row(StatusError, r.ParseError), null)); continue; }
            var code = r.Code!.Trim();
            if (existingCodes.Contains(code)) { result.Add((Row(StatusDuplicate, Messages.EmployeeImportExists), null)); continue; }
            if (!seen.Add(code)) { result.Add((Row(StatusDuplicate, Messages.EmployeeImportRepeated), null)); continue; }

            var role = NormalizeRole(r.Role);
            // Quản đốc không nhập được nhân viên vai trò Admin / Kế toán.
            if (role is not null && !_scope.CanSee(Roles.ToRoleCode(role)))
            { result.Add((Row(StatusError, Messages.ManagerCannotAssignRole), null)); continue; }
            int? teamId = null;
            if (!string.IsNullOrWhiteSpace(r.TeamCode))
            {
                if (!teams.TryGetValue(r.TeamCode.Trim().ToUpperInvariant(), out var id))
                { result.Add((Row(StatusError, string.Format(Messages.EmployeeImportTeamUnknown, r.TeamCode)), null)); continue; }
                teamId = id;
            }
            if (r.AttendanceCode is { } att && !seenAttendance.Add(att))
            { result.Add((Row(StatusDuplicate, Messages.EmployeeImportAttendanceRepeated), null)); continue; }

            var dto = new CreateEmployeeDto(code, r.FullName ?? string.Empty, NormalizeGender(r.Gender), r.DateOfBirth, r.Phone?.Trim(),
                role ?? r.Role ?? string.Empty, teamId, NormalizePayType(r.PayType), r.StartDate ?? DateOnly.FromDateTime(DateTime.Today), r.AttendanceCode);
            var check = await _validator.ValidateAsync(dto, ct);
            result.Add(check.IsValid ? (Row(StatusValid, null), dto) : (Row(StatusError, check.Errors[0].ErrorMessage), null));
        }
        return result;
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        var key = role.Replace("_", "").Replace(" ", "").Trim();
        return new[] { Roles.Admin, Roles.Manager, Roles.Accountant, Roles.WarehouseKeeper, Roles.TeamLeader, Roles.TeamDeputy, Roles.QaQc, Roles.Electrician, Roles.Worker }
            .FirstOrDefault(r => string.Equals(r, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeGender(string? gender) => gender?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "m" or "nam" => "M",
        "f" or "nữ" or "nu" => "F",
        var other => other.ToUpperInvariant(),
    };

    private static string NormalizePayType(string? payType)
    {
        var key = (payType ?? string.Empty).Replace("_", "").Replace(" ", "").Trim().ToLowerInvariant();
        return key switch { "piecerate" or "khoán" or "khoan" => "PieceRate", "daily" or "ngày" or "ngay" => "Daily", "monthly" or "tháng" or "thang" => "Monthly", _ => payType ?? string.Empty };
    }
}

/// <summary>UC22: Admin xem AuditLog (lọc theo bảng, hành động, người thực hiện, khoảng ngày, mã bản ghi).</summary>
public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly IUnitOfWork _uow;

    public AuditLogQueryService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<PagedResult<AuditLogRowDto>>> ListAsync(AuditLogQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim();
        var from = q.FromDate?.ToDateTime(TimeOnly.MinValue);
        var to = q.ToDate?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var page = await _uow.Repository<AuditLog>().Query().AsNoTracking()
            .Where(a => string.IsNullOrEmpty(q.TableName) || a.TableName == q.TableName)
            .Where(a => string.IsNullOrEmpty(q.Action) || a.Action == q.Action)
            .Where(a => q.ChangedBy == null || a.ChangedBy == q.ChangedBy)
            .Where(a => q.RecordId == null || a.RecordId == q.RecordId)
            .Where(a => from == null || a.ChangedAt >= from)
            .Where(a => to == null || a.ChangedAt < to)
            .Where(a => string.IsNullOrEmpty(keyword) || (a.Reason != null && a.Reason.Contains(keyword))
                        || (a.ChangedByNavigation != null && a.ChangedByNavigation.FullName.Contains(keyword)))
            .OrderByDescending(a => a.AuditLogId)
            .Select(a => new AuditLogRowDto(a.AuditLogId, a.TableName, a.RecordId, a.Action, a.OldValues, a.NewValues, a.Reason, a.ChangedBy,
                a.ChangedByNavigation == null ? null : a.ChangedByNavigation.FullName, a.ChangedAt))
            .ToPagedAsync(q, ct);
        return Result<PagedResult<AuditLogRowDto>>.Success(page);
    }

    public async Task<Result<IReadOnlyList<string>>> GetTableNamesAsync(CancellationToken ct) =>
        Result<IReadOnlyList<string>>.Success(await _uow.Repository<AuditLog>().Query().AsNoTracking()
            .Select(a => a.TableName).Distinct().OrderBy(n => n).ToListAsync(ct));
}
