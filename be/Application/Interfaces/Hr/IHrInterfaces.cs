using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface ITemporaryPasswordGenerator
{
    /// <summary>D29: 6 chữ số ngẫu nhiên an toàn.</summary>
    string Generate();
}

public interface IEmployeeRepository : IGenericRepository<Employee>
{
    /// <summary>Tracked, kèm Account, Role, Team.</summary>
    Task<Employee?> GetFullAsync(int employeeId, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);

    /// <summary>Người khác (≠ exceptEmployeeId) đang giữ mã chấm công này.</summary>
    Task<bool> AttendanceCodeTakenAsync(int attendanceCode, int? exceptEmployeeId, CancellationToken ct);

    /// <summary>Người ACTIVE đang giữ vai trò này trong tổ (≠ exceptEmployeeId); null nếu chưa có.</summary>
    Task<Employee?> FindTeamRoleHolderAsync(int teamId, string roleCode, int? exceptEmployeeId, CancellationToken ct);
    Task<PagedResult<EmployeeListItemDto>> ListAsync(EmployeeQuery q, IReadOnlyCollection<string>? hiddenRoleCodes,
        bool includeAccount, CancellationToken ct);
    Task<EmployeeDetailDto?> GetDetailAsync(int employeeId, bool includeAccount, CancellationToken ct);
    Task<int> GetRoleIdAsync(string roleCode, CancellationToken ct);
    Task<List<Employee>> GetActiveByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>D30: gọi dbo.usp_Attendance_MapCode trong transaction hiện tại (thủ tục tự ghi AuditLog).</summary>
    Task MapAttendanceCodeAsync(int attendanceCode, int employeeId, int changedBy, CancellationToken ct);
    Task<IReadOnlyList<EmployeeLookupDto>> LookupMembersAsync(int teamId, IReadOnlyCollection<string>? hiddenRoleCodes, CancellationToken ct);
}

public interface ITeamRepository : IGenericRepository<Team>
{
    Task<IReadOnlyList<TeamListItemDto>> ListAsync(bool includeDiscontinued, CancellationToken ct);
    Task<TeamDetailDto?> GetDetailAsync(int teamId, IReadOnlyCollection<string>? hiddenRoleCodes, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct);
    Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct);
    Task<int> CountActiveMembersAsync(int teamId, CancellationToken ct);
    Task<IReadOnlyList<TeamLookupDto>> LookupAsync(CancellationToken ct);
}

public interface IProfileService
{
    Task<Result<ProfileDto>> GetAsync(CancellationToken ct);
    Task<Result<LoginResultDto>> ChangePasswordAsync(ChangePasswordDto dto, CancellationToken ct);
}

public interface IEmployeeService
{
    Task<Result<PagedResult<EmployeeListItemDto>>> ListAsync(EmployeeQuery query, CancellationToken ct);
    Task<Result<EmployeeDetailDto>> GetAsync(int employeeId, CancellationToken ct);
    Task<Result<CreatedEmployeeDto>> CreateAsync(CreateEmployeeDto dto, CancellationToken ct);
    Task<Result> AdminUpdateAsync(int employeeId, AdminUpdateEmployeeDto dto, CancellationToken ct);
    Task<Result> ManagerUpdateAsync(int employeeId, ManagerUpdateEmployeeDto dto, CancellationToken ct);
    Task<Result> ResignAsync(int employeeId, ResignEmployeeDto dto, CancellationToken ct);
}

public interface IAccountAdminService
{
    Task<Result> LockAsync(int employeeId, LockAccountDto dto, CancellationToken ct);
    Task<Result> UnlockAsync(int employeeId, CancellationToken ct);
    Task<Result<TemporaryPasswordDto>> ResetPasswordAsync(int employeeId, CancellationToken ct);
}

public interface ITeamService
{
    Task<Result<IReadOnlyList<TeamListItemDto>>> ListAsync(bool includeDiscontinued, CancellationToken ct);
    Task<Result<TeamDetailDto>> GetAsync(int teamId, CancellationToken ct);
    Task<Result<int>> CreateAsync(SaveTeamDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int teamId, SaveTeamDto dto, CancellationToken ct);
    Task<Result> DiscontinueAsync(int teamId, CancellationToken ct);
    Task<Result> ReactivateAsync(int teamId, CancellationToken ct);
    Task<Result> TransferInAsync(int targetTeamId, TransferMembersDto dto, CancellationToken ct);
    Task<Result> AppointAsync(int teamId, AppointDto dto, CancellationToken ct);
}

public interface IHrLookupService
{
    Task<Result<IReadOnlyList<TeamLookupDto>>> TeamsAsync(CancellationToken ct);
    Task<Result<IReadOnlyList<EmployeeLookupDto>>> MembersAsync(int teamId, CancellationToken ct);
}
