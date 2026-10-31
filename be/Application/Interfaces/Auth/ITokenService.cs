using NX_MTD.Application.Common;

namespace NX_MTD.Application.Interfaces;

public sealed record AuthUser(int AccountId, int EmployeeId, string Username, string FullName, string Role,
    int? TeamId, string? TeamType, Guid Stamp);
public sealed record TokenPair(string AccessToken, DateTime AccessExpiresAt, string RefreshToken, DateTime RefreshExpiresAt);
public sealed record RefreshClaims(int AccountId, Guid Stamp);

public interface ITokenService
{
    TokenPair CreateTokens(AuthUser user);
    /// <summary>Kiểm tra chữ ký, hạn dùng, tokenType = refresh.</summary>
    Result<RefreshClaims> ReadRefreshToken(string refreshToken);
}
