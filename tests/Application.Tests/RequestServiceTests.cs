using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Requests;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class RequestServiceTests
{
    private static readonly byte[] RowVer = { 1, 2, 3 };

    private readonly IRequestRepository _requests = Substitute.For<IRequestRepository>();
    private readonly IGenericRepository<RequestLine> _lines = Substitute.For<IGenericRepository<RequestLine>>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();

    public RequestServiceTests()
    {
        _clock.Now.Returns(new DateTime(2026, 10, 22, 8, 0, 0));
        _clock.Today.Returns(new DateOnly(2026, 10, 22));
        _user.EmployeeId.Returns(7);
        _user.IsAuthenticated.Returns(true);
    }

    private RequestService Create(params IRequestTypeHandler[] handlers) => new(_requests, _lines, _uow, _user, _clock,
        _audit, _notifier, new RequestAccess(_user), new RequestTypeHandlers(handlers));

    private void SignIn(string role, int? teamId = null, string? teamType = null)
    {
        _user.Role.Returns(role);
        _user.TeamId.Returns(teamId);
        _user.TeamTypeCode.Returns(teamType);
    }

    private static CreateRequestDto NewDto(string type) => new(type, "t", "nội dung", null, null, null, null, null, null,
        new[] { new RequestLineInput(null, null, null, null, "chân vịt", 2) });

    private static Request Existing(string type, string status, int createdBy = 7, int? team = null) => new()
    {
        RequestId = 1, RequestCode = "YC261022-001", RequestTypeCode = type, Status = status, CreatedBy = createdBy,
        CreatedByTeamId = team,
    };

    [Fact]
    public async Task Create_WorkerCreatesSpareParts_ReturnsForbidden()
    {
        SignIn(Roles.Worker);

        var result = await Create().CreateAsync(NewDto("SpareParts"), default);

        result.ErrorCode.Should().Be(ErrorCodes.RequestTypeNotAllowed);
        result.Kind.Should().Be(ResultKind.Forbidden);
        await _requests.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task Create_ElectricianSpareParts_IsPendingApprovalAndNotifiesAdmin()
    {
        SignIn(Roles.Electrician);
        Request? saved = null;
        _requests.When(x => x.AddAsync(Arg.Any<Request>())).Do(c => saved = c.Arg<Request>());

        var result = await Create().CreateAsync(NewDto("SpareParts"), default);

        result.IsSuccess.Should().BeTrue();
        saved!.Status.Should().Be(RequestStatuses.PendingApproval);
        saved.CreatedBy.Should().Be(7);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_MachineSupplies_IsPendingProcessingAndNotifiesAccountant()
    {
        SignIn(Roles.Electrician);
        Request? saved = null;
        _requests.When(x => x.AddAsync(Arg.Any<Request>())).Do(c => saved = c.Arg<Request>());

        await Create().CreateAsync(NewDto("MachineSupplies"), default);

        saved!.Status.Should().Be(RequestStatuses.PendingProcessing);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Accountant, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_CuttingLeaderSemiFinished_ReturnsForbidden()
    {
        SignIn(Roles.TeamLeader, 3, TeamTypes.Cutting);

        var result = await Create().CreateAsync(NewDto("SemiFinishedRequisition"), default);

        result.ErrorCode.Should().Be(ErrorCodes.RequestTypeNotAllowed);
    }

    [Fact]
    public async Task Create_SewingDeputyMaterialRequisition_SucceedsWithTeamId()
    {
        SignIn(Roles.TeamDeputy, 4, TeamTypes.Sewing);
        Request? saved = null;
        _requests.When(x => x.AddAsync(Arg.Any<Request>())).Do(c => saved = c.Arg<Request>());

        var result = await Create().CreateAsync(NewDto("MaterialRequisition"), default);

        result.IsSuccess.Should().BeTrue();
        saved!.CreatedByTeamId.Should().Be(4);
    }

    [Fact]
    public async Task Create_HandlerValidationFails_ReturnsHandlerErrorAndDoesNotSave()
    {
        SignIn(Roles.Electrician);
        var handler = Substitute.For<IRequestTypeHandler>();
        handler.RequestType.Returns(RequestTypes.SpareParts);
        handler.ValidateAsync(default!, default).ReturnsForAnyArgs(Result.Failure("X_ERR", "lỗi", ResultKind.Validation));

        var result = await Create(handler).CreateAsync(NewDto("SpareParts"), default);

        result.ErrorCode.Should().Be("X_ERR");
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_SecondRequestSameDay_IncrementsCode()
    {
        SignIn(Roles.Electrician);
        _requests.GetMaxCodeAsync("YC261022-", default).ReturnsForAnyArgs("YC261022-007");
        Request? saved = null;
        _requests.When(x => x.AddAsync(Arg.Any<Request>())).Do(c => saved = c.Arg<Request>());

        await Create().CreateAsync(NewDto("SpareParts"), default);

        saved!.RequestCode.Should().Be("YC261022-008");
    }

    [Fact]
    public async Task Approve_NotHandlerRole_ReturnsNotFound()
    {
        SignIn(Roles.Accountant);
        _requests.GetByIdAsync(1).Returns(Existing(RequestTypes.SpareParts, RequestStatuses.PendingApproval));

        var result = await Create().ApproveAsync(1, new ApproveRequestDto(null, RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task Approve_AlreadyRejected_ReturnsInvalidStatus()
    {
        SignIn(Roles.Admin);
        _requests.GetByIdAsync(1).Returns(Existing(RequestTypes.SpareParts, RequestStatuses.Rejected));

        var result = await Create().ApproveAsync(1, new ApproveRequestDto(null, RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.RequestInvalidStatus);
    }

    [Fact]
    public async Task Approve_TypeWithoutApproval_ReturnsInvalidStatus()
    {
        SignIn(Roles.Accountant);
        _requests.GetByIdAsync(1).Returns(Existing(RequestTypes.MachineSupplies, RequestStatuses.PendingProcessing));

        var result = await Create().ApproveAsync(1, new ApproveRequestDto(null, RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.RequestInvalidStatus);
    }

    [Fact]
    public async Task Reject_Pending_SetsDecisionLogsAndCommitsOnce()
    {
        SignIn(Roles.Admin);
        var request = Existing(RequestTypes.SpareParts, RequestStatuses.PendingApproval);
        _requests.GetByIdAsync(1).Returns(request);

        var result = await Create().RejectAsync(1, new RejectRequestDto("Dùng đồ dự phòng", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatuses.Rejected);
        request.DecisionNote.Should().Be("Dùng đồ dự phòng");
        request.DecidedAt.Should().Be(_clock.Now);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Reject && e.RecordId == 1));
        await _uow.Received(1).CommitTransactionAsync();
    }

    [Fact]
    public async Task Complete_HandlerDisallowsManual_ReturnsNotAllowed()
    {
        SignIn(Roles.WarehouseKeeper);
        _requests.GetByIdAsync(1).Returns(Existing(RequestTypes.MaterialRequisition, RequestStatuses.PendingProcessing));
        var handler = Substitute.For<IRequestTypeHandler>();
        handler.RequestType.Returns(RequestTypes.MaterialRequisition);
        handler.AllowManualComplete.Returns(false);

        var result = await Create(handler).CompleteAsync(1, new CompleteRequestDto(null, RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.RequestManualCompleteNotAllowed);
    }

    [Fact]
    public async Task Complete_HookFails_RollsBack()
    {
        SignIn(Roles.Accountant);
        _requests.GetByIdAsync(1).Returns(Existing(RequestTypes.MachineSupplies, RequestStatuses.PendingProcessing));
        var handler = Substitute.For<IRequestTypeHandler>();
        handler.RequestType.Returns(RequestTypes.MachineSupplies);
        handler.AllowManualComplete.Returns(true);
        handler.OnStatusChangedAsync(default!, default!, default).ReturnsForAnyArgs(Result.Failure("HOOK", "x"));

        var result = await Create(handler).CompleteAsync(1, new CompleteRequestDto(null, RowVer), default);

        result.ErrorCode.Should().Be("HOOK");
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task Cancel_OtherTeamLeader_ReturnsNotFound()
    {
        SignIn(Roles.TeamLeader, 3, TeamTypes.Sewing);
        _requests.GetByIdAsync(1).Returns(Existing(RequestTypes.MaterialSupplement, RequestStatuses.PendingApproval, 9, 2));

        var result = await Create().CancelAsync(1, new CancelRequestDto(null, RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task Update_Approved_ReturnsInvalidStatus()
    {
        SignIn(Roles.Electrician);
        _requests.GetWithLinesAsync(1, default).ReturnsForAnyArgs(Existing(RequestTypes.SpareParts, RequestStatuses.Approved));

        var result = await Create().UpdateAsync(1, new UpdateRequestDto("t", "c", null, null, RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.RequestInvalidStatus);
    }

    [Fact]
    public void RejectValidator_EmptyReason_HasMsg09()
    {
        var result = new RejectRequestDtoValidator().Validate(new RejectRequestDto("", RowVer));

        result.Errors.Should().Contain(e => e.ErrorMessage == Messages.ReasonRequired);
    }

    [Fact]
    public void CreateValidator_UnknownTypeOrEmptyContent_Fails()
    {
        var validator = new CreateRequestDtoValidator();

        validator.Validate(NewDto("Nope")).IsValid.Should().BeFalse();
        validator.Validate(NewDto("SpareParts") with { Content = "" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RequestTypes_AllRoundTripThroughPascal()
    {
        foreach (var code in RequestTypes.All)
            CodeText.ToCode(CodeText.ToPascal(code)).Should().Be(code);
    }
}
