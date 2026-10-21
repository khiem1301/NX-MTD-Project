using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Hr;

/// <summary>UC03 đổi mật khẩu + UC04 xem hồ sơ của mình.</summary>
public sealed class ProfileService : IProfileService
{
    private readonly IEmployeeRepository _employees;
    private readonly IGenericRepository<Account> _accounts;
    private readonly IAuthService _auth;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;

    public ProfileService(IEmployeeRepository employees, IGenericRepository<Account> accounts, IAuthService auth,
        IPasswordHasher hasher, IAuditLogService audit, IUnitOfWork uow, ICurrentUser user)
    {
        _employees = employees; _accounts = accounts; _auth = auth; _hasher = hasher; _audit = audit; _uow = uow; _user = user;
    }

    public async Task<Result<ProfileDto>> GetAsync(CancellationToken ct)
    {
        var e = await _employees.GetFullAsync(_user.EmployeeId, ct);
        if (e is null) return Fail.NotFound<ProfileDto>();
        // UC04: bậc thợ chỉ có ý nghĩa với người của tổ may.
        var showGrade = e.Team?.TeamType == TeamTypes.Sewing
                        && e.Role.RoleCode is RoleCodes.Worker or RoleCodes.TeamLeader or RoleCodes.TeamDeputy;
        return Result<ProfileDto>.Success(new ProfileDto(e.EmployeeId, e.EmployeeCode, e.FullName, e.Gender, e.DateOfBirth,
            e.Phone, e.AvatarUrl, Roles.FromRoleCode(e.Role.RoleCode), e.Role.RoleName, e.Team?.TeamName, e.StartDate,
            showGrade ? e.CurrentGradeCode : null, e.Account?.IsTemporaryPassword ?? false));
    }

    public async Task<Result<LoginResultDto>> ChangePasswordAsync(ChangePasswordDto dto, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(_user.AccountId);
        if (account is null) return Fail.NotFound<LoginResultDto>();
        if (!_hasher.Verify(dto.CurrentPassword, account.PasswordHash))   // D36: 409, không 401 để FE không tự refresh
            return Result<LoginResultDto>.Failure(ErrorCodes.AuthWrongCurrentPassword, Messages.WrongCurrentPassword);

        account.PasswordHash = _hasher.Hash(dto.NewPassword);
        account.IsTemporaryPassword = false;
        account.SecurityStamp = Guid.NewGuid();   // đẩy mọi phiên khác ra (MSG08)
        _audit.Add(new AuditEntry(nameof(Account), account.AccountId, AuditActions.Update, null, new { PasswordChanged = true }));
        await _uow.SaveChangesAsync();

        var tokens = await _auth.IssueForAccountAsync(account.AccountId, ct);
        return tokens.IsSuccess ? Result<LoginResultDto>.Success(tokens.Data!, Messages.PasswordChanged) : tokens;
    }
}
