using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class AuthServiceTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_accounts, _hasher, _tokens, NullLogger<AuthService>.Instance);
        _tokens.CreateTokens(Arg.Any<AuthUser>()).Returns(new TokenPair("access", DateTime.UtcNow, "refresh", DateTime.UtcNow));
    }

    private static Account MakeAccount(string roleCode = "TEAM_LEADER", string accountStatus = RecordStatus.Active,
        string employeeStatus = RecordStatus.Active, Guid? stamp = null)
    {
        var employee = new Employee
        {
            EmployeeId = 7, EmployeeCode = "TTMAY", FullName = "Tổ trưởng", TeamId = 3, Status = employeeStatus,
            Role = new Role { RoleCode = roleCode, RoleName = "Tổ trưởng" },
            Team = new Team { TeamId = 3, TeamName = "Tổ may 1", TeamType = "SEWING" },
        };
        return new Account
        {
            AccountId = 5, EmployeeId = 7, PasswordHash = "hash",
            Status = accountStatus, SecurityStamp = stamp ?? Guid.NewGuid(), Employee = employee,
        };
    }

    private void GivenAccount(Account? account, bool passwordOk = true)
    {
        _accounts.GetByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(account);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(passwordOk);
    }

    [Fact]
    public async Task Login_UnknownUsername_ReturnsInvalidCredentials()
    {
        GivenAccount(null);

        var result = await _sut.LoginAsync(new LoginDto("nobody", "x"), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.AuthInvalidCredentials);
        result.Kind.Should().Be(ResultKind.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentials()
    {
        GivenAccount(MakeAccount(), passwordOk: false);

        var result = await _sut.LoginAsync(new LoginDto("TTMAY", "bad"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AuthInvalidCredentials);
        result.Kind.Should().Be(ResultKind.Unauthorized);
        result.Message.Should().Be(Messages.InvalidCredentials);
    }

    [Fact]
    public async Task Login_LockedAccount_ReturnsForbidden()
    {
        GivenAccount(MakeAccount(accountStatus: RecordStatus.Locked));

        var result = await _sut.LoginAsync(new LoginDto("TTMAY", "123456"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AuthAccountLocked);
        result.Kind.Should().Be(ResultKind.Forbidden);
    }

    [Fact]
    public async Task Login_ResignedEmployee_ReturnsForbidden()
    {
        GivenAccount(MakeAccount(employeeStatus: RecordStatus.Resigned));

        var result = await _sut.LoginAsync(new LoginDto("TTMAY", "123456"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AuthAccountDisabled);
        result.Kind.Should().Be(ResultKind.Forbidden);
    }

    [Fact]
    public async Task Login_ValidAccount_ReturnsTokensAndUser()
    {
        GivenAccount(MakeAccount());

        var result = await _sut.LoginAsync(new LoginDto("ttmay", "123456"), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access");
        result.Data.User.Role.Should().Be("TeamLeader");
        result.Data.User.TeamType.Should().Be("Sewing");
    }

    [Fact]
    public async Task Refresh_StampChanged_ReturnsInvalidRefreshToken()
    {
        var account = MakeAccount(stamp: Guid.NewGuid());
        _tokens.ReadRefreshToken("old").Returns(Result<RefreshClaims>.Success(new RefreshClaims(5, Guid.NewGuid())));
        _accounts.GetWithEmployeeAsync(5, Arg.Any<CancellationToken>()).Returns(account);

        var result = await _sut.RefreshAsync(new RefreshTokenDto("old"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AuthInvalidRefreshToken);
        result.Kind.Should().Be(ResultKind.Unauthorized);
    }
}
