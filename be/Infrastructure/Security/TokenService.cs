using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Security;

public sealed class TokenService : ITokenService
{
    private const string TokenTypeClaim = "tokenType";
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 ký tự (đặt bằng user-secrets).");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
    }

    public TokenPair CreateTokens(AuthUser user)
    {
        var now = DateTime.UtcNow;
        var accessExp = now.AddMinutes(_options.AccessMinutes);
        var refreshExp = now.AddDays(_options.RefreshDays);

        var access = new List<Claim>
        {
            new("sub", user.AccountId.ToString()),
            new("employeeId", user.EmployeeId.ToString()),
            new("username", user.Username),
            new("fullName", user.FullName),
            new("role", user.Role),
            new("stamp", user.Stamp.ToString()),
            new(TokenTypeClaim, "access"),
        };
        if (user.TeamId is not null) access.Add(new Claim("teamId", user.TeamId.Value.ToString()));
        if (user.TeamType is not null) access.Add(new Claim("teamType", user.TeamType));

        var refresh = new List<Claim>
        {
            new("sub", user.AccountId.ToString()),
            new("stamp", user.Stamp.ToString()),
            new(TokenTypeClaim, "refresh"),
        };

        return new TokenPair(Write(access, now, accessExp), accessExp, Write(refresh, now, refreshExp), refreshExp);
    }

    public Result<RefreshClaims> ReadRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = _options.Issuer,
                ValidateAudience = true, ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = _key,
                ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
            }, out _);

            if (principal.FindFirst(TokenTypeClaim)?.Value != "refresh"
                || !int.TryParse(principal.FindFirst("sub")?.Value, out var accountId)
                || !Guid.TryParse(principal.FindFirst("stamp")?.Value, out var stamp))
                return Invalid();

            return Result<RefreshClaims>.Success(new RefreshClaims(accountId, stamp));
        }
        catch (SecurityTokenException) { return Invalid(); }
        catch (ArgumentException) { return Invalid(); }
    }

    private string Write(IEnumerable<Claim> claims, DateTime now, DateTime expires)
    {
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now, expires,
            new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Result<RefreshClaims> Invalid() =>
        Result<RefreshClaims>.Failure(ErrorCodes.AuthInvalidRefreshToken, Messages.InvalidRefreshToken, ResultKind.Unauthorized);
}
