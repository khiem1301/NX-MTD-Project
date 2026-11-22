using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID204–UTCID251: OutputService độ phức tạp cao (UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES.md).</summary>
public sealed class OutputServiceHighComplexityTests : ProductionTestBase
{
    private const int TeamId = 3;
    private const int OtherTeamId = 5;
    private const int LineId = 100;
    private const int LineNotAllotted = 101;
    private const int OrderId = 1;
    private const int ProductId = 1;
    private const int OtherProductId = 2;
    private const int Op10 = 10;
    private const int Op20 = 20;
    private const int Op30 = 30;
    private const int Op40 = 40;
    private const int OrphanOp = 777;
    private const int W1 = 1;
    private const int W2 = 2;
    private const int W3 = 3;
    private const int W4 = 4;
    private const int W5 = 5;
    private const int W9 = 9;

    private static readonly DateTime SpecNow = new(2026, 11, 22, 10, 0, 0);
    private static readonly DateOnly SpecToday = new(2026, 11, 22);
    private static readonly byte[] RowVerV1 = [1];
    private static readonly byte[] RowVerV2 = [2];
    private const string Ca1 = "Morning";

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
    private readonly Dictionary<int, EffectivePieceRate> _rateByOperation = [];
    private readonly OutputService _sut;
    private long _nextRecordId = 500;

    public OutputServiceHighComplexityTests()
    {
        Clock.Now.Returns(SpecNow);
        Clock.Today.Returns(SpecToday);

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

        Uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        Uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        Uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));

        Repo<OutputRecord>().When(r => r.AddAsync(Arg.Any<OutputRecord>())).Do(c =>
        {
            var record = c.Arg<OutputRecord>();
            if (record.OutputRecordId == 0) record.OutputRecordId = _nextRecordId++;
            _records.Add(record);
        });
        Repo<OutputRecord>().When(r => r.Remove(Arg.Any<OutputRecord>())).Do(c => _records.Remove(c.Arg<OutputRecord>()));
        Repo<WorkAllocation>().When(r => r.AddAsync(Arg.Any<WorkAllocation>())).Do(c => _allocations.Add(c.Arg<WorkAllocation>()));

        SeedBaseScenario();
        AsTeamLeader(TeamId);
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _rates.FindEffectiveManyAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyDictionary<int, EffectivePieceRate>)_rateByOperation
                .Where(r => c.Arg<IReadOnlyCollection<int>>().Contains(r.Key)).ToDictionary(r => r.Key, r => r.Value));

        _sut = new OutputService(Uow, Guard(), _rates, Audit, _locks, Clock);
    }

    private OutputService ManagerSut()
    {
        AsManager();
        return new OutputService(Uow, Guard(), _rates, Audit, _locks, Clock);
    }

    private void SeedBaseScenario()
    {
        _allotted.Clear();
        _allotted.Add(new OrderLineTeam { OrderLineId = LineId, SewingTeamId = TeamId });
        _lines.Clear();
        _lines.Add(new OrderLine
        {
            OrderLineId = LineId, OrderId = OrderId, ProductId = ProductId,
            ProductVariant = TestVariant.Make(ProductId),
            Order = new CustomerOrder { OrderId = OrderId, OrderCode = "DH2611", Status = OrderStatuses.InProduction, ReceivedDate = new DateOnly(2026, 11, 10) },
        });
        _lines.Add(new OrderLine
        {
            OrderLineId = LineNotAllotted, OrderId = OrderId, ProductId = ProductId,
            ProductVariant = TestVariant.Make(ProductId),
            Order = new CustomerOrder { OrderId = OrderId, OrderCode = "DH2611", Status = OrderStatuses.InProduction, ReceivedDate = new DateOnly(2026, 11, 10) },
        });

        _issueLines.Add(new MaterialIssueLine
        {
            OrderLineId = LineId, IssuedQty = 1000, ConfirmedQty = 1000,
            MaterialIssue = new MaterialIssue { WarehouseType = WarehouseTypes.SemiFinished, Status = IssueStatuses.Received, ReceivingTeamId = TeamId },
        });

        AddOperation(Op10, "OP10");
        AddOperation(Op20, "OP20");
        AddOperation(Op30, "OP30", status: OperationStatuses.Discontinued);
        AddOperation(Op40, "OP40", productId: OtherProductId);
        AddWorker(W1, "CN001", TeamId);
        AddWorker(W2, "CN002", TeamId);
        AddWorker(W3, "CN003", TeamId);
        AddWorker(W4, "CN004", TeamId);
        AddWorker(W5, "CN005", TeamId);
        AddWorker(W9, "CN009", OtherTeamId);

        GivenRate(Op10, 100, 1_500m);
        GivenRate(Op20, 101, 1_700m);
        AttendanceFor(W1, W2, W3, W4, W5);
    }

    private void ResetCapsForGrid()
    {
        _caps.Clear();
        SetCap(W1, Op10, cap: 50, output: 20);
        SetCap(W2, Op10, cap: 50, output: 0);
    }

    private void SetCap(int workerId, int operationId, int cap, int output = 0) =>
        _caps.Add(new VwWorkerOutputCap { WorkerId = workerId, OrderLineId = LineId, OperationId = operationId, CapQty = cap, OutputQty = output });

    private Operation AddOperation(int id, string code, int productId = ProductId, string status = OperationStatuses.Active)
    {
        var existing = _operations.FirstOrDefault(o => o.OperationId == id);
        if (existing is not null) return existing;
        var op = new Operation
        {
            OperationId = id, ProductId = productId, OperationCode = code, OperationName = "CĐ " + code,
            StationSequence = (short)id, Status = status,
        };
        _operations.Add(op);
        return op;
    }

    private Employee AddWorker(int id, string code, int teamId)
    {
        if (_employees.Any(e => e.EmployeeId == id)) return _employees.Single(e => e.EmployeeId == id);
        var e = new Employee { EmployeeId = id, EmployeeCode = code, FullName = "Công nhân " + id, TeamId = teamId, Status = RecordStatus.Active };
        _employees.Add(e);
        return e;
    }

    private void Assign(int operationId, int workerId, int teamId = TeamId, DateOnly? validTo = null) =>
        _assignments.Add(new OperationAssignment
        {
            SewingTeamId = teamId, OrderId = OrderId, OperationId = operationId, WorkerId = workerId, ValidTo = validTo,
            Operation = _operations.Single(o => o.OperationId == operationId),
        });

    private void GivenRate(int operationId, int pieceRateId, decimal rate) =>
        _rateByOperation[operationId] = new EffectivePieceRate(pieceRateId, rate);

    private void AttendanceFor(params int[] workerIds)
    {
        foreach (var id in workerIds)
            if (_timesheets.All(t => t.EmployeeId != id))
                _timesheets.Add(new DailyTimesheet { EmployeeId = id, WorkDate = SpecToday, CheckIn = SpecNow.AddHours(-2) });
    }

    private OutputRecord Existing(int operationId, int workerId, int qty, decimal rate = 1_500m, string shift = ShiftCodes.Morning,
        DateTime? deadline = null, byte[]? rowVer = null)
    {
        var record = new OutputRecord
        {
            OutputRecordId = _nextRecordId++, WorkDate = SpecToday, ShiftCode = shift, SewingTeamId = TeamId, WorkerId = workerId,
            OperationId = operationId, OrderLineId = LineId, Quantity = qty,
            PieceRateId = 50, PieceRate = new PieceRate { PieceRateId = 50, Rate = rate },
            EditDeadline = deadline ?? ProductionGuard.NextWorkingDayEnd(SpecToday), Status = OutputStatuses.Saved,
            RowVer = rowVer ?? RowVerV1,
        };
        _records.Add(record);
        return record;
    }

    private static SaveOutputDto SaveDto(IReadOnlyList<OutputLineInput> rows, DateOnly? date = null, int? teamId = null,
        string? reason = null, string shift = Ca1, bool transferExcess = false) =>
        new(date ?? SpecToday, shift, LineId, teamId, reason, rows, transferExcess);

    private static OutputLineInput Row(int op, int worker, int qty, byte[]? rowVer = null) => new(op, worker, qty, rowVer);


    [Fact]
    public async Task UTCID204_GetGridAsync_TeamLeader_ReturnsTwoRows()
    {

        // Arrange
        _assignments.Clear();
        _records.Clear();
        Assign(Op10, W1);
        Assign(Op10, W2);
        Existing(Op10, W1, 20);
        ResetCapsForGrid();

        // Act
        var result = await _sut.GetGridAsync(SpecToday, Ca1, LineId, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Should().HaveCount(2);
        var w1 = result.Data.Rows.Single(r => r.WorkerId == W1);
        w1.ReportedQty.Should().Be(20);
        w1.CapQty.Should().Be(50);
        w1.CanEdit.Should().BeTrue();
        await _rates.Received().FindEffectiveManyAsync(Arg.Any<IReadOnlyCollection<int>>(), SpecToday, Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task UTCID205_GetGridAsync_Manager_WithTeamId_ReturnsTeamGrid()
    {

        // Arrange
        _assignments.Clear();
        Assign(Op10, W1);
        ResetCapsForGrid();
        var sut = ManagerSut();

        // Act
        var result = await sut.GetGridAsync(SpecToday, Ca1, LineId, TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.SewingTeamId.Should().Be(TeamId);

    }

    [Fact]
    public async Task UTCID206_GetGridAsync_CapWithoutAssignment_IncludesRow()
    {

        // Arrange
        _assignments.Clear();
        _records.Clear();
        Assign(Op10, W1);
        Assign(Op10, W2);
        SetCap(W3, Op20, cap: 10);

        // Act
        var rows = (await _sut.GetGridAsync(SpecToday, Ca1, LineId, null, default)).Data!.Rows;

        // Assert
        rows.Should().Contain(r => r.WorkerId == W3 && r.OperationId == Op20);

    }

    [Fact]
    public async Task UTCID207_GetGridAsync_ZeroCap_ExcludesRow()
    {

        // Arrange
        _assignments.Clear();
        Assign(Op10, W1);
        SetCap(W4, Op20, cap: 0);

        // Act
        var rows = (await _sut.GetGridAsync(SpecToday, Ca1, LineId, null, default)).Data!.Rows;

        // Assert
        rows.Should().NotContain(r => r.WorkerId == W4 && r.OperationId == Op20);

    }

    [Fact]
    public async Task UTCID208_GetGridAsync_AtEditDeadline_CanEditTrue()
    {

        // Arrange
        _assignments.Clear();
        var deadline = ProductionGuard.NextWorkingDayEnd(SpecToday);
        Clock.Now.Returns(deadline);
        Existing(Op10, W1, 10, deadline: deadline);

        // Act
        var row = (await _sut.GetGridAsync(SpecToday, Ca1, LineId, null, default)).Data!.Rows.Single();

        // Assert
        row.CanEdit.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID209_GetGridAsync_AfterEditDeadline_CanEditFalse()
    {

        // Arrange
        var deadline = ProductionGuard.NextWorkingDayEnd(SpecToday);
        Clock.Now.Returns(deadline.AddSeconds(1));
        Existing(Op10, W1, 10, deadline: deadline);

        // Act
        var row = (await _sut.GetGridAsync(SpecToday, Ca1, LineId, null, default)).Data!.Rows.Single();

        // Assert
        row.CanEdit.Should().BeFalse();

    }

    [Fact]
    public async Task UTCID210_GetGridAsync_ManagerAfterDeadline_CanEditTrue()
    {

        // Arrange
        var deadline = ProductionGuard.NextWorkingDayEnd(SpecToday).AddSeconds(-1);
        Clock.Now.Returns(deadline.AddHours(48));
        Existing(Op10, W1, 10, deadline: deadline);
        var sut = ManagerSut();

        // Act
        var row = (await sut.GetGridAsync(SpecToday, Ca1, LineId, TeamId, default)).Data!.Rows.Single();

        // Assert
        row.CanEdit.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID211_GetGridAsync_InvalidShift_ReturnsValidationFailure()
    {

        // Arrange
        // Act
        var result = await _sut.GetGridAsync(SpecToday, "XYZ", LineId, null, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);

    }

    [Fact]
    public async Task UTCID212_GetGridAsync_UnknownOrderLine_ReturnsNotFound()
    {

        // Act
        var result = await _sut.GetGridAsync(SpecToday, Ca1, 999, null, default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);

    }

    [Fact]
    public async Task UTCID213_GetGridAsync_LineNotAllotted_ReturnsNotAllotted()
    {

        // Act
        var result = await _sut.GetGridAsync(SpecToday, Ca1, LineNotAllotted, null, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);

    }

    [Fact]
    public async Task UTCID214_GetGridAsync_ManagerWithoutTeam_ReturnsValidationFailure()
    {

        // Arrange
        var sut = ManagerSut();

        // Act
        var result = await sut.GetGridAsync(SpecToday, Ca1, LineId, null, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);

    }

    [Fact]
    public async Task UTCID215_GetGridAsync_OrphanOperationId_DoesNotThrow()
    {

        // Arrange
        Existing(OrphanOp, W1, 5);
        ResetCapsForGrid();

        // Act
        var act = async () => await _sut.GetGridAsync(SpecToday, Ca1, LineId, null, default);

        // Assert
        await act.Should().NotThrowAsync<KeyNotFoundException>();

    }

    [Fact]
    public async Task UTCID216_GetDayGridAsync_SumsShifts_ReturnsAllDay()
    {

        // Arrange
        _assignments.Clear();
        Assign(Op10, W1);
        Existing(Op10, W1, 10, shift: ShiftCodes.Morning);
        Existing(Op10, W1, 15, shift: ShiftCodes.Afternoon);

        // Act
        var result = await _sut.GetDayGridAsync(SpecToday, LineId, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ShiftCode.Should().Be(ShiftCodes.AllDay);
        var row = result.Data.Rows.Single(r => r.WorkerId == W1);
        row.Quantity.Should().Be(25);
        row.CanEdit.Should().BeFalse();

    }

    [Fact]
    public async Task UTCID217_GetDayGridAsync_AssignmentWithoutRecord_ShowsZeroQty()
    {

        // Arrange
        _assignments.Clear();
        Assign(Op10, W1);

        // Act
        var row = (await _sut.GetDayGridAsync(SpecToday, LineId, null, default)).Data!.Rows.Single();

        // Assert
        row.Quantity.Should().Be(0);

    }

    [Fact]
    public async Task UTCID218_GetDayGridAsync_DifferentShiftRates_ShowsWeightedAverage()
    {

        // Arrange
        Existing(Op10, W1, 10, rate: 1_500m, shift: ShiftCodes.Morning);
        Existing(Op10, W1, 15, rate: 1_700m, shift: ShiftCodes.Afternoon);

        // Act
        var row = (await _sut.GetDayGridAsync(SpecToday, LineId, null, default)).Data!.Rows.Single(r => r.WorkerId == W1);

        // Assert
        row.UnitRate.Should().Be(1_620m);

    }

    [Fact]
    public async Task UTCID219_GetDayGridAsync_LineNotAllotted_ReturnsFailure()
    {

        // Act
        var result = await _sut.GetDayGridAsync(SpecToday, LineNotAllotted, null, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);

    }

    [Fact]
    public async Task UTCID220_SaveAsync_NewRows_InsertsWithLockAndAudit()
    {

        // Arrange
        _records.Clear();
        _assignments.Clear();
        Assign(Op10, W1);
        Assign(Op10, W2);
        SetCap(W1, Op10, 50);
        SetCap(W2, Op10, 50);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 10), Row(Op10, W2, 12)]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Inserted.Should().Be(2);
        await _locks.DidNotReceive().AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await Uow.Received().CommitTransactionAsync();
        Audit.Received(2).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Insert));
        await Uow.Received().SaveChangesAsync();

    }

    [Fact]
    public async Task UTCID221_SaveAsync_UpdateExisting_UpdatesQuantity()
    {

        // Arrange
        _records.Clear();
        SetCap(W1, Op10, 100);
        Assign(Op10, W1);
        var record = Existing(Op10, W1, 10);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 15, RowVerV1)]), default);

        // Assert
        result.Data!.Updated.Should().Be(1);
        record.Quantity.Should().Be(15);

    }

    [Fact]
    public async Task UTCID222_SaveAsync_QuantityZero_DeletesRecord()
    {

        // Arrange
        var record = Existing(Op10, W1, 10);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 0, RowVerV1)]), default);

        // Assert
        result.Data!.Deleted.Should().Be(1);
        _records.Should().NotContain(record);
        Audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Delete));

    }

    [Fact]
    public async Task UTCID223_SaveAsync_TransferExcess_AppliesCapTransfer()
    {

        // Arrange
        _records.Clear();
        _allocations.Clear();
        SetCap(W1, Op10, 50);
        SetCap(W5, Op10, 50, output: 40);
        _progress.Add(new VwOperationAllocationProgress { SewingTeamId = TeamId, OrderLineId = LineId, OperationId = Op10, UnallocatedQty = 10 });
        Assign(Op10, W1);
        Assign(Op10, W5);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 55)], transferExcess: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _locks.Received(1).AcquireAsync($"work-allocation:{TeamId}:{LineId}", Arg.Any<CancellationToken>());
        result.Data!.Warnings.Should().NotBeEmpty();

    }

    [Fact]
    public async Task UTCID224_SaveAsync_MaxQtyBoundary_Succeeds()
    {

        // Arrange
        _records.Clear();
        SetCap(W1, Op10, OutputLimits.MaxQty);
        Assign(Op10, W1);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, OutputLimits.MaxQty)]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID225_SaveAsync_AboveMaxQty_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, OutputLimits.MaxQty + 1)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        await Uow.DidNotReceive().SaveChangesAsync();

    }

    [Fact]
    public async Task UTCID226_SaveAsync_ZeroQtyWithoutRecord_SkipsInsert()
    {

        // Arrange
        _records.Clear();

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 0)]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Inserted.Should().Be(0);
        await Uow.DidNotReceive().SaveChangesAsync();

    }

    [Fact]
    public async Task UTCID227_SaveAsync_NegativeQty_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, -1)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);

    }

    [Fact]
    public async Task UTCID228_SaveAsync_AtCapWithOtherShifts_Succeeds()
    {

        // Arrange
        _records.Clear();
        SetCap(W1, Op10, 50);
        Existing(Op10, W1, 30, shift: ShiftCodes.Afternoon);
        Assign(Op10, W1);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 20)]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID229_SaveAsync_ExceedsCap_ReturnsCapFailure()
    {

        // Arrange
        _records.Clear();
        SetCap(W1, Op10, 50);
        Existing(Op10, W1, 30, shift: ShiftCodes.Afternoon);
        Assign(Op10, W1);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 21)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
        _records.Count(r => r.ShiftCode == ShiftCodes.Morning && r.WorkerId == W1).Should().Be(0);

    }

    [Fact]
    public async Task UTCID230_SaveAsync_EmptyRows_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        await Uow.DidNotReceive().BeginTransactionAsync();

    }

    [Fact]
    public async Task UTCID231_SaveAsync_FutureWorkDate_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 1)], date: SpecToday.AddDays(1)), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);

    }

    [Fact]
    public async Task UTCID232_SaveAsync_OnOrderReceivedDate_Succeeds()
    {

        // Arrange
        _records.Clear();
        SetCap(W1, Op10, 100);
        Assign(Op10, W1);
        var received = new DateOnly(2026, 11, 10);
        // Insert path kiểm edit window theo WorkDate — Clock phải trong cửa sổ của ngày nhận hàng
        Clock.Now.Returns(new DateTime(2026, 11, 10, 10, 0, 0));
        Clock.Today.Returns(received);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 5)], date: received), default);

        // Assert
        result.IsSuccess.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID233_SaveAsync_BeforeOrderStart_ReturnsFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 5)], date: new DateOnly(2026, 11, 9)), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);

    }

    [Fact]
    public async Task UTCID234_SaveAsync_TeamLeaderAtEditDeadline_Succeeds()
    {

        // Arrange
        var deadline = ProductionGuard.NextWorkingDayEnd(SpecToday);
        Clock.Now.Returns(deadline);
        SetCap(W1, Op10, 100);
        Assign(Op10, W1);
        var record = Existing(Op10, W1, 10, deadline: deadline);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 12, RowVerV1)]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.Quantity.Should().Be(12);

    }

    [Fact]
    public async Task UTCID235_SaveAsync_TeamLeaderAfterEditDeadline_ReturnsForbidden()
    {

        // Arrange
        var deadline = ProductionGuard.NextWorkingDayEnd(SpecToday);
        Clock.Now.Returns(deadline.AddSeconds(1));
        var record = Existing(Op10, W1, 10, deadline: deadline);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 12, RowVerV1)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.ProductionEditDeadlinePassed);
        record.Quantity.Should().Be(10);

    }

    [Fact]
    public async Task UTCID236_SaveAsync_ManagerAfterDeadlineWithReason_Succeeds()
    {

        // Arrange
        var deadline = SpecNow.AddMinutes(-5);
        var record = Existing(Op10, W1, 10, deadline: deadline);
        SetCap(W1, Op10, 100);
        var sut = ManagerSut();

        // Act
        var result = await sut.SaveAsync(SaveDto([Row(Op10, W1, 9, RowVerV1)], teamId: TeamId, reason: "Bổ sung sót"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.Quantity.Should().Be(9);
        Audit.Received().Add(Arg.Is<AuditEntry>(e => e.Reason == "Bổ sung sót"));

    }

    [Fact]
    public async Task UTCID237_SaveAsync_ManagerAfterDeadlineWithoutReason_ReturnsValidationFailure()
    {

        // Arrange
        var record = Existing(Op10, W1, 10, deadline: SpecNow.AddMinutes(-5));
        var sut = ManagerSut();

        // Act
        var result = await sut.SaveAsync(SaveDto([Row(Op10, W1, 9, RowVerV1)], teamId: TeamId), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.CommonValidation);
        record.Quantity.Should().Be(10);

    }

    [Fact]
    public async Task UTCID238_SaveAsync_OmittedRow_NotDeleted()
    {

        // Arrange
        SetCap(W1, Op10, 100);
        SetCap(W2, Op10, 100);
        Assign(Op10, W1);
        Assign(Op10, W2);
        Existing(Op10, W1, 10);
        Existing(Op10, W2, 12);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 11, RowVerV1)]), default);

        // Assert
        result.Data!.Deleted.Should().Be(0);
        _records.Should().Contain(r => r.WorkerId == W2 && r.Quantity == 12);

    }

    [Fact]
    public async Task UTCID239_SaveAsync_DuplicateRowKeys_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 5), Row(Op10, W1, 7)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputInvalid);

    }

    [Fact]
    public async Task UTCID240_SaveAsync_PeriodLocked_NoSave()
    {

        // Arrange
        _periods.Add(new AccountingPeriod
        {
            PeriodMonth = "2026-11", Status = RecordStatus.Locked,
            StartDate = new DateOnly(2026, 11, 1), EndDate = new DateOnly(2026, 11, 30),
        });

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 5)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.ProductionPeriodLocked);
        await Uow.DidNotReceive().SaveChangesAsync();

    }

    [Fact]
    public async Task UTCID241_SaveAsync_ClosedOrder_ReturnsFailure()
    {

        // Arrange
        _lines[0].Order!.Status = OrderStatuses.Delivered;

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 5)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.AssignmentOrderInvalid);

    }

    [Fact]
    public async Task UTCID242_SaveAsync_NewRowWithoutApprovedRate_ReturnsFailure()
    {

        // Arrange
        _records.Clear();
        _rateByOperation.Remove(Op10);
        SetCap(W1, Op10, 50);
        Assign(Op10, W1);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 5)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
        _records.Should().BeEmpty();

    }

    [Fact]
    public async Task UTCID243_SaveAsync_UpdateWithoutRateReapproval_Succeeds()
    {

        // Arrange
        SetCap(W1, Op10, 100);
        Assign(Op10, W1);
        _rateByOperation.Remove(Op10);
        var record = Existing(Op10, W1, 10);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 12, RowVerV1)]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        record.Quantity.Should().Be(12);

    }

    [Fact]
    public async Task UTCID244_SaveAsync_DiscontinuedOperation_ReturnsFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op30, W1, 5)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OperationNotActive);

    }

    [Fact]
    public async Task UTCID245_SaveAsync_OperationWrongProduct_ReturnsFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op40, W1, 5)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OperationProductMismatch);

    }

    [Fact]
    public async Task UTCID246_SaveAsync_WorkerWrongTeam_ReturnsFailure()
    {

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W9, 5)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);

    }

    [Fact]
    public async Task UTCID247_SaveAsync_RowVersionMismatch_ReturnsConcurrencyFailure()
    {

        // Arrange
        SetCap(W1, Op10, 100);
        Assign(Op10, W1);
        var record = Existing(Op10, W1, 10, rowVer: RowVerV2);
        Repo<OutputRecord>().When(r => r.SetOriginalRowVersion(record, RowVerV1))
            .Do(_ => throw new DbUpdateConcurrencyException());

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 12, RowVerV1)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.CommonConcurrencyConflict);
        await Uow.Received().RollbackTransactionAsync();

    }

    [Fact]
    public async Task UTCID248_SaveAsync_TransferExcessFails_RollsBackWithoutRecords()
    {

        // Arrange
        _records.Clear();
        SetCap(W1, Op10, 50);
        Assign(Op10, W1);

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 55)], transferExcess: true), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
        await Uow.Received().RollbackTransactionAsync();
        _records.Should().BeEmpty();

    }

    [Fact]
    public async Task UTCID249_SaveAsync_LockTimeout_NoSaveChanges()
    {

        // Arrange — qty > cap để vào nhánh Shortages → AcquireAsync (giống UTCID223)
        _records.Clear();
        _allocations.Clear();
        SetCap(W1, Op10, 50);
        SetCap(W5, Op10, 50, output: 40);
        _progress.Add(new VwOperationAllocationProgress { SewingTeamId = TeamId, OrderLineId = LineId, OperationId = Op10, UnallocatedQty = 10 });
        Assign(Op10, W1);
        Assign(Op10, W5);
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Lock timeout"));

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 55)], transferExcess: true), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamOrderBusy);
        await Uow.DidNotReceive().SaveChangesAsync();

    }

    [Fact]
    public async Task UTCID250_SaveAsync_SqlCapTrigger_MapsToCapExceeded()
    {

        // Arrange
        _records.Clear();
        SetCap(W1, Op10, 100);
        Assign(Op10, W1);
        Uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new DbUpdateException("Sản lượng vượt số lần được giao cho công nhân ở công đoạn này."));

        // Act
        var result = await _sut.SaveAsync(SaveDto([Row(Op10, W1, 10)]), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OutputExceedsCap);
        await Uow.Received().RollbackTransactionAsync();

    }

    [Fact]
    public async Task UTCID251_SaveAsync_NullDto_ThrowsOrValidationFailure()
    {

        // Act
        Func<Task> act = async () => await _sut.SaveAsync(null!, default);

        // Assert
        await act.Should().ThrowAsync<Exception>();

    }

}
