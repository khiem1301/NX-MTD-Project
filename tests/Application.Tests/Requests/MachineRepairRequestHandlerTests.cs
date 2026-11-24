using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// UTCID932–UTCID945: MachineRepairRequestHandler (qua NoLineRequestHandler) và policy MACHINE_REPAIR.
/// Assert khóa theo sheet MachineRepairRequestHandler (P2-2) — không nới Expected cho Actual lệch spec.
/// Clock Today/Now = 2026-11-24.
/// </summary>
public class MachineRepairRequestHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 0, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 24);
    private const int SewingTeamId = 5;

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IRequestRepository _requests = Substitute.For<IRequestRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private Request? _saved;

    public MachineRepairRequestHandlerTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _user.EmployeeId.Returns(7);
        _user.IsAuthenticated.Returns(true);
        _requests.When(x => x.AddAsync(Arg.Any<Request>())).Do(c => _saved = c.Arg<Request>());

        var allotments = Substitute.For<IGenericRepository<OrderLineTeam>>();
        allotments.Query().Returns(_ => new List<OrderLineTeam>().AsTestAsyncQueryable());
        _uow.Repository<OrderLineTeam>().Returns(allotments);
    }

    private MachineRepairRequestHandler Handler() => new(_clock);

    private static RequestTypePolicy Policy() => RequestTypePolicies.All[RequestTypes.MachineRepair];

    private RequestService Service() => new(_requests, Substitute.For<IGenericRepository<RequestLine>>(),
        _uow, _user, _clock, Substitute.For<IAuditLogService>(), _notifier, new RequestAccess(_user),
        new RequestTypeHandlers(new IRequestTypeHandler[] { Handler() }));

    private void SignIn(string role, string? teamType, int? teamId = SewingTeamId)
    {
        _user.Role.Returns(role);
        _user.TeamTypeCode.Returns(teamType);
        _user.TeamId.Returns(teamId);
    }

    private static RequestDraft Draft(DateOnly? targetDate, int? requestId = null, IReadOnlyList<RequestLineInput>? lines = null) =>
        new(RequestTypes.MachineRepair, null, null, null, null, null, null, targetDate,
            "Máy may số 3 hỏng", "Máy không chạy kim", lines ?? Array.Empty<RequestLineInput>(), requestId);

    private static CreateRequestDto CreateDto() =>
        new("MachineRepair", "Máy may số 3 hỏng", "Máy không chạy kim", Today.AddDays(2),
            null, null, null, null, null, Array.Empty<RequestLineInput>());

    private static Request Repair(string? status) => new()
    {
        RequestId = 12,
        RequestCode = "YC261124-001",
        RequestTypeCode = RequestTypes.MachineRepair,
        Status = status!,
        Title = "Máy may số 3 hỏng",
        Content = "Máy không chạy kim",
        TargetDate = Today.AddDays(2),
        CreatedAt = Now,
    };

    private static object Snapshot(Request request) => new
    {
        request.RequestId,
        request.RequestCode,
        request.RequestTypeCode,
        request.Status,
        request.Title,
        request.Content,
        request.TargetDate,
        request.CreatedAt,
        request.UpdatedAt,
        request.CompletedAt,
        request.DecidedAt,
        request.ResultNote,
        request.DecisionNote,
        request.HandlerRoleCode,
        request.HandlerTeamId,
    };

    [Fact]
    public async Task UTCID932_ValidateAsync_CreateFutureTargetDate_Success()
    {
        // Arrange — Today=2026-11-24; create, Lines=[], TargetDate=2026-11-26
        var draft = Draft(new DateOnly(2026, 11, 26));

        // Act
        var result = await Handler().ValidateAsync(draft, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Kind.Should().Be(ResultKind.Ok);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task UTCID933_ValidateAsync_CreateTargetDateEqualsToday_Success()
    {
        // Arrange — create, Lines=[], TargetDate=2026-11-24 (= Today)
        var draft = Draft(Today);

        // Act
        var result = await Handler().ValidateAsync(draft, CancellationToken.None);

        // Assert — biên bằng Today vẫn hợp lệ (`<`, không `<=`)
        result.IsSuccess.Should().BeTrue();
        result.Kind.Should().Be(ResultKind.Ok);
    }

    [Fact]
    public async Task UTCID934_ValidateAsync_CreateTargetDateYesterday_FailsPastDate()
    {
        // Arrange — create, Lines=[], TargetDate=2026-11-23 (Today−1)
        var draft = Draft(Today.AddDays(-1));

        // Act
        var result = await Handler().ValidateAsync(draft, CancellationToken.None);

        // Assert — Failure message RequisitionTargetDatePast
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be(Messages.RequisitionTargetDatePast);
            result.ErrorCode.Should().Be(ErrorCodes.RequestLinesInvalid);
            result.Kind.Should().Be(ResultKind.Validation);
        }
    }

    [Fact]
    public async Task UTCID935_ValidateAsync_UpdatePastTargetDate_Success()
    {
        // Arrange — update {RequestId=5, Lines=[], TargetDate=2026-11-23}
        var draft = Draft(new DateOnly(2026, 11, 23), requestId: 5);

        // Act
        var result = await Handler().ValidateAsync(draft, CancellationToken.None);

        // Assert — update bỏ qua check ngày quá khứ
        result.IsSuccess.Should().BeTrue();
        result.Kind.Should().Be(ResultKind.Ok);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task UTCID936_ValidateAsync_CreateNullTargetDate_Success()
    {
        // Arrange — create, Lines=[], TargetDate=null
        var draft = Draft(targetDate: null);

        // Act
        var result = await Handler().ValidateAsync(draft, CancellationToken.None);

        // Assert — không check khi không có ngày cần
        result.IsSuccess.Should().BeTrue();
        result.Kind.Should().Be(ResultKind.Ok);
    }

    [Fact]
    public async Task UTCID937_ValidateAsync_CreateWithOneLine_FailsNoLines()
    {
        // Arrange — create, Lines=[1 dòng], TargetDate=Today+1
        var lines = new[] { new RequestLineInput(null, null, null, "máy", 1) };
        var draft = Draft(Today.AddDays(1), lines: lines);

        // Act
        var result = await Handler().ValidateAsync(draft, CancellationToken.None);

        // Assert — message MachineRepairNoLines, ErrorCode RequestLinesInvalid
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Message.Should().Be(Messages.MachineRepairNoLines);
            result.ErrorCode.Should().Be(ErrorCodes.RequestLinesInvalid);
            result.Kind.Should().Be(ResultKind.Validation);
        }
    }

    [Fact]
    public async Task UTCID938_ValidateAsync_NullDto_ArgumentNullOrFailure()
    {
        // Arrange
        var handler = Handler();

        // Act
        var (thrown, result) = await Capture(() => handler.ValidateAsync(null!, CancellationToken.None));

        // Assert — ArgumentNullException hoặc Failure. ⚠ Dereference → NullReferenceException.
        if (thrown is not null)
            thrown.Should().BeOfType<ArgumentNullException>();
        else
            result!.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID939_OnStatusChangedAsync_Completed_SuccessWithoutSideEffect()
    {
        // Arrange — Request MACHINE_REPAIR, status mới = Completed
        var request = Repair(RequestStatuses.Completed);
        var before = Snapshot(request);
        _clock.ClearReceivedCalls();

        // Act
        var result = await Handler().OnStatusChangedAsync(request, RequestStatuses.PendingProcessing, CancellationToken.None);

        // Assert — Success; không side-effect
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Kind.Should().Be(ResultKind.Ok);
            Snapshot(request).Should().BeEquivalentTo(before);
            _clock.ReceivedCalls().Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID940_OnStatusChangedAsync_RejectedOrCancelled_Success()
    {
        // Arrange — status mới = Rejected / Cancelled
        var handler = Handler();

        // Act
        var rejected = await handler.OnStatusChangedAsync(Repair(RequestStatuses.Rejected), RequestStatuses.PendingProcessing, CancellationToken.None);
        var cancelled = await handler.OnStatusChangedAsync(Repair(RequestStatuses.Cancelled), RequestStatuses.PendingProcessing, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            rejected.IsSuccess.Should().BeTrue();
            rejected.Kind.Should().Be(ResultKind.Ok);
            cancelled.IsSuccess.Should().BeTrue();
            cancelled.Kind.Should().Be(ResultKind.Ok);
        }
    }

    [Fact]
    public async Task UTCID941_OnStatusChangedAsync_NullOrUnknownStatus_SuccessWithoutException()
    {
        // Arrange — status mới = null hoặc "FOO"
        var handler = Handler();

        // Act
        var nullStatus = await handler.OnStatusChangedAsync(Repair(null), RequestStatuses.PendingProcessing, CancellationToken.None);
        var unknown = await handler.OnStatusChangedAsync(Repair("FOO"), RequestStatuses.PendingProcessing, CancellationToken.None);

        // Assert — handler không xử lý theo status; không exception
        using (new AssertionScope())
        {
            nullStatus.IsSuccess.Should().BeTrue();
            unknown.IsSuccess.Should().BeTrue();
            unknown.Kind.Should().Be(ResultKind.Ok);
        }
    }

    [Fact]
    public async Task UTCID942_Policy_CuttingTeamLeader_AllowedElectricianHandler()
    {
        // Arrange — Người tạo: Tổ trưởng tổ cắt
        SignIn(Roles.TeamLeader, TeamTypes.Cutting);
        var handler = Handler();
        var policy = Policy();

        // Act
        var result = await Service().CreateAsync(CreateDto(), CancellationToken.None);

        // Assert — được phép; người xử lý = Electrician; RequestType MACHINE_REPAIR; AllowManualComplete=true
        using (new AssertionScope())
        {
            policy.CanCreate(Roles.TeamLeader, TeamTypes.Cutting).Should().BeTrue();
            policy.HandlerRole.Should().Be(Roles.Electrician);
            handler.RequestType.Should().Be(RequestTypes.MachineRepair);
            handler.AllowManualComplete.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            _saved!.RequestTypeCode.Should().Be(RequestTypes.MachineRepair);
            _notifier.ReceivedCalls().Should().ContainSingle();
        }

        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Electrician, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID943_Policy_SewingTeamLeader_AllowedElectricianHandler()
    {
        // Arrange — Người tạo: Tổ trưởng tổ may
        SignIn(Roles.TeamLeader, TeamTypes.Sewing);

        // Act
        var result = await Service().CreateAsync(CreateDto(), CancellationToken.None);

        // Assert — được phép; người xử lý = Electrician
        using (new AssertionScope())
        {
            Policy().CanCreate(Roles.TeamLeader, TeamTypes.Sewing).Should().BeTrue();
            Policy().HandlerRole.Should().Be(Roles.Electrician);
            result.IsSuccess.Should().BeTrue();
            _saved.Should().NotBeNull();
            _saved!.RequestTypeCode.Should().Be(RequestTypes.MachineRepair);
        }

        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Electrician, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID944_Policy_ActingLeaderOfSewing_AllowedLikeTeamLeader()
    {
        // Arrange — NV đang giữ quyền tổ trưởng tạm (acting leader) tổ may.
        // Trong policy, người đó là tổ phó (TeamDeputy): cùng tập Leaders với tổ trưởng, không đọc vw_TeamActingLeader.
        SignIn(Roles.TeamDeputy, TeamTypes.Sewing);

        // Act
        var result = await Service().CreateAsync(CreateDto(), CancellationToken.None);

        // Assert — được phép như tổ trưởng; người xử lý = Electrician
        using (new AssertionScope())
        {
            var policy = Policy();
            policy.CanCreate(Roles.TeamDeputy, TeamTypes.Sewing).Should().BeTrue();
            policy.CanCreate(Roles.TeamDeputy, TeamTypes.Sewing)
                .Should().Be(policy.CanCreate(Roles.TeamLeader, TeamTypes.Sewing));
            policy.HandlerRole.Should().Be(Roles.Electrician);
            result.IsSuccess.Should().BeTrue();
            _saved.Should().NotBeNull();
            _saved!.CreatedByTeamId.Should().Be(SewingTeamId);
        }

        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Electrician, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID945_Policy_Worker_Forbidden()
    {
        // Arrange — Người tạo: Worker
        SignIn(Roles.Worker, TeamTypes.Sewing);

        // Act
        var result = await Service().CreateAsync(CreateDto(), CancellationToken.None);

        // Assert — không được phép theo policy (Forbidden)
        using (new AssertionScope())
        {
            Policy().CanCreate(Roles.Worker, TeamTypes.Sewing).Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Forbidden);
            result.ErrorCode.Should().Be(ErrorCodes.RequestTypeNotAllowed);
            _saved.Should().BeNull();
        }

        await _requests.DidNotReceiveWithAnyArgs().AddAsync(default!);
        await _notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
    }

    private static async Task<(Exception? Thrown, Result? Result)> Capture(Func<Task<Result>> act)
    {
        try
        {
            return (null, await act());
        }
        catch (Exception ex)
        {
            return (ex, null);
        }
    }
}
