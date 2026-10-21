using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
    public int AccountId => Int("sub") ?? throw NotSignedIn();
    public int EmployeeId => Int("employeeId") ?? throw NotSignedIn();
    public string Role => Claim("role") ?? throw NotSignedIn();
    public int? TeamId => Int("teamId");
    public string? TeamTypeCode => Claim("teamType") is { } pascal ? CodeText.ToCode(pascal) : null;

    private string? Claim(string type) => _accessor.HttpContext?.User.FindFirst(type)?.Value;
    private int? Int(string type) => int.TryParse(Claim(type), out var v) ? v : null;
    private static InvalidOperationException NotSignedIn() => new("Chưa đăng nhập.");
}
