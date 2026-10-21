using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Hr;

/// <summary>Khóa / mở khóa / đặt lại mật khẩu tài khoản (chỉ Admin). Mọi thao tác đổi SecurityStamp → đẩy người đó ra (D29).</summary>
public sealed class AccountAdminService : IAccountAdminService
{
    private readonly IEmployeeRepository _employees;
    private readonly IPasswordHasher _hasher;
    private readonly ITemporaryPasswordGenerator _passwords;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AccountAdminService(IEmployeeRepository employees, IPasswordHasher hasher, ITemporaryPasswordGenerator passwords,
        IAuditLogService audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    {
        _employees = employees; _hasher = hasher; _passwords = passwords; _audit = audit; _uow = uow; _user = user; _clock = clock;
    }

    public async Task<Result> LockAsync(int employeeId, LockAccountDto dto, CancellationToken ct)
    {
        if (employeeId == _user.EmployeeId) return Result.Failure(ErrorCodes.AccountCannotLockSelf, Messages.AccountCannotLockSelf);
        var account = await FindAccountAsync(employeeId, ct);
        if (account is null) return Fail.NotFound();
        if (account.Status == RecordStatus.Locked) return Result.Failure(ErrorCodes.AccountInvalidStatus, Messages.AccountInvalidStatus);

        account.Status = RecordStatus.Locked;
        account.LockedAt = _clock.Now;
        account.LockReason = dto.Reason.Trim();
        account.SecurityStamp = Guid.NewGuid();
        _audit.Add(new AuditEntry(nameof(Account), account.AccountId, AuditActions.Lock, null, null, account.LockReason));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.AccountLockedDone);
    }

    public async Task<Result> UnlockAsync(int employeeId, CancellationToken ct)
    {
        var employee = await _employees.GetFullAsync(employeeId, ct);
        var account = employee?.Account;
        if (employee is null || account is null) return Fail.NotFound();
        if (employee.Status != RecordStatus.Active) return Result.Failure(ErrorCodes.EmployeeNotActive, Messages.EmployeeNotActive);
        if (account.Status != RecordStatus.Locked) return Result.Failure(ErrorCodes.AccountInvalidStatus, Messages.AccountInvalidStatus);

        account.Status = RecordStatus.Active;
        account.LockedAt = null;
        account.LockReason = null;
        _audit.Add(new AuditEntry(nameof(Account), account.AccountId, AuditActions.Unlock));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.AccountUnlockedDone);
    }

    public async Task<Result<TemporaryPasswordDto>> ResetPasswordAsync(int employeeId, CancellationToken ct)
    {
        if (employeeId == _user.EmployeeId)
            return Result<TemporaryPasswordDto>.Failure(ErrorCodes.AccountCannotLockSelf, Messages.CannotActOnSelf);
        var account = await FindAccountAsync(employeeId, ct);
        if (account is null) return Fail.NotFound<TemporaryPasswordDto>();

        var temp = _passwords.Generate();
        account.PasswordHash = _hasher.Hash(temp);
        account.IsTemporaryPassword = true;
        account.SecurityStamp = Guid.NewGuid();
        _audit.Add(new AuditEntry(nameof(Account), account.AccountId, AuditActions.Update, null, new { PasswordReset = true }));
        await _uow.SaveChangesAsync();
        return Result<TemporaryPasswordDto>.Success(new TemporaryPasswordDto(account.Username, temp), Messages.PasswordResetDone);
    }

    private async Task<Account?> FindAccountAsync(int employeeId, CancellationToken ct) =>
        (await _employees.GetFullAsync(employeeId, ct))?.Account;
}
