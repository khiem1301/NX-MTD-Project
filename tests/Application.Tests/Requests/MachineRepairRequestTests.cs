using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-169: tổ trưởng / tổ phó tổ cắt và tổ may gửi đơn Sửa máy, Cơ điện xử lý, không qua duyệt.</summary>
public class MachineRepairRequestTests
{
    private readonly IRequestRepository _requests = Substitute.For<IRequestRepository>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private Request? _saved;

    public MachineRepairRequestTests()
    {
        _clock.Now.Returns(new DateTime(2026, 11, 24, 8, 0, 0));
        _clock.Today.Returns(new DateOnly(2026, 11, 24));
        _user.EmployeeId.Returns(7);
        _requests.When(x => x.AddAsync(Arg.Any<Request>())).Do(c => _saved = c.Arg<Request>());
    }

    private RequestService Service() => new(_requests, Substitute.For<IGenericRepository<RequestLine>>(),
        Substitute.For<IUnitOfWork>(), _user, _clock, Substitute.For<IAuditLogService>(), _notifier, new RequestAccess(_user),
        new RequestTypeHandlers(new IRequestTypeHandler[] { new MachineRepairRequestHandler(_clock) }));

    private void SignIn(string role, string? teamType, int? teamId = 5)
    {
        _user.Role.Returns(role);
        _user.TeamTypeCode.Returns(teamType);
        _user.TeamId.Returns(teamId);
    }

    private static CreateRequestDto Dto(IReadOnlyList<RequestLineInput>? lines = null) =>
        new("MachineRepair", "Máy may số 3 hỏng", "Máy không chạy kim", null, null, null, null, null, null, lines);

    [Theory]
    [InlineData(Roles.TeamLeader, TeamTypes.Sewing, true)]
    [InlineData(Roles.TeamDeputy, TeamTypes.Cutting, true)]
    [InlineData(Roles.TeamLeader, TeamTypes.Office, false)]
    [InlineData(Roles.Worker, TeamTypes.Sewing, false)]
    [InlineData(Roles.Electrician, null, false)]
    public void CanCreate_ByRoleAndTeamType_OnlyCuttingOrSewingLeaders(string role, string? teamType, bool expected)
    {
        RequestTypePolicies.All[RequestTypes.MachineRepair].CanCreate(role, teamType).Should().Be(expected);
    }

    [Fact]
    public async Task CreateAsync_SewingLeader_GoesStraightToElectrician()
    {
        SignIn(Roles.TeamLeader, TeamTypes.Sewing);

        var result = await Service().CreateAsync(Dto(), default);

        result.IsSuccess.Should().BeTrue();
        (_saved!.Status, _saved.CreatedByTeamId).Should().Be((RequestStatuses.PendingProcessing, 5));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Electrician, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithLines_ReturnsInvalid()
    {
        SignIn(Roles.TeamLeader, TeamTypes.Cutting);

        var result = await Service().CreateAsync(Dto(new[] { new RequestLineInput(null, null, null, "máy", 1) }), default);

        result.Message.Should().Be(Messages.MachineRepairNoLines);
        await _requests.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Theory]
    [InlineData(Roles.Electrician, true)]
    [InlineData(Roles.Admin, false)]
    [InlineData(Roles.TeamLeader, false)]
    public void IsHandler_MachineRepair_OnlyElectrician(string role, bool expected)
    {
        SignIn(role, null);

        new RequestAccess(_user).IsHandler(RequestTypes.MachineRepair).Should().Be(expected);
    }
}
