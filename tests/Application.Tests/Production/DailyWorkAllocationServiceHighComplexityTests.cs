using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// UTCID551–567: <see cref="DailyWorkAllocationService.SettleOnTeamLeaveAsync"/>.
/// Thu hồi ADJUSTED là CONFIRMED + SYSTEM (khác SaveAsync PENDING/WORKER).
/// </summary>
[Trait("Category", "HighComplexity")]
public class DailyWorkAllocationServiceHighComplexityTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateTime Now = new(2026, 11, 24);
    private static readonly DateOnly ClosedOn = new(2026, 11, 20);

    private const int TeamSewing = 3;
    private const int TeamCutting = 1;
    private const int Office1 = 5;
    private const int W1 = 1;
    private const int W2 = 2;
    private const int E4 = 4;
    private const int W5 = 5;
    private const int Op10 = 10;
    private const int Line100 = 100;
    private const int Line101 = 101;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IPieceRateService _rates = Substitute.For<IPieceRateService>();

    private readonly List<Team> _teams = new();
    private readonly List<WorkAllocation> _allocations = new();
    private readonly List<OperationAssignment> _assignments = new();
    private readonly List<OutputRecord> _outputs = new();
    private readonly List<string> _lockKeys = new();
    private string? _timeoutOnKey;
    private int _nextAllocationId = 1;
    private int _nextAssignmentId = 1;

    public DailyWorkAllocationServiceHighComplexityTests()
    {
        _clock.Today.Returns(Today);
        _clock.Now.Returns(Now);
        _uow.SaveChangesAsync().Returns(1);
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        _uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var key = call.Arg<string>();
                _lockKeys.Add(key);
                if (_timeoutOnKey is not null && key == _timeoutOnKey)
                    throw new TimeoutException("lock timeout");
                return Task.CompletedTask;
            });
        Setup(_teams);
        Setup(_allocations);
        Setup(_assignments);
        Setup(_outputs);
        _teams.Add(Team(TeamSewing, "MAY3", TeamTypes.Sewing));
    }

    [Fact]
    public async Task UTCID551_SettleOnTeamLeaveAsync_PendingAndRemaining_ConfirmsAdjustedAndClosesAssignment()
    {
        // Arrange — H1 PENDING, R1 PENDING, Remaining hiển thị = 10, OA1 còn mở
        var h1 = PendingHandover(W1, Line100, 4);
        var r1 = PendingRetrieval(W1, Line100, 10);
        ConfirmedHandover(W1, Line100, 20);
        var oa1 = OpenAssignment(W1, Op10);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert — ADJUSTED CONFIRMED/SYSTEM, không phải PENDING/WORKER như SaveAsync UTCID504
        result.IsSuccess.Should().BeTrue();
        _lockKeys.Should().Equal($"work-allocation:{TeamSewing}:{Line100}");
        h1.Status.Should().Be(HandoverStatuses.Rejected);
        h1.RejectReason.Should().Be(string.Format(WarehouseMessages.TeamLeavePendingWithdrawn, WarehouseMessages.TeamLeaveRetrieveNote));
        h1.ConfirmedAt.Should().Be(Now);
        AssertRetrievalConfirmed(r1, 10);
        var adjusted = SingleAdjusted();
        adjusted.Quantity.Should().Be(10);
        adjusted.ConfirmedQty.Should().Be(10);
        adjusted.OrderLineId.Should().Be(Line100);
        adjusted.WorkerId.Should().Be(W1);
        adjusted.OperationId.Should().Be(Op10);
        adjusted.Status.Should().Be(HandoverStatuses.Confirmed);
        adjusted.ConfirmMode.Should().Be(ConfirmModes.System);
        adjusted.RetrievalReason.Should().Be(RetrievalReasons.Adjusted);
        adjusted.RetrievalNote.Should().Be(WarehouseMessages.TeamLeaveRetrieveNote);
        adjusted.WorkDate.Should().Be(Today);
        adjusted.CreatedAt.Should().Be(Now);
        adjusted.ConfirmedAt.Should().Be(Now);
        oa1.ValidTo.Should().Be(Today);
        await _uow.Received(2).SaveChangesAsync();
        AssertNotifierSilent();
    }

    [Fact]
    public async Task UTCID552_SettleOnTeamLeaveAsync_PendingRetrieval_AdjustedDoesNotDoubleCount()
    {
        // Arrange — R1 PENDING qty=5; Remaining hiển thị = 20 − 5 = 15 (DEV-ALL-157)
        var r1 = PendingRetrieval(W1, Line100, 5);
        ConfirmedHandover(W1, Line100, 20);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert — R1=5 + ADJUSTED=15 = 20, đúng phần đang giữ
        result.IsSuccess.Should().BeTrue();
        AssertRetrievalConfirmed(r1, 5);
        var adjusted = SingleAdjusted();
        adjusted.Quantity.Should().Be(15);
        adjusted.ConfirmedQty.Should().Be(15);
        adjusted.Status.Should().Be(HandoverStatuses.Confirmed);
        adjusted.ConfirmMode.Should().Be(ConfirmModes.System);
        ConfirmedRetrievalQty().Should().Be(20);
    }

    [Fact]
    public async Task UTCID553_SettleOnTeamLeaveAsync_TwoLines_LocksAndSettlesEach()
    {
        // Arrange — line 100 còn 6, line 101 còn 7 sau khi chốt R1
        var h1 = PendingHandover(W1, Line100, 3);
        ConfirmedHandover(W1, Line100, 6);
        var r1 = PendingRetrieval(W1, Line101, 4);
        ConfirmedHandover(W1, Line101, 11);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _lockKeys.Should().Equal(
            $"work-allocation:{TeamSewing}:{Line100}",
            $"work-allocation:{TeamSewing}:{Line101}");
        h1.Status.Should().Be(HandoverStatuses.Rejected);
        AssertRetrievalConfirmed(r1, 4);
        AdjustedOn(Line100).Quantity.Should().Be(6);
        AdjustedOn(Line101).Quantity.Should().Be(7);
        AdjustedOn(Line100).Status.Should().Be(HandoverStatuses.Confirmed);
        AdjustedOn(Line101).ConfirmMode.Should().Be(ConfirmModes.System);
    }

    [Fact]
    public async Task UTCID554_SettleOnTeamLeaveAsync_EmptyWorkers_ReturnsWithoutWork()
    {
        // Arrange
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, Array.Empty<int>(), default);

        // Assert — trả ngay, không query, không lock, không SaveChanges
        result.IsSuccess.Should().BeTrue();
        _uow.ReceivedCalls().Should().BeEmpty();
        _locks.ReceivedCalls().Should().BeEmpty();
        _lockKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID555_SettleOnTeamLeaveAsync_DuplicateWorker_SettlesOnce()
    {
        // Arrange — cùng fixture Normal, workerIds lặp
        PendingHandover(W1, Line100, 4);
        PendingRetrieval(W1, Line100, 10);
        ConfirmedHandover(W1, Line100, 20);
        OpenAssignment(W1, Op10);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1, W1 }, default);

        // Assert — Distinct: một phiếu ADJUSTED, một lần khóa line
        result.IsSuccess.Should().BeTrue();
        SingleAdjusted().Quantity.Should().Be(10);
        _lockKeys.Should().Equal($"work-allocation:{TeamSewing}:{Line100}");
        await _locks.Received(1).AcquireAsync($"work-allocation:{TeamSewing}:{Line100}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID556_SettleOnTeamLeaveAsync_NoPending_SavesOnlyFinalRetrieval()
    {
        // Arrange — không HO/RT PENDING; Remaining = 5
        ConfirmedHandover(W1, Line100, 5);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert — bỏ SaveChanges của pending; SaveChanges cuối đúng 1 lần
        result.IsSuccess.Should().BeTrue();
        SingleAdjusted().Quantity.Should().Be(5);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID557_SettleOnTeamLeaveAsync_RemainingZero_DoesNotAddAdjusted()
    {
        // Arrange — đã giao 10 và đã báo 10 → Remaining = 0
        ConfirmedHandover(W1, Line100, 10);
        Output(W1, Line100, 10);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _allocations.Should().NotContain(a => a.RetrievalReason == RetrievalReasons.Adjusted);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID558_SettleOnTeamLeaveAsync_RemainingOne_AddsAdjustedQtyOne()
    {
        // Arrange — Remaining = 1
        ConfirmedHandover(W1, Line100, 1);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var adjusted = SingleAdjusted();
        adjusted.Quantity.Should().Be(1);
        adjusted.ConfirmedQty.Should().Be(1);
        adjusted.Status.Should().Be(HandoverStatuses.Confirmed);
        adjusted.ConfirmMode.Should().Be(ConfirmModes.System);
    }

    [Fact]
    public async Task UTCID559_SettleOnTeamLeaveAsync_ClosedAssignment_KeepsValidTo()
    {
        // Arrange — OA mở bị đóng; OA2 đã ValidTo=2026-11-20
        ConfirmedHandover(W1, Line100, 2);
        var open = OpenAssignment(W1, Op10);
        var oa2 = OpenAssignment(W1, Op10 + 1);
        oa2.ValidTo = ClosedOn;
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        open.ValidTo.Should().Be(Today);
        oa2.ValidTo.Should().Be(ClosedOn);
    }

    [Fact]
    public async Task UTCID560_SettleOnTeamLeaveAsync_NoAllocation_ClosesAssignmentWithoutLock()
    {
        // Arrange — W2 không có WorkAllocation; OA3 còn mở
        var oa3 = OpenAssignment(W2, Op10);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W2 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _lockKeys.Should().BeEmpty();
        await _locks.DidNotReceiveWithAnyArgs().AcquireAsync(default!, default);
        oa3.ValidTo.Should().Be(Today);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID561_SettleOnTeamLeaveAsync_UnknownTeam_SilentNoOp()
    {
        // Arrange — team 999 không có; phiếu sẵn để chứng minh không đụng DB
        var h1 = PendingHandover(W1, Line100, 4);
        h1.SewingTeamId = 999;
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(999, new[] { W1 }, default);

        // Assert — Success no-op im lặng
        result.IsSuccess.Should().BeTrue();
        h1.Status.Should().Be(HandoverStatuses.Pending);
        _lockKeys.Should().BeEmpty();
        await _uow.DidNotReceive().SaveChangesAsync();
        _allocations.Should().NotContain(a => a.RetrievalReason == RetrievalReasons.Adjusted);
    }

    [Fact]
    public async Task UTCID562_SettleOnTeamLeaveAsync_CuttingTeam_NoOp()
    {
        // Arrange — tổ 1 là tổ cắt
        _teams.Add(Team(TeamCutting, "CAT1", TeamTypes.Cutting));
        var h1 = PendingHandover(W1, Line100, 4);
        h1.SewingTeamId = TeamCutting;
        var oa = OpenAssignment(W1, Op10);
        oa.SewingTeamId = TeamCutting;
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamCutting, new[] { W1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        h1.Status.Should().Be(HandoverStatuses.Pending);
        oa.ValidTo.Should().BeNull();
        _lockKeys.Should().BeEmpty();
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID563_SettleOnTeamLeaveAsync_OfficeTeam_NoOp()
    {
        // Arrange — tổ văn phòng Office1, nhân viên E4
        _teams.Add(Team(Office1, "VP-1", TeamTypes.Office));
        var h1 = PendingHandover(E4, Line100, 4);
        h1.SewingTeamId = Office1;
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(Office1, new[] { E4 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        h1.Status.Should().Be(HandoverStatuses.Pending);
        _lockKeys.Should().BeEmpty();
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID564_SettleOnTeamLeaveAsync_LockTimeout_TeamOrderBusyRollsBack()
    {
        // Arrange — khóa 100 xong, khóa 101 timeout; caller bọc transaction
        var h1 = PendingHandover(W1, Line100, 3);
        ConfirmedHandover(W1, Line100, 6);
        var r1 = PendingRetrieval(W1, Line101, 4);
        ConfirmedHandover(W1, Line101, 11);
        var oa = OpenAssignment(W1, Op10);
        _timeoutOnKey = $"work-allocation:{TeamSewing}:{Line101}";
        ResetCalls();

        // Act
        var result = await _uow.InTransactionAsync(() => Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default));

        // Assert — Failure TeamOrderBusy; caller Rollback; không commit
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamOrderBusy);
        result.Message.Should().Be(Messages.OutputLockBusy);
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
        await _uow.DidNotReceive().SaveChangesAsync();
        _lockKeys.Should().Equal(
            $"work-allocation:{TeamSewing}:{Line100}",
            $"work-allocation:{TeamSewing}:{Line101}");
        h1.Status.Should().Be(HandoverStatuses.Pending);
        r1.Status.Should().Be(HandoverStatuses.Pending);
        oa.ValidTo.Should().BeNull();
        _allocations.Should().NotContain(a => a.RetrievalReason == RetrievalReasons.Adjusted);
    }

    [Fact]
    public async Task UTCID565_SettleOnTeamLeaveAsync_SecondSaveThrowsOutsideTx_PropagatesPartialCommit()
    {
        // Arrange — gọi ngoài TX; SaveChanges lần 2 ném
        var h1 = PendingHandover(W1, Line100, 4);
        var r1 = PendingRetrieval(W1, Line100, 10);
        ConfirmedHandover(W1, Line100, 20);
        var oa1 = OpenAssignment(W1, Op10);
        var saves = 0;
        _uow.SaveChangesAsync().Returns(_ =>
        {
            saves++;
            if (saves >= 2) throw new InvalidOperationException("second save failed");
            return Task.FromResult(1);
        });
        ResetCalls();

        // Act
        var act = async () => await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert — exception đi ra; H1/R1 đã đổi ở Save 1; không rollback
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("second save failed");
        saves.Should().Be(2);
        h1.Status.Should().Be(HandoverStatuses.Rejected);
        r1.Status.Should().Be(HandoverStatuses.Confirmed);
        r1.ConfirmMode.Should().Be(ConfirmModes.System);
        await _uow.Received(2).SaveChangesAsync();
        await _uow.DidNotReceive().BeginTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
        oa1.ValidTo.Should().Be(Today);
    }

    [Fact]
    public async Task UTCID566_SettleOnTeamLeaveAsync_PendingHandoverOfLeaver_DoesNotNotify()
    {
        // Arrange — H1 PENDING của W1 (người nhận phiếu). W5 là người liên quan đáng được báo.
        var h1 = PendingHandover(W1, Line100, 4);
        ResetCalls();

        // Act
        var result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, new[] { W1 }, default);

        // Assert — H1 bị từ chối; ⚠ không gọi notifier cho W5
        result.IsSuccess.Should().BeTrue();
        h1.Status.Should().Be(HandoverStatuses.Rejected);
        h1.RejectReason.Should().Be(string.Format(WarehouseMessages.TeamLeavePendingWithdrawn, WarehouseMessages.TeamLeaveRetrieveNote));
        _notifier.DidNotReceive().NotifyEmployees(
            Arg.Is<IEnumerable<int>>(ids => ids.Contains(W5)),
            Arg.Any<NotificationMessage>());
        AssertNotifierSilent();
    }

    [Fact]
    public async Task UTCID567_SettleOnTeamLeaveAsync_NullWorkerIds_ArgumentNullOrEmptySuccess()
    {
        // Arrange
        ResetCalls();

        // Act — spec: ArgumentNullException hoặc Success rỗng
        Result? result = null;
        Exception? error = null;
        try
        {
            result = await Sut().SettleOnTeamLeaveAsync(TeamSewing, null!, default);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // Assert
        if (error is null)
        {
            result!.IsSuccess.Should().BeTrue();
            _uow.ReceivedCalls().Should().BeEmpty();
            _locks.ReceivedCalls().Should().BeEmpty();
            return;
        }

        error.Should().BeOfType<ArgumentNullException>(
            "workerIds null phải ArgumentNullException hoặc Success rỗng, không ghi DB");
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    private DailyWorkAllocationService Sut() => new(_uow, _notifier, _locks, _clock, _rates);

    private void ResetCalls()
    {
        _lockKeys.Clear();
        _uow.ClearReceivedCalls();
        _locks.ClearReceivedCalls();
        _notifier.ClearReceivedCalls();
    }

    private void AssertNotifierSilent()
    {
        _notifier.DidNotReceiveWithAnyArgs().NotifyEmployees(default!, default!);
        _notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
        _notifier.DidNotReceiveWithAnyArgs().NotifyTeamLeadersAsync(default, default!, default);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        repo.AddAsync(Arg.Do<T>(backing.Add)).Returns(Task.CompletedTask);
        _uow.Repository<T>().Returns(repo);
    }

    private static Team Team(int id, string code, string type) =>
        new() { TeamId = id, TeamCode = code, TeamType = type, Status = RecordStatus.Active, CreatedAt = Now };

    private WorkAllocation ConfirmedHandover(int workerId, int lineId, int qty) =>
        AddAllocation(workerId, lineId, PieceMovementTypes.Handover, qty, HandoverStatuses.Confirmed, ConfirmModes.Leader, qty);

    private WorkAllocation PendingHandover(int workerId, int lineId, int qty) =>
        AddAllocation(workerId, lineId, PieceMovementTypes.Handover, qty, HandoverStatuses.Pending, ConfirmModes.Worker, null);

    private WorkAllocation PendingRetrieval(int workerId, int lineId, int qty) =>
        AddAllocation(workerId, lineId, PieceMovementTypes.Retrieval, qty, HandoverStatuses.Pending, ConfirmModes.Worker, null);

    private WorkAllocation AddAllocation(int workerId, int lineId, string movement, int qty, string status, string mode, int? confirmedQty)
    {
        var row = new WorkAllocation
        {
            WorkAllocationId = _nextAllocationId++,
            WorkDate = Today,
            MovementType = movement,
            SewingTeamId = TeamSewing,
            WorkerId = workerId,
            OrderLineId = lineId,
            OperationId = Op10,
            Quantity = qty,
            ConfirmedQty = confirmedQty,
            ConfirmMode = mode,
            Status = status,
            CreatedAt = Now,
        };
        _allocations.Add(row);
        return row;
    }

    private void Output(int workerId, int lineId, int qty) => _outputs.Add(new OutputRecord
    {
        WorkDate = Today,
        ShiftCode = "CA1",
        SewingTeamId = TeamSewing,
        WorkerId = workerId,
        OrderLineId = lineId,
        OperationId = Op10,
        Quantity = qty,
        Status = "SAVED",
    });

    private OperationAssignment OpenAssignment(int workerId, int operationId)
    {
        var row = new OperationAssignment
        {
            OperationAssignmentId = _nextAssignmentId++,
            SewingTeamId = TeamSewing,
            OrderId = 7,
            OperationId = operationId,
            WorkerId = workerId,
            ValidFrom = Today.AddDays(-7),
            ValidTo = null,
            AssignedAt = Now,
        };
        _assignments.Add(row);
        return row;
    }

    private WorkAllocation SingleAdjusted() =>
        _allocations.Should().ContainSingle(a => a.RetrievalReason == RetrievalReasons.Adjusted).Which;

    private WorkAllocation AdjustedOn(int lineId) =>
        _allocations.Single(a => a.RetrievalReason == RetrievalReasons.Adjusted && a.OrderLineId == lineId);

    private int ConfirmedRetrievalQty() =>
        _allocations.Where(a => a.MovementType == PieceMovementTypes.Retrieval && a.Status == HandoverStatuses.Confirmed)
            .Sum(a => a.ConfirmedQty ?? 0);

    private static void AssertRetrievalConfirmed(WorkAllocation row, int qty)
    {
        row.Status.Should().Be(HandoverStatuses.Confirmed);
        row.ConfirmMode.Should().Be(ConfirmModes.System);
        row.ConfirmedQty.Should().Be(qty);
        row.ConfirmedAt.Should().Be(Now);
        row.RetrievalNote.Should().Be(WarehouseMessages.TeamLeaveRetrieveNote);
    }
}
