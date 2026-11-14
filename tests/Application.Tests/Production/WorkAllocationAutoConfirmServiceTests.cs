using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-101: phiếu giao việc chờ công nhân quá 60 phút thì hệ thống tự xác nhận đủ số.</summary>
public class WorkAllocationAutoConfirmServiceTests : ProductionTestBase
{
    private readonly List<WorkAllocation> _allocations = [];
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly WorkAllocationAutoConfirmService _sut;

    public WorkAllocationAutoConfirmServiceTests()
    {
        SetupQueryable(_allocations);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _sut = new WorkAllocationAutoConfirmService(Uow, Clock, _locks, Audit, Notifier,
            NullLogger<WorkAllocationAutoConfirmService>.Instance);
    }

    private WorkAllocation Add(int minutesAgo, string status = HandoverStatuses.Pending,
        string type = PieceMovementTypes.Handover, int workerId = 40)
    {
        var allocation = new WorkAllocation
        {
            WorkAllocationId = _allocations.Count + 1, WorkDate = Today, MovementType = type, SewingTeamId = 5, WorkerId = workerId,
            OrderLineId = 101, OperationId = 9, Quantity = 30, Status = status, ConfirmMode = ConfirmModes.Worker,
            CreatedAt = Now.AddMinutes(-minutesAgo),
            RetrievalReason = type == PieceMovementTypes.Retrieval ? RetrievalReasons.Adjusted : null,
        };
        _allocations.Add(allocation);
        return allocation;
    }

    [Fact]
    public async Task RunAsync_PendingHandoverPastOneHour_ConfirmsFullQuantityAsSystem()
    {
        var old = Add(61);

        var count = await _sut.RunAsync(default);

        count.Should().Be(1);
        (old.Status, old.ConfirmedQty, old.ConfirmMode, old.ConfirmedAt).Should()
            .Be((HandoverStatuses.Confirmed, (int?)30, ConfirmModes.System, (DateTime?)Now));
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.RecordId == old.WorkAllocationId));
        Notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Single() == 40), Arg.Any<NotificationMessage>());
        await _locks.Received(1).AcquireAsync("work-allocation:5:101", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_NotDueOrNotPendingHandover_LeavesThemUnchanged()
    {
        var exactly = Add(60);
        var fresh = Add(10);
        var retrieval = Add(120, type: PieceMovementTypes.Retrieval);
        var rejected = Add(120, status: HandoverStatuses.Rejected);

        var count = await _sut.RunAsync(default);

        count.Should().Be(0);
        new[] { exactly, fresh, retrieval }.Should().OnlyContain(a => a.Status == HandoverStatuses.Pending);
        rejected.Status.Should().Be(HandoverStatuses.Rejected);
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task RunAsync_SeveralWorkers_NotifiesEachWorkerOnce()
    {
        Add(90, workerId: 40);
        Add(90, workerId: 40);
        Add(90, workerId: 41);

        var count = await _sut.RunAsync(default);

        count.Should().Be(3);
        Notifier.Received(2).NotifyEmployees(Arg.Any<IEnumerable<int>>(), Arg.Any<NotificationMessage>());
    }
}
