using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Requests;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>Đơn tăng ca (KPI không đạt) là đơn của riêng một tổ: chỉ tổ trưởng của đúng tổ đó xử lý.</summary>
public class OvertimeRequestAccessTests
{
    private static RequestAccess As(string role, int? teamId)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Role.Returns(role);
        user.TeamId.Returns(teamId);
        user.EmployeeId.Returns(1);
        return new RequestAccess(user);
    }

    [Fact]
    public void OnlyTheLeaderOfTheSameTeamHandlesAnOvertimeRequest()
    {
        As(Roles.TeamLeader, 5).IsHandler(RequestTypes.Overtime, 5).Should().BeTrue();
        As(Roles.TeamLeader, 6).IsHandler(RequestTypes.Overtime, 5).Should().BeFalse();
        As(Roles.Manager, null).IsHandler(RequestTypes.Overtime, 5).Should().BeFalse();
    }

    [Fact]
    public void ThePermissionsOfTheOwnTeamLeaderAllowApproveAndReject_OtherTeamsSeeNothing()
    {
        var own = As(Roles.TeamLeader, 5).PermissionsFor(RequestTypes.Overtime, RequestStatuses.PendingApproval, null, 5, true);
        own.CanApprove.Should().BeTrue();
        own.CanReject.Should().BeTrue();

        var other = As(Roles.TeamLeader, 6);
        other.CanView(RequestTypes.Overtime, null, 5).Should().BeFalse();
        other.PermissionsFor(RequestTypes.Overtime, RequestStatuses.PendingApproval, null, 5, true).CanApprove.Should().BeFalse();
    }

    [Fact]
    public void OvertimeIsNotInTheRoleWideInboxOfTeamLeaders()
    {
        As(Roles.TeamLeader, 5).HandledTypes().Should().NotContain(RequestTypes.Overtime);
        RequestTypePolicies.All[RequestTypes.Overtime].CanCreate(Roles.Manager, null).Should().BeFalse();   // chỉ hệ thống tạo
    }
}
