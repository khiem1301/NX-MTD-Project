using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services.Hr;

/// <summary>Tra cứu tổ / thành viên cho luồng khác (chấm công, công đoạn).</summary>
public sealed class HrLookupService : IHrLookupService
{
    private readonly ITeamRepository _teams;
    private readonly IEmployeeRepository _employees;
    private readonly ICurrentUser _user;

    public HrLookupService(ITeamRepository teams, IEmployeeRepository employees, ICurrentUser user)
    {
        _teams = teams; _employees = employees; _user = user;
    }

    public async Task<Result<IReadOnlyList<TeamLookupDto>>> TeamsAsync(CancellationToken ct) =>
        Result<IReadOnlyList<TeamLookupDto>>.Success(await _teams.LookupAsync(ct));

    /// <summary>Tổ trưởng / tổ phó chỉ tra tổ mình (khác → 404). Admin và Kế toán thấy mọi vai trò; còn lại không thấy Admin / Quản đốc / Kế toán.</summary>
    public async Task<Result<IReadOnlyList<EmployeeLookupDto>>> MembersAsync(int teamId, CancellationToken ct)
    {
        if (Roles.IsTeamRole(_user.Role) && _user.TeamId != teamId) return Fail.NotFound<IReadOnlyList<EmployeeLookupDto>>();
        var seesAll = _user.Role is Roles.Admin or Roles.Accountant;
        IReadOnlyCollection<string>? hidden = seesAll ? null : RoleCodes.HiddenFromManager.ToArray();
        return Result<IReadOnlyList<EmployeeLookupDto>>.Success(await _employees.LookupMembersAsync(teamId, hidden, ct));
    }
}
