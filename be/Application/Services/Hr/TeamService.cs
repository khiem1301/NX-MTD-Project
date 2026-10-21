using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Hr;

/// <summary>UC18: quản lý tổ (Admin + Quản đốc). Chuyển tổ và bổ nhiệm tổ trưởng / tổ phó.</summary>
public sealed class TeamService : ITeamService
{
    private readonly ITeamRepository _teams;
    private readonly IEmployeeRepository _employees;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly EmployeeScope _scope;

    public TeamService(ITeamRepository teams, IEmployeeRepository employees, IAuditLogService audit, IUnitOfWork uow,
        IClock clock, EmployeeScope scope)
    {
        _teams = teams; _employees = employees; _audit = audit; _uow = uow; _clock = clock; _scope = scope;
    }

    public async Task<Result<IReadOnlyList<TeamListItemDto>>> ListAsync(bool includeDiscontinued, CancellationToken ct) =>
        Result<IReadOnlyList<TeamListItemDto>>.Success(await _teams.ListAsync(includeDiscontinued, ct));

    public async Task<Result<TeamDetailDto>> GetAsync(int teamId, CancellationToken ct)
    {
        var detail = await _teams.GetDetailAsync(teamId, _scope.HiddenRoleCodes, ct);
        if (detail is null) return Fail.NotFound<TeamDetailDto>();
        var history = await _audit.GetHistoryAsync(nameof(Team), teamId, ct);
        return Result<TeamDetailDto>.Success(detail with { History = history });
    }

    public async Task<Result<int>> CreateAsync(SaveTeamDto dto, CancellationToken ct)
    {
        var duplicate = await CheckDuplicateAsync(dto, null, ct);
        if (duplicate is not null) return Fail.As<int>(duplicate);
        var team = new Team
        {
            TeamCode = dto.TeamCode.Trim().ToUpperInvariant(), TeamName = dto.TeamName.Trim(),
            TeamType = CodeText.ToCode(dto.TeamType), Status = RecordStatus.Active, CreatedAt = _clock.Now,
        };
        return await _uow.InTransactionAsync(async () =>   // D17: log cần TeamId
        {
            await _teams.AddAsync(team);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(Team), team.TeamId, AuditActions.Insert, null, Snapshot(team)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(team.TeamId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int teamId, SaveTeamDto dto, CancellationToken ct)
    {
        var team = await _teams.GetByIdAsync(teamId);
        if (team is null) return Fail.NotFound();
        var duplicate = await CheckDuplicateAsync(dto, teamId, ct);
        if (duplicate is not null) return duplicate;

        var newType = CodeText.ToCode(dto.TeamType);
        if (newType != team.TeamType)
        {
            var members = await _teams.CountActiveMembersAsync(teamId, ct);
            if (members > 0)
                return Result.Failure(ErrorCodes.TeamHasActiveMembers, string.Format(Messages.TeamHasActiveMembers, members));
        }
        var before = Snapshot(team);
        team.TeamCode = dto.TeamCode.Trim().ToUpperInvariant();
        team.TeamName = dto.TeamName.Trim();
        team.TeamType = newType;
        _audit.Add(new AuditEntry(nameof(Team), teamId, AuditActions.Update, before, Snapshot(team)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> DiscontinueAsync(int teamId, CancellationToken ct)
    {
        var team = await _teams.GetByIdAsync(teamId);
        if (team is null) return Fail.NotFound();
        if (team.Status != RecordStatus.Active) return Result.Failure(ErrorCodes.TeamNotActive, Messages.TeamNotActive);
        var members = await _teams.CountActiveMembersAsync(teamId, ct);
        if (members > 0) return Result.Failure(ErrorCodes.TeamHasActiveMembers, string.Format(Messages.TeamHasActiveMembers, members));

        team.Status = RecordStatus.Discontinued;
        _audit.Add(new AuditEntry(nameof(Team), teamId, AuditActions.Update, new { Status = RecordStatus.Active }, new { team.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    /// <summary>Gọi lại khi tổ đã ACTIVE không lỗi, không đổi gì.</summary>
    public async Task<Result> ReactivateAsync(int teamId, CancellationToken ct)
    {
        var team = await _teams.GetByIdAsync(teamId);
        if (team is null) return Fail.NotFound();
        if (team.Status == RecordStatus.Active) return Result.Success(Messages.Saved);

        team.Status = RecordStatus.Active;
        _audit.Add(new AuditEntry(nameof(Team), teamId, AuditActions.Update, new { Status = RecordStatus.Discontinued }, new { team.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    /// <summary>D32: ngày hiệu lực ≤ hôm nay; TeamId đổi ngay, ngày ghi trong AuditLog.</summary>
    public async Task<Result> TransferInAsync(int targetTeamId, TransferMembersDto dto, CancellationToken ct)
    {
        if (dto.EffectiveDate > _clock.Today)
            return Result.Failure(ErrorCodes.TeamTransferFutureDate, Messages.TeamTransferFutureDate);
        var team = await _teams.GetByIdAsync(targetTeamId);
        if (team is null) return Fail.NotFound();
        if (team.Status != RecordStatus.Active) return Result.Failure(ErrorCodes.TeamNotActive, Messages.TeamNotActive);

        var people = await _employees.GetActiveByIdsAsync(dto.EmployeeIds.ToList(), ct);
        if (people.Count != dto.EmployeeIds.Count || people.Any(p => !_scope.CanSee(p.Role.RoleCode))) return Fail.NotFound();
        var leader = people.FirstOrDefault(p => p.Role.RoleCode is RoleCodes.TeamLeader or RoleCodes.TeamDeputy);
        if (leader is not null)
            return Result.Failure(ErrorCodes.TeamTransferLeader, string.Format(Messages.TeamTransferLeader, leader.FullName));

        var moving = people.Where(p => p.TeamId != targetTeamId).ToList();
        var reason = $"Chuyển tổ, hiệu lực {dto.EffectiveDate:dd/MM/yyyy}. {dto.Reason?.Trim()}".Trim();
        foreach (var person in moving)
        {
            var oldTeam = person.TeamId;
            person.TeamId = targetTeamId;
            person.UpdatedAt = _clock.Now;
            if (person.Account is not null) person.Account.SecurityStamp = Guid.NewGuid();
            _audit.Add(new AuditEntry(nameof(Employee), person.EmployeeId, AuditActions.Update,
                new { TeamId = oldTeam }, new { TeamId = targetTeamId }, reason));
        }
        await _uow.SaveChangesAsync();
        return Result.Success(string.Format(Messages.TeamTransferred, moving.Count));
    }

    /// <summary>D31, D33: người đang giữ chức chuyển về Công nhân (giữ kiểu lương); hạ trước, lưu, rồi mới nâng để trigger không thấy 2 người.</summary>
    public async Task<Result> AppointAsync(int teamId, AppointDto dto, CancellationToken ct)
    {
        var team = await _teams.GetByIdAsync(teamId);
        if (team is null || team.Status != RecordStatus.Active || team.TeamType == TeamTypes.Office)
            return Result.Failure(ErrorCodes.TeamLeaderInvalidTeam, Messages.TeamLeaderInvalidTeam);
        var person = await _employees.GetFullAsync(dto.EmployeeId, ct);
        if (person is null || person.TeamId != teamId || person.Status != RecordStatus.Active || !_scope.CanSee(person.Role.RoleCode))
            return Result.Failure(ErrorCodes.TeamMemberNotInTeam, Messages.TeamMemberNotInTeam);
        if (person.Role.RoleCode is not (RoleCodes.Worker or RoleCodes.TeamLeader or RoleCodes.TeamDeputy))
            return Result.Failure(ErrorCodes.TeamMemberNotInTeam, Messages.TeamAppointInvalidRole);

        var roleCode = dto.Position == TeamPositions.Leader ? RoleCodes.TeamLeader : RoleCodes.TeamDeputy;
        if (person.Role.RoleCode == roleCode) return Result.Success(Messages.TeamAppointed);

        var holder = await _employees.FindTeamRoleHolderAsync(teamId, roleCode, person.EmployeeId, ct);
        var workerRoleId = await _employees.GetRoleIdAsync(RoleCodes.Worker, ct);
        var newRoleId = await _employees.GetRoleIdAsync(roleCode, ct);
        var label = dto.Position == TeamPositions.Leader ? "tổ trưởng" : "tổ phó";

        return await _uow.InTransactionAsync(async () =>
        {
            if (holder is not null)
            {
                ChangeRole(holder, workerRoleId, roleCode, RoleCodes.Worker, "Thôi giữ chức do bổ nhiệm người khác");
                await _uow.SaveChangesAsync();
            }
            ChangeRole(person, newRoleId, person.Role.RoleCode, roleCode, $"Bổ nhiệm {label} tổ {team.TeamName}");
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.TeamAppointed);
        });
    }

    private void ChangeRole(Employee e, int newRoleId, string oldCode, string newCode, string reason)
    {
        e.RoleId = newRoleId;
        e.UpdatedAt = _clock.Now;
        if (e.Account is not null) e.Account.SecurityStamp = Guid.NewGuid();
        _audit.Add(new AuditEntry(nameof(Employee), e.EmployeeId, AuditActions.Update,
            new { RoleCode = oldCode }, new { RoleCode = newCode }, reason));
    }

    private async Task<Result?> CheckDuplicateAsync(SaveTeamDto dto, int? exceptId, CancellationToken ct)
    {
        if (await _teams.CodeExistsAsync(dto.TeamCode.Trim().ToUpperInvariant(), exceptId, ct))
            return Result.Failure(ErrorCodes.TeamCodeDuplicate, Messages.TeamCodeDuplicate);
        if (await _teams.NameExistsAsync(dto.TeamName.Trim(), exceptId, ct))
            return Result.Failure(ErrorCodes.TeamNameDuplicate, Messages.TeamNameDuplicate);
        return null;
    }

    private static object Snapshot(Team t) => new { t.TeamCode, t.TeamName, t.TeamType, t.Status };
}
