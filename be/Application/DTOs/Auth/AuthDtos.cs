namespace NX_MTD.Application.DTOs;

public sealed record LoginDto(string Username, string Password);
public sealed record RefreshTokenDto(string RefreshToken);

public sealed record UserInfoDto(
    int AccountId, int EmployeeId, string Username, string FullName,
    string Role, string RoleName, int? TeamId, string? TeamName, string? TeamType,
    bool IsTemporaryPassword);

public sealed record LoginResultDto(
    string AccessToken, DateTime AccessExpiresAt,
    string RefreshToken, DateTime RefreshExpiresAt,
    UserInfoDto User);
