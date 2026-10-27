using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAccountRepository accounts, IPasswordHasher hasher, ITokenService tokens,
        ILogger<AuthService> logger)
    {
        _accounts = accounts; _hasher = hasher; _tokens = tokens; _logger = logger;
    }

    public async Task<Result<LoginResultDto>> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var account = await _accounts.GetByUsernameAsync(dto.Username.Trim(), ct);
        if (account is null || !_hasher.Verify(dto.Password, account.PasswordHash))
        {
            _logger.LogWarning("Login failed for username {Username}", dto.Username);
            return Fail(ErrorCodes.AuthInvalidCredentials, Messages.InvalidCredentials, ResultKind.Unauthorized);
        }

        var blocked = CheckUsable(account);
        if (blocked is not null) return blocked;

        return Result<LoginResultDto>.Success(BuildLoginResult(account));
    }

    public async Task<Result<LoginResultDto>> RefreshAsync(RefreshTokenDto dto, CancellationToken ct)
    {
        var read = _tokens.ReadRefreshToken(dto.RefreshToken);
        if (!read.IsSuccess || read.Data is null)
            return Fail(ErrorCodes.AuthInvalidRefreshToken, Messages.InvalidRefreshToken, ResultKind.Unauthorized);

        var account = await _accounts.GetWithEmployeeAsync(read.Data.AccountId, ct);
        if (account is null || account.SecurityStamp != read.Data.Stamp)
            return Fail(ErrorCodes.AuthInvalidRefreshToken, Messages.InvalidRefreshToken, ResultKind.Unauthorized);

        var blocked = CheckUsable(account);
        if (blocked is not null) return blocked;

        return Result<LoginResultDto>.Success(BuildLoginResult(account));
    }

    public async Task<Result<LoginResultDto>> IssueForAccountAsync(int accountId, CancellationToken ct)
    {
        var account = await _accounts.GetWithEmployeeAsync(accountId, ct);
        if (account is null)
            return Fail(ErrorCodes.AuthUnauthorized, Messages.Unauthorized, ResultKind.Unauthorized);
        return CheckUsable(account) ?? Result<LoginResultDto>.Success(BuildLoginResult(account));
    }

    public async Task<Result<UserInfoDto>> GetMeAsync(int accountId, CancellationToken ct)
    {
        var account = await _accounts.GetWithEmployeeAsync(accountId, ct);
        return account is null
            ? Result<UserInfoDto>.Failure(ErrorCodes.AuthUnauthorized, Messages.Unauthorized, ResultKind.Unauthorized)
            : Result<UserInfoDto>.Success(ToUserInfo(account));
    }

    public async Task<bool> IsSessionValidAsync(int accountId, Guid stamp, CancellationToken ct)
    {
        var account = await _accounts.GetWithEmployeeAsync(accountId, ct);
        return account is not null
               && account.SecurityStamp == stamp
               && account.Status == RecordStatus.Active
               && account.Employee.Status == RecordStatus.Active;
    }

    private static Result<LoginResultDto>? CheckUsable(Account account)
    {
        if (account.Status != RecordStatus.Active)
            return Fail(ErrorCodes.AuthAccountLocked, Messages.AccountLocked, ResultKind.Forbidden);
        if (account.Employee.Status != RecordStatus.Active)
            return Fail(ErrorCodes.AuthAccountDisabled, Messages.AccountDisabled, ResultKind.Forbidden);
        return null;
    }

    private LoginResultDto BuildLoginResult(Account account)
    {
        var employee = account.Employee;
        var pair = _tokens.CreateTokens(new AuthUser(
            account.AccountId, employee.EmployeeId, employee.EmployeeCode, employee.FullName,
            Roles.FromRoleCode(employee.Role.RoleCode), employee.TeamId,
            employee.Team is null ? null : CodeText.ToPascal(employee.Team.TeamType),
            account.SecurityStamp));
        return new LoginResultDto(pair.AccessToken, pair.AccessExpiresAt, pair.RefreshToken, pair.RefreshExpiresAt,
            ToUserInfo(account));
    }

    private static UserInfoDto ToUserInfo(Account account)
    {
        var e = account.Employee;
        return new UserInfoDto(account.AccountId, e.EmployeeId, e.EmployeeCode, e.FullName,
            Roles.FromRoleCode(e.Role.RoleCode), e.Role.RoleName, e.TeamId, e.Team?.TeamName,
            e.Team is null ? null : CodeText.ToPascal(e.Team.TeamType), account.IsTemporaryPassword);
    }

    private static Result<LoginResultDto> Fail(string code, string message, ResultKind kind) =>
        Result<LoginResultDto>.Failure(code, message, kind);
}
