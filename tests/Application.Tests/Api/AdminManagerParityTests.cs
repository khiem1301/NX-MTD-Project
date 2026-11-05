using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>DEV-ALL-34: Admin có toàn bộ chức năng của Quản đốc, ngoài phân quyền theo controller.</summary>
public class AdminManagerParityTests
{
    private static ICurrentUser User(string role)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Role.Returns(role);
        return user;
    }

    private static ProductionGuard Guard(string role) => new(Substitute.For<IUnitOfWork>(), User(role),
        Substitute.For<IClock>(), Substitute.For<ISystemSettingReader>());

    [Theory]
    [InlineData(Roles.Manager, true)]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.TeamLeader, false)]
    [InlineData(Roles.QaQc, false)]
    public void Production_guard_treats_admin_like_the_manager(string role, bool expected) =>
        Guard(role).IsManager.Should().Be(expected);

    [Theory]
    [InlineData(RequestTypes.MaterialSupplement)]
    [InlineData(RequestTypes.DefectiveMaterial)]
    [InlineData(RequestTypes.ReplacementCutting)]
    [InlineData(RequestTypes.ShortDelivery)]
    public void Admin_handles_every_request_type_of_the_manager(string requestType)
    {
        new RequestAccess(User(Roles.Admin)).IsHandler(requestType).Should().BeTrue();
        new RequestAccess(User(Roles.Admin)).HandledTypes().Should().Contain(requestType);
    }

    [Fact]
    public void Admin_keeps_its_own_request_types_and_gets_no_other_role_types()
    {
        var access = new RequestAccess(User(Roles.Admin));

        access.IsHandler(RequestTypes.SpareParts).Should().BeTrue();
        access.IsHandler(RequestTypes.AttendanceCorrection).Should().BeFalse();
        access.IsHandler(RequestTypes.MaterialRequisition).Should().BeFalse();
    }

    [Fact]
    public void Manager_does_not_gain_the_admin_request_types()
    {
        new RequestAccess(User(Roles.Manager)).IsHandler(RequestTypes.SpareParts).Should().BeFalse();
    }
}
