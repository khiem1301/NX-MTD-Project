using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

public sealed class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(NxMtdDbContext db) : base(db) { }

    public Task<Employee?> GetFullAsync(int employeeId, CancellationToken ct) =>
        _context.Employees.Include(e => e.Account).Include(e => e.Role).Include(e => e.Team)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        _context.Employees.AnyAsync(e => e.EmployeeCode == code, ct);

    public Task<bool> AttendanceCodeTakenAsync(int attendanceCode, int? exceptEmployeeId, CancellationToken ct) =>
        _context.Employees.AnyAsync(e => e.AttendanceCode == attendanceCode && (exceptEmployeeId == null || e.EmployeeId != exceptEmployeeId), ct);

    public Task<Employee?> FindTeamRoleHolderAsync(int teamId, string roleCode, int? exceptEmployeeId, CancellationToken ct) =>
        _context.Employees.Include(e => e.Account).FirstOrDefaultAsync(e => e.TeamId == teamId && e.Status == RecordStatus.Active
            && e.Role.RoleCode == roleCode && (exceptEmployeeId == null || e.EmployeeId != exceptEmployeeId), ct);

    public async Task<PagedResult<EmployeeListItemDto>> ListAsync(EmployeeQuery q, IReadOnlyCollection<string>? hidden,
        bool includeAccount, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(q.Status) ? RecordStatus.Active : CodeText.ToCode(q.Status);
        var query = _context.Employees.AsNoTracking().Where(e => e.Status == status);
        if (hidden is not null) query = query.Where(e => !hidden.Contains(e.Role.RoleCode));
        if (q.TeamId is not null) query = query.Where(e => e.TeamId == q.TeamId);
        if (!string.IsNullOrWhiteSpace(q.Role))
        {
            var roleCode = Roles.ToRoleCode(q.Role);
            query = query.Where(e => e.Role.RoleCode == roleCode);
        }
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var k = q.Keyword.Trim();
            query = query.Where(e => e.EmployeeCode.Contains(k) || e.FullName.Contains(k));
        }

        var page = await query.OrderBy(e => e.EmployeeCode).Select(e => new
        {
            e.EmployeeId, e.EmployeeCode, e.FullName, e.Role.RoleCode, e.Role.RoleName, e.TeamId,
            TeamName = e.Team == null ? null : e.Team.TeamName, e.PayType, e.CurrentGradeCode, e.AttendanceCode, e.Status,
            AccountStatus = e.Account == null ? null : e.Account.Status, e.StartDate,
        }).ToPagedAsync(q, ct);

        var items = page.Items.Select(e => new EmployeeListItemDto(e.EmployeeId, e.EmployeeCode, e.FullName,
            Roles.FromRoleCode(e.RoleCode), e.RoleName, e.TeamId, e.TeamName, CodeText.ToPascal(e.PayType), e.CurrentGradeCode,
            e.AttendanceCode, CodeText.ToPascal(e.Status),
            includeAccount && e.AccountStatus is not null ? CodeText.ToPascal(e.AccountStatus) : null, e.StartDate)).ToList();
        return new PagedResult<EmployeeListItemDto>(items, page.Page, page.PageSize, page.TotalItems);
    }

    public async Task<EmployeeDetailDto?> GetDetailAsync(int employeeId, bool includeAccount, CancellationToken ct)
    {
        var e = await _context.Employees.AsNoTracking().Where(x => x.EmployeeId == employeeId).Select(x => new
        {
            x.EmployeeId, x.EmployeeCode, x.FullName, x.Gender, x.DateOfBirth, x.Phone, x.AvatarUrl,
            x.Role.RoleCode, x.Role.RoleName, x.TeamId, TeamName = x.Team == null ? null : x.Team.TeamName,
            x.PayType, x.CurrentGradeCode, x.AttendanceCode, x.StartDate, x.ResignDate, x.Status, x.RowVer,
            Account = x.Account == null ? null : new AccountInfoDto(x.Account.AccountId, x.Account.Username,
                x.Account.Status, x.Account.IsTemporaryPassword, x.Account.LockedAt, x.Account.LockReason),
        }).FirstOrDefaultAsync(ct);
        if (e is null) return null;

        var account = includeAccount && e.Account is not null ? e.Account with { Status = CodeText.ToPascal(e.Account.Status) } : null;
        return new EmployeeDetailDto
        {
            EmployeeId = e.EmployeeId, EmployeeCode = e.EmployeeCode, FullName = e.FullName, Gender = e.Gender,
            DateOfBirth = e.DateOfBirth, Phone = e.Phone, AvatarUrl = e.AvatarUrl, Role = Roles.FromRoleCode(e.RoleCode),
            RoleName = e.RoleName, TeamId = e.TeamId, TeamName = e.TeamName, PayType = CodeText.ToPascal(e.PayType),
            CurrentGradeCode = e.CurrentGradeCode, AttendanceCode = e.AttendanceCode, StartDate = e.StartDate,
            ResignDate = e.ResignDate, Status = CodeText.ToPascal(e.Status), RowVer = e.RowVer, Account = account,
        };
    }

    public Task<int> GetRoleIdAsync(string roleCode, CancellationToken ct) =>
        _context.Roles.Where(r => r.RoleCode == roleCode).Select(r => r.RoleId).FirstAsync(ct);

    public Task<List<Employee>> GetActiveByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        _context.Employees.Include(e => e.Account).Include(e => e.Role)
            .Where(e => ids.Contains(e.EmployeeId) && e.Status == RecordStatus.Active).ToListAsync(ct);

    public Task MapAttendanceCodeAsync(int attendanceCode, int employeeId, int changedBy, CancellationToken ct) =>
        _context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.usp_Attendance_MapCode @AttendanceCode = {attendanceCode}, @EmployeeId = {employeeId}, @ChangedBy = {changedBy}", ct);

    public async Task<IReadOnlyList<EmployeeLookupDto>> LookupMembersAsync(int teamId, IReadOnlyCollection<string>? hidden, CancellationToken ct)
    {
        var query = _context.Employees.AsNoTracking().Where(e => e.TeamId == teamId && e.Status == RecordStatus.Active);
        if (hidden is not null) query = query.Where(e => !hidden.Contains(e.Role.RoleCode));
        var rows = await query.OrderBy(e => e.EmployeeCode)
            .Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName, e.Role.RoleCode, e.TeamId, e.PayType }).ToListAsync(ct);
        return rows.Select(e => new EmployeeLookupDto(e.EmployeeId, e.EmployeeCode, e.FullName, Roles.FromRoleCode(e.RoleCode),
            e.TeamId, CodeText.ToPascal(e.PayType))).ToList();
    }
}

public sealed class TeamRepository : GenericRepository<Team>, ITeamRepository
{
    public TeamRepository(NxMtdDbContext db) : base(db) { }

    public async Task<IReadOnlyList<TeamListItemDto>> ListAsync(bool includeDiscontinued, CancellationToken ct)
    {
        var query = _context.Teams.AsNoTracking();
        if (!includeDiscontinued) query = query.Where(t => t.Status == RecordStatus.Active);
        var rows = await query.OrderBy(t => t.TeamCode).Select(t => new
        {
            t.TeamId, t.TeamCode, t.TeamName, t.TeamType, t.Status,
            LeaderName = _context.Employees.Where(e => e.TeamId == t.TeamId && e.Status == RecordStatus.Active
                && e.Role.RoleCode == RoleCodes.TeamLeader).Select(e => e.FullName).FirstOrDefault(),
            DeputyName = _context.Employees.Where(e => e.TeamId == t.TeamId && e.Status == RecordStatus.Active
                && e.Role.RoleCode == RoleCodes.TeamDeputy).Select(e => e.FullName).FirstOrDefault(),
            MemberCount = _context.Employees.Count(e => e.TeamId == t.TeamId && e.Status == RecordStatus.Active),
        }).ToListAsync(ct);
        return rows.Select(t => new TeamListItemDto(t.TeamId, t.TeamCode, t.TeamName, CodeText.ToPascal(t.TeamType),
            CodeText.ToPascal(t.Status), t.LeaderName, t.DeputyName, t.MemberCount)).ToList();
    }

    public async Task<TeamDetailDto?> GetDetailAsync(int teamId, IReadOnlyCollection<string>? hidden, CancellationToken ct)
    {
        var team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId, ct);
        if (team is null) return null;

        var members = _context.Employees.AsNoTracking().Where(e => e.TeamId == teamId && e.Status == RecordStatus.Active);
        if (hidden is not null) members = members.Where(e => !hidden.Contains(e.Role.RoleCode));
        var rows = await members.OrderBy(e => e.EmployeeCode)
            .Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName, e.Role.RoleCode, e.Role.RoleName, e.PayType, e.CurrentGradeCode })
            .ToListAsync(ct);
        var list = rows.Select(e => new TeamMemberDto(e.EmployeeId, e.EmployeeCode, e.FullName, Roles.FromRoleCode(e.RoleCode),
            e.RoleName, CodeText.ToPascal(e.PayType), e.CurrentGradeCode)).ToList();
        return new TeamDetailDto(team.TeamId, team.TeamCode, team.TeamName, CodeText.ToPascal(team.TeamType),
            CodeText.ToPascal(team.Status), list, Array.Empty<AuditLogDto>());
    }

    public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct) =>
        _context.Teams.AnyAsync(t => t.TeamCode == code && (exceptId == null || t.TeamId != exceptId), ct);

    public Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct) =>
        _context.Teams.AnyAsync(t => t.TeamName == name && (exceptId == null || t.TeamId != exceptId), ct);

    public Task<int> CountActiveMembersAsync(int teamId, CancellationToken ct) =>
        _context.Employees.CountAsync(e => e.TeamId == teamId && e.Status == RecordStatus.Active, ct);

    public async Task<IReadOnlyList<TeamLookupDto>> LookupAsync(CancellationToken ct)
    {
        var rows = await _context.Teams.AsNoTracking().Where(t => t.Status == RecordStatus.Active).OrderBy(t => t.TeamCode)
            .Select(t => new { t.TeamId, t.TeamCode, t.TeamName, t.TeamType }).ToListAsync(ct);
        return rows.Select(t => new TeamLookupDto(t.TeamId, t.TeamCode, t.TeamName, CodeText.ToPascal(t.TeamType))).ToList();
    }
}
