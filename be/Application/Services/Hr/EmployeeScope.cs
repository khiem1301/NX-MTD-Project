using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services.Hr;

/// <summary>Admin thấy tất cả; Quản đốc không thấy Admin / Quản đốc / Kế toán (UC16, GBR-01).</summary>
public sealed class EmployeeScope
{
    private readonly ICurrentUser _user;
    public EmployeeScope(ICurrentUser user) => _user = user;

    public bool IsAdmin => _user.Role == Roles.Admin;
    public IReadOnlyCollection<string>? HiddenRoleCodes => IsAdmin ? null : RoleCodes.HiddenFromManager.ToArray();
    public bool CanSee(string roleCode) => IsAdmin || !RoleCodes.HiddenFromManager.Contains(roleCode);
}
