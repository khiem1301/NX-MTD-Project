using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-91: đơn Khác, người tạo chọn vai trò xử lý; gửi tổ trưởng thì tổ trưởng / tổ phó của tổ người tạo xử lý.</summary>
public class OtherRequestTests
{
    private readonly IRequestRepository _requests = Substitute.For<IRequestRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private Request? _saved;

    public OtherRequestTests()
    {
        _clock.Now.Returns(new DateTime(2026, 11, 11, 8, 0, 0));
        _clock.Today.Returns(new DateOnly(2026, 11, 11));
        _user.EmployeeId.Returns(7);
        _requests.When(x => x.AddAsync(Arg.Any<Request>())).Do(c => _saved = c.Arg<Request>());
    }

    private RequestService Service() => new(_requests, Substitute.For<IGenericRepository<RequestLine>>(), _uow, _user, _clock,
        Substitute.For<IAuditLogService>(), _notifier, new RequestAccess(_user),
        new RequestTypeHandlers(new IRequestTypeHandler[] { new OtherRequestHandler(_clock) }));

    private void SignIn(string role, int? teamId = null)
    {
        _user.Role.Returns(role);
        _user.TeamId.Returns(teamId);
    }

    private static CreateRequestDto Dto(string? handlerRole, IReadOnlyList<RequestLineInput>? lines = null) =>
        new("Other", "Xin nghỉ", "Xin nghỉ chiều nay", null, null, null, null, null, null, lines, handlerRole);

    [Fact]
    public async Task CreateAsync_WorkerSendsToTeamLeader_HandledByOwnTeamLeaders()
    {
        SignIn(Roles.Worker, teamId: 5);
        _requests.TeamLeaderIdsAsync(5, Arg.Any<CancellationToken>()).Returns(new[] { 9 });

        var result = await Service().CreateAsync(Dto(Roles.TeamLeader), default);

        result.IsSuccess.Should().BeTrue();
        (_saved!.HandlerRoleCode, _saved.HandlerTeamId, _saved.CreatedByTeamId, _saved.Status)
            .Should().Be((RoleCodes.TeamLeader, 5, (int?)null, RequestStatuses.PendingApproval));
        _notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Single() == 9), Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task CreateAsync_SendsToAccountant_NotifiesAccountantRole()
    {
        SignIn(Roles.WarehouseKeeper);

        (await Service().CreateAsync(Dto(Roles.Accountant), default)).IsSuccess.Should().BeTrue();

        (_saved!.HandlerRoleCode, _saved.HandlerTeamId).Should().Be((RoleCodes.Accountant, (int?)null));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Accountant, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_NoHandlerRole_ReturnsFieldError()
    {
        SignIn(Roles.Worker, teamId: 5);

        var result = await Service().CreateAsync(Dto(null), default);

        result.Message.Should().Be(Messages.RequestHandlerRoleRequired);
        result.Errors!.Should().ContainKey("handlerRole");
        await _requests.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Theory]
    [InlineData(Roles.Manager, Roles.Manager, null)]       // tự gửi cho vai trò của mình
    [InlineData(Roles.Admin, Roles.Manager, null)]         // Admin xử lý được đơn của Quản đốc
    [InlineData(Roles.Worker, Roles.TeamLeader, null)]     // công nhân chưa có tổ
    [InlineData(Roles.TeamDeputy, Roles.TeamLeader, 5)]    // tổ phó cùng quyền tổ trưởng
    [InlineData(Roles.Accountant, Roles.TeamLeader, 5)]    // vai trò không thuộc tổ, kể cả khi có TeamId
    [InlineData(Roles.Electrician, Roles.TeamLeader, 5)]
    [InlineData(Roles.Worker, "QaQc", 5)]                  // không có trong danh sách chọn
    public async Task CreateAsync_HandlerRoleNotChoosable_ReturnsInvalid(string role, string handler, int? team)
    {
        SignIn(role, team);

        (await Service().CreateAsync(Dto(handler), default)).Message.Should().Be(Messages.RequestHandlerRoleInvalid);
    }

    [Fact]
    public async Task CreateAsync_WithLines_ReturnsInvalid()
    {
        SignIn(Roles.Worker, teamId: 5);

        var result = await Service().CreateAsync(Dto(Roles.TeamLeader, new[] { new RequestLineInput(null, null, null, "x", 1) }), default);

        result.Message.Should().Be(Messages.OtherRequestNoLines);
    }

    [Theory]
    [InlineData(Roles.TeamLeader, 5, true)]
    [InlineData(Roles.TeamDeputy, 5, true)]
    [InlineData(Roles.TeamLeader, 6, false)]
    [InlineData(Roles.Accountant, null, false)]
    public void IsHandler_SentToTeamLeader_OnlyThatTeam(string role, int? team, bool expected)
    {
        SignIn(role, team);

        new RequestAccess(_user).IsHandler(RequestTypes.Other, new ChosenHandler(RoleCodes.TeamLeader, 5)).Should().Be(expected);
    }

    [Fact]
    public void IsHandler_SentToManager_AdminAndManagerHandle()
    {
        SignIn(Roles.Admin);
        new RequestAccess(_user).IsHandler(RequestTypes.Other, new ChosenHandler(RoleCodes.Manager, null)).Should().BeTrue();
        SignIn(Roles.Accountant);
        new RequestAccess(_user).IsHandler(RequestTypes.Other, new ChosenHandler(RoleCodes.Manager, null)).Should().BeFalse();
    }

    [Fact]
    public async Task ApproveAsync_LeaderOfAnotherTeam_ReturnsNotFound()
    {
        SignIn(Roles.TeamLeader, teamId: 6);
        _requests.GetByIdAsync(1).Returns(new Request
        {
            RequestId = 1, RequestTypeCode = RequestTypes.Other, Status = RequestStatuses.PendingApproval, CreatedBy = 7,
            HandlerRoleCode = RoleCodes.TeamLeader, HandlerTeamId = 5,
        });

        var result = await Service().ApproveAsync(1, new ApproveRequestDto(null, new byte[] { 1 }), default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public void ChoosableHandlerRoles_WorkerInTeam_IncludesTeamLeader()
    {
        SignIn(Roles.Worker, teamId: 5);

        new RequestAccess(_user).ChoosableHandlerRoles().Should()
            .Equal(Roles.Admin, Roles.Manager, Roles.Accountant, Roles.WarehouseKeeper, Roles.TeamLeader);
    }

    [Fact]
    public void ChoosableHandlerRoles_Accountant_NoTeamLeaderNorOwnRole()
    {
        SignIn(Roles.Accountant, teamId: 5);

        new RequestAccess(_user).ChoosableHandlerRoles().Should().Equal(Roles.Admin, Roles.Manager, Roles.WarehouseKeeper);
    }
}
