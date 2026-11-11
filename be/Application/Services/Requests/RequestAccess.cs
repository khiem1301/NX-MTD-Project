using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>Người xử lý người tạo chọn cho đơn Khác (DEV-ALL-91); chọn tổ trưởng thì kèm tổ của người tạo.</summary>
public sealed record ChosenHandler(string? RoleCode, int? TeamId);

/// <summary>D15: ai thấy / ai thao tác đơn nào. Dùng chung cho service, query, quyền ảnh.</summary>
public sealed class RequestAccess
{
    private readonly ICurrentUser _user;
    public RequestAccess(ICurrentUser user) => _user = user;

    public bool IsCreatorSide(int? createdBy, int? createdByTeamId) =>
        Roles.IsTeamRole(_user.Role)
            ? createdByTeamId is not null && createdByTeamId == _user.TeamId
            : createdBy is not null && createdBy == _user.EmployeeId;

    /// <summary>Admin xử lý được đơn của Quản đốc; tổ phó cùng quyền tổ trưởng.</summary>
    public bool HandlesRole(string role) =>
        role == _user.Role
        || (role == Roles.Manager && _user.Role == Roles.Admin)
        || (role == Roles.TeamLeader && _user.Role == Roles.TeamDeputy);

    public bool IsHandler(string requestType, ChosenHandler? chosen = null)
    {
        if (!RequestTypePolicies.All.TryGetValue(requestType, out var p)) return false;
        if (!p.HandlerChosenOnCreate) return HandlesRole(p.HandlerRole);
        if (chosen?.RoleCode is not { } code || !HandlesRole(Roles.FromRoleCode(code))) return false;
        return code != RoleCodes.TeamLeader || (chosen.TeamId is not null && chosen.TeamId == _user.TeamId);
    }

    public bool CanView(string requestType, int? createdBy, int? createdByTeamId, ChosenHandler? chosen = null) =>
        IsCreatorSide(createdBy, createdByTeamId) || IsHandler(requestType, chosen);

    public IReadOnlyCollection<string> HandledTypes() =>
        RequestTypePolicies.All.Values.Where(p => !p.HandlerChosenOnCreate && HandlesRole(p.HandlerRole))
            .Select(p => p.RequestType).ToList();

    /// <summary>Vai trò của đơn Khác mà người dùng xử lý, trừ tổ trưởng (lọc theo tổ ở <see cref="HandledTeamId"/>).</summary>
    public IReadOnlyCollection<string> HandledRoleCodes() =>
        RequestTypePolicies.ChoosableHandlerRoles.Where(r => r != Roles.TeamLeader && HandlesRole(r))
            .Select(Roles.ToRoleCode).ToList();

    public int? HandledTeamId() => Roles.IsTeamRole(_user.Role) ? _user.TeamId : null;

    /// <summary>
    /// Không chọn vai trò của chính mình. Tổ trưởng chỉ dành cho công nhân đang thuộc tổ: Admin, Quản đốc, Kế toán, Thủ kho...
    /// không thuộc tổ nào; tổ trưởng / tổ phó không gửi cho chính tổ mình.
    /// </summary>
    public IReadOnlyList<string> ChoosableHandlerRoles() =>
        RequestTypePolicies.ChoosableHandlerRoles
            .Where(r => !HandlesRole(r))
            .Where(r => r != Roles.TeamLeader || (_user.Role == Roles.Worker && _user.TeamId is not null))
            .ToList();

    public IReadOnlyList<RequestTypePolicy> CreatableTypes() =>
        RequestTypePolicies.All.Values.Where(p => p.CanCreate(_user.Role, _user.TeamTypeCode)).ToList();

    public RequestPermissionsDto PermissionsFor(string requestType, string status, int? createdBy, int? teamId,
        bool allowManualComplete, ChosenHandler? chosen = null)
    {
        var creator = IsCreatorSide(createdBy, teamId);
        var handler = IsHandler(requestType, chosen);
        var pending = RequestStatuses.Pending.Contains(status);
        return new RequestPermissionsDto(
            CanEdit: creator && pending,
            CanCancel: creator && pending && createdBy is not null,
            CanApprove: handler && status == RequestStatuses.PendingApproval,
            CanReject: handler && RequestStatuses.Rejectable.Contains(status),
            CanComplete: handler && allowManualComplete && RequestStatuses.Completable.Contains(status));
    }
}
