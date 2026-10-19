using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public int AccountId => Int("sub") ?? throw new InvalidOperationException("Chưa đăng nhập.");
    public int EmployeeId => Int("employeeId") ?? throw new InvalidOperationException("Chưa đăng nhập.");
    public string Role => _accessor.HttpContext?.User.FindFirst("role")?.Value ?? throw new InvalidOperationException("Chưa đăng nhập.");
    public int? TeamId => Int("teamId");

    private int? Int(string claim) =>
        int.TryParse(_accessor.HttpContext?.User.FindFirst(claim)?.Value, out var v) ? v : null;
}
