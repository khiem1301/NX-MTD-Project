using FluentAssertions;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

public class CodeTextTests
{
    [Theory]
    [InlineData("PendingApproval", "PENDING_APPROVAL")]
    [InlineData("SemiFinishedRequisition", "SEMI_FINISHED_REQUISITION")]
    [InlineData("Active", "ACTIVE")]
    public void ToCode_PascalStatus_ReturnsUpperSnake(string pascal, string expected) =>
        CodeText.ToCode(pascal).Should().Be(expected);

    [Theory]
    [InlineData("PENDING_APPROVAL")]
    [InlineData("SEMI_FINISHED_REQUISITION")]
    [InlineData("MATERIAL_REQUISITION")]
    public void ToPascal_ThenToCode_RoundTrips(string code) =>
        CodeText.ToCode(CodeText.ToPascal(code)).Should().Be(code);

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.WarehouseKeeper)]
    [InlineData(Roles.TeamLeader)]
    [InlineData(Roles.TeamDeputy)]
    [InlineData(Roles.QaQc)]
    [InlineData(Roles.Electrician)]
    [InlineData(Roles.Worker)]
    public void Roles_ToRoleCode_FromRoleCode_RoundTrips(string role) =>
        Roles.FromRoleCode(Roles.ToRoleCode(role)).Should().Be(role);
}
