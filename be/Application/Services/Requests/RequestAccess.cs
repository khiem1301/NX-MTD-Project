using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services.Requests;

/// <summary>D15: ai thấy / ai thao tác đơn nào. Dùng chung cho service, query, quyền ảnh.</summary>
public sealed class RequestAccess
{
    private readonly ICurrentUser _user;
    public RequestAccess(ICurrentUser user) => _user = user;

    public bool IsCreatorSide(int? createdBy, int? createdByTeamId) =>
        Roles.IsTeamRole(_user.Role)
            ? createdByTeamId is not null && createdByTeamId == _user.TeamId
            : createdBy is not null && createdBy == _user.EmployeeId;

    public bool IsHandler(string requestType) =>
        RequestTypePolicies.All.TryGetValue(requestType, out var p) && p.HandlerRole == _user.Role;

    public bool CanView(string requestType, int? createdBy, int? createdByTeamId) =>
        IsCreatorSide(createdBy, createdByTeamId) || IsHandler(requestType);


    public IReadOnlyCollection<string> HandledTypes() =>
        RequestTypePolicies.All.Values.Where(p => p.HandlerRole == _user.Role).Select(p => p.RequestType).ToList();

    public IReadOnlyList<RequestTypePolicy> CreatableTypes() =>
        RequestTypePolicies.All.Values.Where(p => p.CanCreate(_user.Role, _user.TeamTypeCode)).ToList();

    public RequestPermissionsDto PermissionsFor(string requestType, string status, int? createdBy, int? teamId, bool allowManualComplete)
    {
        var creator = IsCreatorSide(createdBy, teamId);
        var handler = IsHandler(requestType);
        var pending = RequestStatuses.Pending.Contains(status);
        return new RequestPermissionsDto(
            CanEdit: creator && pending,
            CanCancel: creator && pending && createdBy is not null,
            CanApprove: handler && status == RequestStatuses.PendingApproval,
            CanReject: handler && RequestStatuses.Rejectable.Contains(status),
            CanComplete: handler && allowManualComplete && RequestStatuses.Completable.Contains(status));
    }
}
