namespace NX_MTD.Application.Common;

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 7;
}
