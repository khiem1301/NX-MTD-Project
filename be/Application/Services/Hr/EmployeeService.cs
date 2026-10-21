using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Hr;

public sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employees;
    private readonly ITeamRepository _teams;
    private readonly IPasswordHasher _hasher;
    private readonly ITemporaryPasswordGenerator _passwords;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly EmployeeScope _scope;

    public EmployeeService(IEmployeeRepository employees, ITeamRepository teams, IPasswordHasher hasher,
        ITemporaryPasswordGenerator passwords, IAuditLogService audit, IUnitOfWork uow, ICurrentUser user, IClock clock,
        EmployeeScope scope)
    {
        _employees = employees; _teams = teams; _hasher = hasher; _passwords = passwords; _audit = audit; _uow = uow;
        _user = user; _clock = clock; _scope = scope;
    }

    public async Task<Result<PagedResult<EmployeeListItemDto>>> ListAsync(EmployeeQuery query, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(query.Role) && !Roles.IsKnown(query.Role))
            return BadFilter<PagedResult<EmployeeListItemDto>>("role");
        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status is not ("Active" or "Resigned"))
            return BadFilter<PagedResult<EmployeeListItemDto>>("status");
        return Result<PagedResult<EmployeeListItemDto>>.Success(
            await _employees.ListAsync(query, _scope.HiddenRoleCodes, _scope.IsAdmin, ct));
    }

    public async Task<Result<EmployeeDetailDto>> GetAsync(int employeeId, CancellationToken ct)
    {
        var detail = await _employees.GetDetailAsync(employeeId, _scope.IsAdmin, ct);
        if (detail is null || !_scope.CanSee(Roles.ToRoleCode(detail.Role))) return Fail.NotFound<EmployeeDetailDto>();
        var history = await _audit.GetHistoryAsync(nameof(Employee), employeeId, ct);
        return Result<EmployeeDetailDto>.Success(detail with { History = history });
    }

    /// <summary>UC17: luôn tạo kèm tài khoản, tên đăng nhập = mã NV, mật khẩu tạm hiện một lần (D29).</summary>
    public async Task<Result<CreatedEmployeeDto>> CreateAsync(CreateEmployeeDto dto, CancellationToken ct)
    {
        var code = dto.EmployeeCode.Trim().ToUpperInvariant();
        if (await _employees.CodeExistsAsync(code, ct))
            return Result<CreatedEmployeeDto>.Failure(ErrorCodes.EmployeeCodeDuplicate, Messages.EmployeeCodeDuplicate);
        var roleCode = Roles.ToRoleCode(dto.Role);
        var check = await CheckPlacementAsync(roleCode, dto.TeamId, dto.AttendanceCode, null, ct);
        if (!check.IsSuccess) return Fail.As<CreatedEmployeeDto>(check);

        var tempPassword = _passwords.Generate();
        var employee = new Employee
        {
            EmployeeCode = code, FullName = dto.FullName.Trim(), Gender = dto.Gender, DateOfBirth = dto.DateOfBirth,
            Phone = Clean(dto.Phone), RoleId = await _employees.GetRoleIdAsync(roleCode, ct), TeamId = dto.TeamId,
            PayType = CodeText.ToCode(dto.PayType), StartDate = dto.StartDate, Status = RecordStatus.Active, CreatedAt = _clock.Now,
            Account = new Account
            {
                Username = code, PasswordHash = _hasher.Hash(tempPassword), IsTemporaryPassword = true,
                SecurityStamp = Guid.NewGuid(), Status = RecordStatus.Active, CreatedAt = _clock.Now,
            },
        };

        return await _uow.InTransactionAsync(async () =>
        {
            await _employees.AddAsync(employee);
            await _uow.SaveChangesAsync();   // lấy EmployeeId; trigger tổ trưởng chạy ở đây
            if (dto.AttendanceCode is int att) await _employees.MapAttendanceCodeAsync(att, employee.EmployeeId, _user.EmployeeId, ct);
            _audit.Add(new AuditEntry(nameof(Employee), employee.EmployeeId, AuditActions.Insert, null, Snapshot(employee, roleCode)));
            await _uow.SaveChangesAsync();
            return Result<CreatedEmployeeDto>.Success(new CreatedEmployeeDto(employee.EmployeeId, code, tempPassword), Messages.EmployeeCreated);
        });
    }

    public async Task<Result> AdminUpdateAsync(int employeeId, AdminUpdateEmployeeDto dto, CancellationToken ct)
    {
        var employee = await _employees.GetFullAsync(employeeId, ct);
        if (employee is null) return Fail.NotFound();
        if (employee.Status != RecordStatus.Active) return Result.Failure(ErrorCodes.EmployeeNotActive, Messages.EmployeeNotActive);

        var roleCode = Roles.ToRoleCode(dto.Role);
        var attChanged = dto.AttendanceCode != employee.AttendanceCode;
        var check = await CheckPlacementAsync(roleCode, dto.TeamId, attChanged ? dto.AttendanceCode : null, employeeId, ct);
        if (!check.IsSuccess) return check;

        _employees.SetOriginalRowVersion(employee, dto.RowVer);
        var oldRole = employee.Role.RoleCode;
        var before = Snapshot(employee, oldRole);
        var teamChanged = employee.TeamId != dto.TeamId;
        var identityChanged = oldRole != roleCode || teamChanged;
        if (oldRole != roleCode) employee.RoleId = await _employees.GetRoleIdAsync(roleCode, ct);
        ApplyProfile(employee, dto.FullName, dto.Gender, dto.DateOfBirth, dto.Phone, dto.StartDate);
        employee.TeamId = dto.TeamId;
        employee.PayType = CodeText.ToCode(dto.PayType);
        if (dto.AttendanceCode is null) employee.AttendanceCode = null;   // gán mã mới đi qua thủ tục bên dưới
        if (identityChanged) RotateStamp(employee);   // token cũ mang vai trò / tổ cũ (D29)
        var reason = teamChanged ? $"Chuyển tổ, hiệu lực {_clock.Today:dd/MM/yyyy}" : null;   // D32
        return await SaveAndMapAsync(employee, before, roleCode, attChanged ? dto.AttendanceCode : null, reason, ct);
    }

    public async Task<Result> ManagerUpdateAsync(int employeeId, ManagerUpdateEmployeeDto dto, CancellationToken ct)
    {
        var employee = await _employees.GetFullAsync(employeeId, ct);
        if (employee is null || !_scope.CanSee(employee.Role.RoleCode)) return Fail.NotFound();
        if (employee.Status != RecordStatus.Active) return Result.Failure(ErrorCodes.EmployeeNotActive, Messages.EmployeeNotActive);

        var attChanged = dto.AttendanceCode != employee.AttendanceCode;
        var check = await CheckPlacementAsync(employee.Role.RoleCode, employee.TeamId, attChanged ? dto.AttendanceCode : null, employeeId, ct, checkTeam: false);
        if (!check.IsSuccess) return check;

        _employees.SetOriginalRowVersion(employee, dto.RowVer);
        var before = Snapshot(employee, employee.Role.RoleCode);
        ApplyProfile(employee, dto.FullName, dto.Gender, dto.DateOfBirth, dto.Phone, dto.StartDate);
        if (dto.AttendanceCode is null) employee.AttendanceCode = null;
        return await SaveAndMapAsync(employee, before, employee.Role.RoleCode, attChanged ? dto.AttendanceCode : null, null, ct);
    }

    public async Task<Result> ResignAsync(int employeeId, ResignEmployeeDto dto, CancellationToken ct)
    {
        if (employeeId == _user.EmployeeId) return Result.Failure(ErrorCodes.AccountCannotLockSelf, Messages.CannotActOnSelf);
        var employee = await _employees.GetFullAsync(employeeId, ct);
        if (employee is null) return Fail.NotFound();
        if (employee.Status != RecordStatus.Active) return Result.Failure(ErrorCodes.EmployeeNotActive, Messages.EmployeeNotActive);
        if (dto.ResignDate < employee.StartDate)
            return Result.Failure(ErrorCodes.CommonValidation, "Ngày nghỉ không được trước ngày vào làm.", ResultKind.Validation,
                new Dictionary<string, string[]> { ["resignDate"] = new[] { "Ngày nghỉ không được trước ngày vào làm." } });

        _employees.SetOriginalRowVersion(employee, dto.RowVer);
        employee.Status = RecordStatus.Resigned;
        employee.ResignDate = dto.ResignDate;
        employee.UpdatedAt = _clock.Now;
        RotateStamp(employee);
        _audit.Add(new AuditEntry(nameof(Employee), employeeId, AuditActions.Update, new { Status = RecordStatus.Active },
            new { Status = RecordStatus.Resigned, ResignDate = dto.ResignDate }, dto.Reason.Trim()));
        await _uow.SaveChangesAsync();

        var wasLeader = employee.Role.RoleCode is RoleCodes.TeamLeader or RoleCodes.TeamDeputy;
        var message = wasLeader
            ? string.Format(Messages.ResignedWasLeader, employee.Team?.TeamName, employee.Role.RoleCode == RoleCodes.TeamLeader ? "tổ trưởng" : "tổ phó")
            : Messages.EmployeeResigned;
        return Result.Success(message);
    }

    /// <summary>Tổ hợp lệ, tổ trưởng / tổ phó đúng loại tổ và không trùng người (D31), mã chấm công chưa ai giữ.</summary>
    private async Task<Result> CheckPlacementAsync(string roleCode, int? teamId, int? attendanceCode, int? exceptEmployeeId,
        CancellationToken ct, bool checkTeam = true)
    {
        if (checkTeam && teamId is int tid)
        {
            var team = await _teams.GetByIdAsync(tid);
            if (team is null || team.Status != RecordStatus.Active) return Result.Failure(ErrorCodes.TeamNotActive, Messages.TeamNotActive);
            if (roleCode is RoleCodes.TeamLeader or RoleCodes.TeamDeputy)
            {
                if (team.TeamType == TeamTypes.Office) return Result.Failure(ErrorCodes.TeamLeaderInvalidTeam, Messages.TeamLeaderInvalidTeam);
                var holder = await _employees.FindTeamRoleHolderAsync(tid, roleCode, exceptEmployeeId, ct);
                if (holder is not null) return PositionTaken(roleCode, team.TeamName, holder.FullName);
            }
        }
        if (attendanceCode is int att && await _employees.AttendanceCodeTakenAsync(att, exceptEmployeeId, ct))
            return Result.Failure(ErrorCodes.EmployeeAttendanceCodeTaken, string.Format(Messages.EmployeeAttendanceCodeTaken, att));
        return Result.Success();
    }

    private static Result PositionTaken(string roleCode, string teamName, string holderName) =>
        roleCode == RoleCodes.TeamLeader
            ? Result.Failure(ErrorCodes.TeamLeaderExists, string.Format(Messages.TeamLeaderExistsNamed, teamName, holderName))
            : Result.Failure(ErrorCodes.TeamDeputyExists, string.Format(Messages.TeamDeputyExistsNamed, teamName, holderName));

    /// <summary>Lưu thay đổi hồ sơ rồi mới gọi thủ tục map mã chấm công (thủ tục UPDATE Employee làm đổi RowVer, D30).</summary>
    private async Task<Result> SaveAndMapAsync(Employee employee, object before, string roleCode, int? mapCode, string? reason,
        CancellationToken ct)
    {
        employee.UpdatedAt = _clock.Now;
        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(Employee), employee.EmployeeId, AuditActions.Update, before, Snapshot(employee, roleCode), reason));
            await _uow.SaveChangesAsync();
            if (mapCode is int att) await _employees.MapAttendanceCodeAsync(att, employee.EmployeeId, _user.EmployeeId, ct);
            return Result.Success(Messages.Saved);
        });
    }

    private static void ApplyProfile(Employee e, string fullName, string? gender, DateOnly? dob, string? phone, DateOnly startDate)
    {
        e.FullName = fullName.Trim();
        e.Gender = gender;
        e.DateOfBirth = dob;
        e.Phone = Clean(phone);
        e.StartDate = startDate;
    }

    private static void RotateStamp(Employee e)
    {
        if (e.Account is not null) e.Account.SecurityStamp = Guid.NewGuid();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<T> BadFilter<T>(string field) => Result<T>.Failure(ErrorCodes.CommonValidation, "Dữ liệu không hợp lệ.",
        ResultKind.Validation, new Dictionary<string, string[]> { [field] = new[] { "Giá trị không hợp lệ." } });

    /// <summary>Không chứa gì của tài khoản (mật khẩu, hash).</summary>
    private static object Snapshot(Employee e, string roleCode) => new
    {
        e.EmployeeCode, e.FullName, e.Gender, e.DateOfBirth, e.Phone, RoleCode = roleCode, e.TeamId, e.PayType,
        e.StartDate, e.AttendanceCode, e.Status,
    };
}
