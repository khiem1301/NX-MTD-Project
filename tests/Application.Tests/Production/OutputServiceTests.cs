using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KI-04 + KI-11 (UC55, UC22): sản lượng cuối ca — đơn giá khóa, trần, hạn sửa, sửa sau hạn của Quản đốc.</summary>
public class OutputServiceTests : ProductionTestBase
{
    private const int TeamId = 5;
    private const int LineId = 11;
    private static readonly byte[] RowVer = { 1 };

    private readonly IPieceRateService _rates = Substitute.For<IPieceRateService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly List<OutputRecord> _records = [];
    private readonly List<WorkAllocation> _allocations = [];
    private readonly List<VwOperationAllocationProgress> _progress = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<Operation> _operations = [];
    private readonly List<Employee> _employees = [];
    private readonly List<OperationAssignment> _assignments = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<VwWorkerOutputCap> _caps = [];
    private readonly List<DailyTimesheet> _timesheets = [];
    private readonly List<AccountingPeriod> _periods = [];
    private readonly List<OrderLineTeam> _allotted = [];
    private readonly OutputService _sut;
    private long _nextId = 1;

    public OutputServiceTests()
    {
        SetupQueryable(_records);
        SetupQueryable(_lines);
        SetupQueryable(_operations);
        SetupQueryable(_employees);
        SetupQueryable(_assignments);
        SetupQueryable(_issueLines);
        SetupQueryable(_caps);
        SetupQueryable(_timesheets);
        SetupQueryable(_periods);
        SetupQueryable(_allocations);
        SetupQueryable(_progress);
        SetupQueryable(_allotted);
        _allotted.Add(new OrderLineTeam { OrderLineId = LineId, SewingTeamId = TeamId });
        Repo<WorkAllocation>().When(r => r.AddAsync(Arg.Any<WorkAllocation>())).Do(c => _allocations.Add(c.Arg<WorkAllocation>()));
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        AsTeamLeader(TeamId);

        Repo<OutputRecord>().When(r => r.AddAsync(Arg.Any<OutputRecord>())).Do(c =>
        {
            var record = c.Arg<OutputRecord>();
            record.OutputRecordId = _nextId++;
            _records.Add(record);
        });
        Repo<OutputRecord>().When(r => r.Remove(Arg.Any<OutputRecord>())).Do(c => _records.Remove(c.Arg<OutputRecord>()));

        _lines.Add(new OrderLine
        {
            OrderLineId = LineId, OrderId = 1, ProductId = 1, ProductVariant = TestVariant.Make(1),
            Order = new CustomerOrder { OrderId = 1, OrderCode = "DH001", Status = OrderStatuses.InProduction },
        });
        // Tổ đã nhận 1000 bộ BTP của dòng đơn (phiếu cấp BTP đã nhận, dòng bộ đủ).
        _issueLines.Add(new MaterialIssueLine
        {
            OrderLineId = LineId, IssuedQty = 1000, ConfirmedQty = 1000,
            MaterialIssue = new MaterialIssue { WarehouseType = WarehouseTypes.SemiFinished, Status = IssueStatuses.Received, ReceivingTeamId = TeamId },
        });
        AddOperation(1, "OP10");
        AddOperation(2, "OP20");
        AddWorker(7);
        AddWorker(8);
        // Mặc định mọi công nhân đã được giao 1000 lần ở cả hai công đoạn; từng test thu hẹp trần khi cần (vw_WorkerOutputCap).
        foreach (var worker in new[] { 7, 8 })
            foreach (var operation in new[] { 1, 2 })
                _caps.Add(new VwWorkerOutputCap { WorkerId = worker, OrderLineId = LineId, OperationId = operation, CapQty = 1000 });
        GivenRate(1, 100, 1_200m);
        GivenRate(2, 101, 800m);
        AttendanceFor(7, 8);

        _rates.FindEffectiveManyAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyDictionary<int, EffectivePieceRate>)_rateByOperation
                .Where(r => c.Arg<IReadOnlyCollection<int>>().Contains(r.Key)).ToDictionary(r => r.Key, r => r.Value));
        _sut = new OutputService(Uow, Guard(), _rates, Audit, _locks, Clock);
    }

    private Operation AddOperation(int id, string code, int productId = 1, string status = OperationStatuses.Active)
    {
        var op = new Operation { OperationId = id, ProductId = productId, OperationCode = code, OperationName = "CĐ " + code,
            StationSequence = (short)id, Status = status };
        _operations.Add(op);
        return op;
    }

    private Employee AddWorker(int id, int teamId = TeamId, string status = RecordStatus.Active)
    {
        var e = new Employee { EmployeeId = id, EmployeeCode = "CN" + id, FullName = "Công nhân " + id, TeamId = teamId, Status = status };
        _employees.Add(e);
        return e;
    }

    /// <summary>Phân công đang hiệu lực (kèm navigation Operation như EF sẽ nạp khi chiếu).</summary>
    private void Assign(int teamId, int operationId, int workerId, DateOnly? validTo = null) =>
        _assignments.Add(new OperationAssignment
        {
            SewingTeamId = teamId, OrderId = 1, OperationId = operationId, WorkerId = workerId, ValidTo = validTo,
            Operation = _operations.Single(o => o.OperationId == operationId),
        });

    /// <summary>Đơn giá duyệt hiện hành theo công đoạn; service đọc theo lô qua FindEffectiveManyAsync.</summary>
    private readonly Dictionary<int, EffectivePieceRate> _rateByOperation = [];

    private void GivenRate(int operationId, int pieceRateId, decimal rate) =>
        _rateByOperation[operationId] = new EffectivePieceRate(pieceRateId, rate);

    private void AttendanceFor(params int[] workerIds)
    {
        foreach (var id in workerIds)
            _timesheets.Add(new DailyTimesheet { EmployeeId = id, WorkDate = Today, CheckIn = Now.AddHours(-2) });
    }

    private OutputRecord Existing(int operationId, int workerId, int qty, decimal rate = 1_000m, DateOnly? date = null,
        DateTime? deadline = null, string shift = ShiftCodes.Morning)
    {
        var record = new OutputRecord
        {
            OutputRecordId = _nextId++, WorkDate = date ?? Today, ShiftCode = shift, SewingTeamId = TeamId, WorkerId = workerId,
            OperationId = operationId, OrderLineId = LineId, Quantity = qty, PieceRateId = 50, PieceRate = new PieceRate { PieceRateId = 50, Rate = rate },
            EditDeadline = deadline ?? Now.AddHours(20), Status = OutputStatuses.Saved, RowVer = RowVer,
        };
        _records.Add(record);
        return record;
    }

    private static SaveOutputDto Save(IReadOnlyList<OutputLineInput> rows, DateOnly? date = null, int? teamId = null,
        string? reason = null, string shift = "MORNING") =>
        new(date ?? Today, shift, LineId, teamId, reason, rows);

    private static OutputLineInput Row(int op, int worker, int qty, byte[]? rowVer = null) => new(op, worker, qty, rowVer);

    // ========== Thêm mới ==========

    [Fact]
    public async Task Save_NewRows_LockRateFlagOutsideAssignmentAndLogInsert()
    {
        Assign(TeamId, 1, 7);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 100), Row(2, 8, 40)]), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Inserted.Should().Be(2);
        var assigned = _records.Single(r => r.WorkerId == 7);
        assigned.PieceRateId.Should().Be(100);
        assigned.IsOutsideAssignment.Should().BeFalse();
        assigned.EditDeadline.Should().Be(new DateTime(2026, 10, 22, 23, 59, 59));
        assigned.Status.Should().Be(OutputStatuses.Saved);
        assigned.SewingTeamId.Should().Be(TeamId);
        _records.Single(r => r.WorkerId == 8).IsOutsideAssignment.Should().BeTrue();   // GBR-20: ngoài phân công vẫn nhập được
        Audit.Received(2).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(OutputRecord) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task Save_ShiftCodeInPascalCase_IsAccepted()
    {
        var result = await _sut.SaveAsync(Save([Row(1, 7, 10)], shift: "Morning"), default);

        result.IsSuccess.Should().BeTrue();
        _records.Single().ShiftCode.Should().Be(ShiftCodes.Morning);
    }

    [Fact]
    public async Task Save_RateNotApproved_IsBlockedWithMsg18()
    {
        _rateByOperation.Remove(1);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 100)]), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
        result.Message.Should().Contain("OP10");
        _records.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_NoAttendanceThatDay_WarnsButStillSaves()
    {
        _timesheets.Clear();
        AttendanceFor(8);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 10), Row(1, 8, 10)]), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Warnings.Should().ContainSingle().Which.Should().Contain("Công nhân 7");   // MSG17
        _records.Single(r => r.WorkerId == 7).NoAttendanceWarning.Should().BeTrue();
        _records.Single(r => r.WorkerId == 8).NoAttendanceWarning.Should().BeFalse();
    }

    // ========== Trần (MSG16) ==========

    [Fact]
    public async Task Save_ExceedsTheAllocatedQty_IsBlockedWithRemaining()
    {
        var result = await _sut.SaveAsync(Save([Row(1, 7, 1001)]), default);

        result.ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
        result.Message.Should().Contain("1000").And.Contain("OP10");
        _records.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_CapCountsTheSameWorkersOtherDaysOnSameOperationButNotOtherWorkers()
    {
        Existing(1, 8, 900, date: Today.AddDays(-1));   // người khác: không ảnh hưởng trần của công nhân 7
        Existing(1, 7, 600, date: Today.AddDays(-1));   // chính công nhân 7, ngày khác, cùng công đoạn

        var result = await _sut.SaveAsync(Save([Row(1, 7, 500)]), default);

        result.ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
        result.Message.Should().Contain("còn tối đa 400");
    }

    [Fact]
    public async Task Save_CapIsPerOperation_SoOtherOperationDoesNotConsumeIt()
    {
        Existing(2, 7, 900);   // công đoạn khác của cùng công nhân

        var result = await _sut.SaveAsync(Save([Row(1, 7, 1000)]), default);

        result.IsSuccess.Should().BeTrue();   // công đoạn 1 vẫn còn nguyên 1000
    }

    [Fact]
    public async Task Save_EditingExistingRow_DoesNotCountItselfAgainstTheCap()
    {
        Existing(1, 7, 900);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 1000, RowVer)]), default);

        result.IsSuccess.Should().BeTrue();
        _records.Single().Quantity.Should().Be(1000);
    }

    [Fact]
    public async Task Save_WorkerWithAllocation_IsLimitedByTheAllocatedQty()
    {
        _caps.RemoveAll(c => c.WorkerId == 7 && c.OperationId == 1);
        _caps.Add(new VwWorkerOutputCap { WorkerId = 7, OrderLineId = LineId, OperationId = 1, CapQty = 100 });

        var result = await _sut.SaveAsync(Save([Row(1, 7, 150)]), default);

        result.ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
        result.Message.Should().Contain("100");
    }

    [Fact]
    public async Task Save_WorkerWithAllocation_WithinCapIsAllowed()
    {
        _caps.RemoveAll(c => c.WorkerId == 7 && c.OperationId == 1);
        _caps.Add(new VwWorkerOutputCap { WorkerId = 7, OrderLineId = LineId, OperationId = 1, CapQty = 100 });

        (await _sut.SaveAsync(Save([Row(1, 7, 100)]), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Save_NothingAllocatedToTheWorkerYet_BlocksAnyOutput()
    {
        _caps.Clear();   // chưa được giao việc: trần bằng 0

        (await _sut.SaveAsync(Save([Row(1, 7, 1)]), default)).ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
    }

    // ========== DEV-ALL-64: chuyển phần báo vượt ==========

    /// <summary>Công đoạn 1: công nhân 7 được giao 100, chưa báo; công nhân 8 được giao <paramref name="coworkerCap"/>, đã báo 40.</summary>
    private void GivenSharedOperation(int coworkerCap, int pool = 0)
    {
        _caps.RemoveAll(c => c.OperationId == 1);
        _caps.Add(new VwWorkerOutputCap { WorkerId = 7, OrderLineId = LineId, OperationId = 1, CapQty = 100 });
        _caps.Add(new VwWorkerOutputCap { WorkerId = 8, OrderLineId = LineId, OperationId = 1, CapQty = coworkerCap, OutputQty = 40 });
        _progress.Add(new VwOperationAllocationProgress { SewingTeamId = TeamId, OrderLineId = LineId, OperationId = 1, UnallocatedQty = pool });
    }

    [Fact]
    public async Task SaveAsync_OverCapWithTransfer_MovesExcessFromCoworkerBeforeSaving()
    {
        GivenSharedOperation(coworkerCap: 100);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 130)]) with { TransferExcess = true }, default);

        result.IsSuccess.Should().BeTrue();
        _allocations.Select(a => (a.MovementType, a.WorkerId, a.Quantity, a.RetrievalReason)).Should().Equal(
            (PieceMovementTypes.Retrieval, 8, 30, RetrievalReasons.Adjusted),
            (PieceMovementTypes.Handover, 7, 30, (string?)null));
        _allocations.Should().OnlyContain(a => a.Status == HandoverStatuses.Confirmed && a.ConfirmMode == ConfirmModes.Leader);
        _records.Single().Quantity.Should().Be(130);
        result.Data!.Warnings.Should().Contain(w => w.Contains("30") && w.Contains("Công nhân 8"));
    }

    [Fact]
    public async Task SaveAsync_CoworkerSpareNotEnough_TakesTheRestFromTeamPool()
    {
        GivenSharedOperation(coworkerCap: 50, pool: 25);   // công nhân 8 còn dư 10

        var result = await _sut.SaveAsync(Save([Row(1, 7, 130)]) with { TransferExcess = true }, default);

        result.IsSuccess.Should().BeTrue();
        _allocations.Select(a => (a.MovementType, a.WorkerId, a.Quantity)).Should().Equal(
            (PieceMovementTypes.Retrieval, 8, 10), (PieceMovementTypes.Handover, 7, 30));
    }

    [Fact]
    public async Task SaveAsync_OverCapBeyondAllSources_ReturnsFailureAndSavesNothing()
    {
        GivenSharedOperation(coworkerCap: 50, pool: 5);   // chuyển được tối đa 15

        var result = await _sut.SaveAsync(Save([Row(1, 7, 130)]) with { TransferExcess = true }, default);

        result.ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
        result.Message.Should().Contain("15");
        _records.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_CoworkerReportingMoreInSameSave_IsNotUsedAsSource()
    {
        GivenSharedOperation(coworkerCap: 100, pool: 30);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 130), Row(1, 8, 60)]) with { TransferExcess = true }, default);

        result.IsSuccess.Should().BeTrue();
        // Công nhân 8 dư 60 nhưng đang báo thêm 60 nên không còn dư; lấy hết từ phần tổ chưa giao.
        _allocations.Select(a => (a.MovementType, a.WorkerId, a.Quantity)).Should().Equal((PieceMovementTypes.Handover, 7, 30));
    }

    [Fact]
    public async Task GetGridAsync_SharedOperation_ReturnsTransferableFromCoworkerAndPool()
    {
        GivenSharedOperation(coworkerCap: 100, pool: 25);

        var rows = (await _sut.GetGridAsync(Today, "MORNING", LineId, null, default)).Data!.Rows;

        rows.Single(r => r.OperationId == 1 && r.WorkerId == 7).TransferableQty.Should().Be(85);
        rows.Single(r => r.OperationId == 1 && r.WorkerId == 8).TransferableQty.Should().Be(125);
    }

    [Fact]
    public async Task GetDayGridAsync_SumsQuantitiesAcrossShifts_AndDisablesEdit()
    {
        Assign(TeamId, 1, 7);
        Existing(1, 7, 40, shift: ShiftCodes.Morning);
        Existing(1, 7, 25, shift: ShiftCodes.Afternoon);
        Existing(1, 7, 10, shift: ShiftCodes.Overtime);
        Existing(1, 8, 5, shift: ShiftCodes.Morning); // công nhân khác không cộng vào dòng của 7

        var result = await _sut.GetDayGridAsync(Today, LineId, null, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ShiftCode.Should().Be(ShiftCodes.AllDay);
        var row = result.Data.Rows.Single(r => r.OperationId == 1 && r.WorkerId == 7);
        row.Quantity.Should().Be(75);
        row.CanEdit.Should().BeFalse();
        row.OutputRecordId.Should().BeNull();
        row.RowVer.Should().BeNull();
        row.TransferableQty.Should().Be(0);
        result.Data.Rows.Should().OnlyContain(r => !r.CanEdit);
    }

    // ========== Sửa / xóa ==========

    [Fact]
    public async Task Save_UpdatingExistingRow_KeepsLockedRateAndLogsOldAndNewQuantity()
    {
        var record = Existing(1, 7, 100, rate: 1_000m);   // đã khóa 1.000, giá duyệt hiện hành là 1.200

        var result = await _sut.SaveAsync(Save([Row(1, 7, 150, RowVer)]), default);

        result.Data!.Updated.Should().Be(1);
        record.Quantity.Should().Be(150);
        record.PieceRate.Rate.Should().Be(1_000m);   // không đổi theo giá mới
        record.UpdatedAt.Should().Be(Now);
        Repo<OutputRecord>().Received(1).SetOriginalRowVersion(record, RowVer);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.RecordId == record.OutputRecordId
            && e.OldValues != null && e.NewValues != null));
    }

    [Fact]
    public async Task Save_QuantityZero_DeletesExistingRecordAndLogsDelete()
    {
        var record = Existing(1, 7, 100);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 0)]), default);

        result.Data!.Deleted.Should().Be(1);
        _records.Should().BeEmpty();
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Delete && e.RecordId == record.OutputRecordId));
    }

    [Fact]
    public async Task Save_QuantityZeroForMissingRow_IsIgnored()
    {
        var result = await _sut.SaveAsync(Save([Row(1, 7, 0)]), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Inserted.Should().Be(0);
        await Uow.DidNotReceive().SaveChangesAsync();
        _records.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_UnchangedQuantity_DoesNothing()
    {
        Existing(1, 7, 100);

        var result = await _sut.SaveAsync(Save([Row(1, 7, 100)]), default);

        result.Data!.Updated.Should().Be(0);
        await Uow.DidNotReceive().SaveChangesAsync();
        Audit.DidNotReceiveWithAnyArgs().Add(default!);
    }

    // ========== Hạn sửa (KI-11) ==========

    [Fact]
    public async Task Save_TeamLeaderEditingAfterDeadline_IsForbidden()
    {
        var record = Existing(1, 7, 100, deadline: Now.AddMinutes(-5));

        var result = await _sut.SaveAsync(Save([Row(1, 7, 90)], reason: "xin sửa"), default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductionEditDeadlinePassed);   // MSG20
        record.Quantity.Should().Be(100);
    }

    [Fact]
    public async Task Save_TeamLeaderAddingRowForOldDate_IsForbidden()
    {
        var old = Today.AddDays(-3);   // hạn của ngày đó đã qua từ lâu

        var result = await _sut.SaveAsync(Save([Row(1, 7, 10)], date: old), default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductionEditDeadlinePassed);
    }

    [Fact]
    public async Task Save_ManagerAfterDeadline_NeedsReason()
    {
        AsManager();
        Existing(1, 7, 100, deadline: Now.AddMinutes(-5));
        var service = new OutputService(Uow, Guard(), _rates, Audit, _locks, Clock);

        var result = await service.SaveAsync(Save([Row(1, 7, 90)], teamId: TeamId), default);

        result.Kind.Should().Be(ResultKind.Validation);
        _records.Single().Quantity.Should().Be(100);
    }

    [Fact]
    public async Task Save_ManagerAfterDeadlineWithReason_EditsAndLogsReason()
    {
        AsManager();
        var record = Existing(1, 7, 100, deadline: Now.AddMinutes(-5));
        var service = new OutputService(Uow, Guard(), _rates, Audit, _locks, Clock);

        var result = await service.SaveAsync(Save([Row(1, 7, 90)], teamId: TeamId, reason: "  Nhập nhầm số  "), default);

        result.IsSuccess.Should().BeTrue();
        record.Quantity.Should().Be(90);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.Reason == "Nhập nhầm số"));
    }

    [Fact]
    public async Task Save_ManagerWithoutChoosingTeam_IsRejected()
    {
        AsManager();
        var service = new OutputService(Uow, Guard(), _rates, Audit, _locks, Clock);

        (await service.SaveAsync(Save([Row(1, 7, 10)]), default)).IsSuccess.Should().BeFalse();
    }

    // ========== Kiểm tra đầu vào ==========

    [Fact]
    public async Task Save_FutureDate_IsRejected()
    {
        (await _sut.SaveAsync(Save([Row(1, 7, 10)], date: Today.AddDays(1)), default)).ErrorCode
            .Should().Be(ErrorCodes.OutputInvalid);
    }

    [Fact]
    public async Task Save_LockedPeriod_IsRejected()
    {
        _periods.Add(new AccountingPeriod
        {
            PeriodMonth = "2026-10", Status = RecordStatus.Locked, StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31),
        });

        var result = await _sut.SaveAsync(Save([Row(1, 7, 10)]), default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductionPeriodLocked);
        _records.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_InvalidRows_AreRejected()
    {
        (await _sut.SaveAsync(Save([]), default)).ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        (await _sut.SaveAsync(Save([Row(1, 7, -1)]), default)).ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        (await _sut.SaveAsync(Save([Row(1, 7, 1), Row(1, 7, 2)]), default)).ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        (await _sut.SaveAsync(Save([Row(1, 7, 1)], shift: "NIGHT"), default)).ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
    }

    [Fact]
    public async Task Save_WorkerFromAnotherTeam_IsRejected()
    {
        AddWorker(9, teamId: 6);

        (await _sut.SaveAsync(Save([Row(1, 9, 10)]), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
    }

    [Fact]
    public async Task Save_OperationOfAnotherProductOrInactive_IsRejected()
    {
        AddOperation(3, "OP30", productId: 2);
        AddOperation(4, "OP40", status: OperationStatuses.Discontinued);

        (await _sut.SaveAsync(Save([Row(3, 7, 10)]), default)).ErrorCode.Should().Be(ErrorCodes.OperationProductMismatch);
        (await _sut.SaveAsync(Save([Row(4, 7, 10)]), default)).ErrorCode.Should().Be(ErrorCodes.OperationNotActive);
    }

    [Fact]
    public async Task Save_ClosedOrder_IsRejected()
    {
        _lines[0].Order.Status = OrderStatuses.Delivered;

        (await _sut.SaveAsync(Save([Row(1, 7, 10)]), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentOrderInvalid);
    }

    // ========== Lưới nhập ==========

    [Fact]
    public async Task Grid_PrefillsFromAssignmentsAndMergesExistingRecords()
    {
        _caps.Clear();   // lưới chỉ tính phân công; trần giao việc mặc định của test bị bỏ
        Assign(TeamId, 1, 7);
        Assign(TeamId, 2, 7);
        Assign(TeamId, 1, 8,
            validTo: Today.AddDays(-1));   // phân công đã đóng: không nạp
        Assign(6, 1, 9);   // tổ khác
        var record = Existing(1, 7, 55);
        Existing(1, 8, 20).IsOutsideAssignment = true;   // đã nhập nhưng ngoài phân công (cờ lưu lúc nhập) → vẫn hiện

        var grid = (await _sut.GetGridAsync(Today, "MORNING", LineId, null, default)).Data!;

        grid.ReceivedFromWarehouse.Should().Be(1000);
        grid.SewingTeamId.Should().Be(TeamId);
        grid.Rows.Select(r => (r.OperationCode, r.WorkerId)).Should().Equal(("OP10", 7), ("OP10", 8), ("OP20", 7));
        var filled = grid.Rows.First(r => r.WorkerId == 7 && r.OperationCode == "OP10");
        filled.Quantity.Should().Be(55);
        filled.OutputRecordId.Should().Be(record.OutputRecordId);
        filled.UnitRate.Should().Be(1_000m);      // hiện đơn giá đã khóa của dòng đã nhập
        filled.IsOutsideAssignment.Should().BeFalse();
        grid.Rows.First(r => r.WorkerId == 8).IsOutsideAssignment.Should().BeTrue();
        var empty = grid.Rows.Single(r => r.OperationCode == "OP20");
        empty.Quantity.Should().BeNull();
        empty.UnitRate.Should().Be(800m);          // dòng chưa nhập hiện đơn giá duyệt hiện hành
        empty.RateApproved.Should().BeTrue();
    }

    [Fact]
    public async Task Grid_ReturnsOperationGroupName()
    {
        _caps.Clear();
        Assign(TeamId, 1, 7);
        Assign(TeamId, 2, 7);
        _operations.First(o => o.OperationId == 1).OperationGroup = new OperationGroup { GroupName = "May túi" };

        var rows = (await _sut.GetGridAsync(Today, "MORNING", LineId, null, default)).Data!.Rows;

        rows.Single(r => r.OperationId == 1).GroupName.Should().Be("May túi");
        rows.Single(r => r.OperationId == 2).GroupName.Should().BeNull();
    }

    [Fact]
    public async Task Grid_OperationWithoutApprovedRate_IsFlagged()
    {
        _caps.Clear();
        Assign(TeamId, 1, 7);
        _rateByOperation.Remove(1);

        var row = (await _sut.GetGridAsync(Today, "MORNING", LineId, null, default)).Data!.Rows.Single();

        row.RateApproved.Should().BeFalse();
        row.UnitRate.Should().BeNull();
    }

    [Fact]
    public async Task Grid_ExpiredRow_LeaderCannotEdit_ManagerCan()
    {
        _caps.Clear();
        Existing(1, 7, 55, deadline: Now.AddMinutes(-5));

        var leaderRow = (await _sut.GetGridAsync(Today, "MORNING", LineId, null, default)).Data!.Rows.Single();
        AsManager();
        var managerRow = (await new OutputService(Uow, Guard(), _rates, Audit, _locks, Clock)
            .GetGridAsync(Today, "MORNING", LineId, TeamId, default)).Data!.Rows.Single();

        leaderRow.CanEdit.Should().BeFalse();
        managerRow.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task Grid_UnknownOrderLine_IsNotFound()
    {
        (await _sut.GetGridAsync(Today, "MORNING", 999, null, default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task GetGridAsync_LineNotAllottedToTeam_ReturnsFailure()
    {
        _allotted.Clear();

        (await _sut.GetGridAsync(Today, "MORNING", LineId, null, default)).ErrorCode
            .Should().Be(ErrorCodes.AssignmentProductNotAllotted);
    }

    [Fact]
    public async Task SaveAsync_LineNotAllottedToTeam_ReturnsFailureAndSavesNothing()
    {
        _allotted.Clear();

        var result = await _sut.SaveAsync(Save([Row(1, 7, 10)]), default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);
        _records.Should().BeEmpty();
    }
}
