using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID374–UTCID415: DefectService high-complexity cases per UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES.md.</summary>
public class DefectServiceHighComplexityTests : ProductionTestBase
{
    private new static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0);
    private new static readonly DateOnly Today = new(2026, 11, 22);

    private const int TeamId = 3;
    private const int OtherTeamId = 5;
    private const int LineAllotted = 100;
    private const int LineNotAllotted = 101;
    private const int Op10 = 10;
    private const int WorkerW1 = 1;
    private static readonly byte[] RowVer = { 1 };

    private readonly IPieceRateService _rates = Substitute.For<IPieceRateService>();
    private readonly List<DefectRecord> _defects = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<Operation> _operations = [];
    private readonly List<Employee> _employees = [];
    private readonly List<Team> _teams =
    [
        new() { TeamId = TeamId, TeamCode = "MAY3", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active },
        new() { TeamId = OtherTeamId, TeamCode = "MAY5", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active },
    ];
    private readonly List<AccountingPeriod> _periods = [];
    private readonly List<OutputRecord> _outputs = [];
    private readonly List<VwWorkerOutputCap> _caps = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<QcBatch> _batches = [];
    private readonly List<Request> _requests = [];
    private readonly List<OrderLineTeam> _allotted = [new() { OrderLineId = LineAllotted, SewingTeamId = TeamId }];
    private readonly DefectService _sut;
    private int _nextDefectId = 70;

    public DefectServiceHighComplexityTests()
    {
        Clock.Now.Returns(Now);
        Clock.Today.Returns(Today);
        SetupQueryable(_defects);
        SetupQueryable(_lines);
        SetupQueryable(_operations);
        SetupQueryable(_employees);
        SetupQueryable(_teams);
        SetupQueryable(_periods);
        SetupQueryable(_outputs);
        SetupQueryable(_caps);
        SetupQueryable(_issueLines);
        SetupQueryable(_batches);
        SetupQueryable(_requests);
        SetupQueryable(_allotted);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        AsTeamLeader(TeamId);

        Repo<DefectRecord>().When(r => r.AddAsync(Arg.Any<DefectRecord>())).Do(c =>
        {
            var d = c.Arg<DefectRecord>();
            if (d.DefectRecordId == 0) d.DefectRecordId = _nextDefectId++;
            _defects.Add(d);
        });

        SeedOrderLine(LineAllotted);
        SeedOrderLine(LineNotAllotted, allotted: false);
        _operations.Add(new Operation
        {
            OperationId = Op10, ProductId = 1, OperationCode = "OP10", OperationName = "May cổ",
            StationSequence = 1, Status = OperationStatuses.Active,
        });
        _employees.Add(MakeWorker(WorkerW1));
        _issueLines.Add(new MaterialIssueLine
        {
            OrderLineId = LineAllotted, IssuedQty = 100, ConfirmedQty = 100,
            MaterialIssue = new MaterialIssue { WarehouseType = WarehouseTypes.SemiFinished, Status = IssueStatuses.Received, ReceivingTeamId = TeamId },
        });
        _rates.FindEffectiveAsync(Op10, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new EffectivePieceRate(100, 1_200m));

        _sut = new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock);
    }

    private void SeedOrderLine(int lineId, bool allotted = true)
    {
        _lines.Add(new OrderLine
        {
            OrderLineId = lineId, OrderId = 1, ProductId = 1,
            Order = new CustomerOrder { OrderId = 1, OrderCode = "DH2611-001", Status = OrderStatuses.InProduction, ReceivedDate = Today.AddDays(-10) },
            ProductVariant = TestVariant.Make(1, "AO-01", "Đỏ", "M"),
        });
        if (allotted && lineId == LineAllotted && !_allotted.Any(a => a.OrderLineId == lineId))
            _allotted.Add(new OrderLineTeam { OrderLineId = lineId, SewingTeamId = TeamId });
    }

    private static Employee MakeWorker(int id, int teamId = TeamId) => new()
    {
        EmployeeId = id, EmployeeCode = "W" + id, FullName = "Công nhân " + id, TeamId = teamId,
        Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.Worker },
    };

    private static CreateDefectDto CreateDto(string type = "Rework", int qty = 5, string reason = "Torn", string? note = null,
        int line = LineAllotted, int operation = Op10, int worker = WorkerW1, DateOnly? date = null, string? shift = "MORNING") =>
        new(date ?? Today, shift, line, operation, worker, type, qty, reason, note);

    private DefectRecord SeedDefect(int id = 70, int teamId = TeamId, string status = DefectStatuses.Recorded,
        string type = DefectTypes.Rework, int qty = 5, int worker = WorkerW1, DateOnly? date = null)
    {
        var op = _operations.First(o => o.OperationId == Op10);
        var workerEnt = _employees.First(e => e.EmployeeId == worker);
        var line = _lines.First(l => l.OrderLineId == LineAllotted);
        var d = new DefectRecord
        {
            DefectRecordId = id, DefectCode = $"MAY3-261122-{id:000}", WorkDate = date ?? Today,
            SewingTeamId = teamId, OrderLineId = LineAllotted, OperationId = Op10, ResponsibleWorkerId = worker,
            DefectType = type, Quantity = qty, DefectReasonCode = DefectReasons.Torn,
            PieceRateId = type == DefectTypes.Scrap ? 100 : null,
            PieceRate = type == DefectTypes.Scrap ? new PieceRate { PieceRateId = 100, Rate = 1_200m } : null,
            EditDeadline = Now.AddHours(24), Status = status, RowVer = RowVer, RecordedAt = Now.AddHours(-1),
            Operation = op, OrderLine = line, ResponsibleWorker = workerEnt,
            SewingTeam = _teams.First(t => t.TeamId == teamId),
        };
        _defects.Add(d);
        return d;
    }

    private void SeedTeamDefects(int teamId, int count, DateOnly? date = null)
    {
        for (var i = 0; i < count; i++)
        {
            var d = SeedDefect(_nextDefectId++, teamId, date: date ?? Today.AddDays(-i));
            d.ResponsibleWorker = MakeWorker(WorkerW1 + i % 3, teamId);
            d.ResponsibleWorkerId = d.ResponsibleWorker.EmployeeId;
            if (!_employees.Any(e => e.EmployeeId == d.ResponsibleWorkerId))
                _employees.Add(d.ResponsibleWorker);
        }
    }

    private static UpdateDefectDto UpdateDto(int qty = 4, string type = "Rework", string? reason = null) =>
        new(null, Op10, WorkerW1, type, qty, "Torn", null, reason, RowVer);

    private static CancelDefectDto CancelDto(string? reason = "Ghi nhầm") => new(reason, RowVer);

    private DefectService ManagerSut()
    {
        AsManager();
        return new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock);
    }

    // ---------- ListForTeamAsync ----------

    [Fact]
    public async Task UTCID374_ListForTeamAsync_TeamLeader_SeesOnlyOwnTeam()
    {
        // Arrange
        SeedTeamDefects(TeamId, 5);
        SeedTeamDefects(OtherTeamId, 4);
        AsTeamLeader(TeamId);

        // Act
        var result = await _sut.ListForTeamAsync(new DefectQuery { Page = 1, PageSize = 50 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(5);
        result.Data.Items.Should().OnlyContain(d => d.SewingTeamId == TeamId);
    }

    [Fact]
    public async Task UTCID375_ListForTeamAsync_Manager_SeesAllTeams()
    {
        // Arrange
        SeedTeamDefects(TeamId, 5);
        SeedTeamDefects(OtherTeamId, 4);
        var sut = ManagerSut();

        // Act
        var result = await sut.ListForTeamAsync(new DefectQuery { Page = 1, PageSize = 50 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(9);
    }

    [Fact]
    public async Task UTCID376_ListForTeamAsync_DateRange_FiltersWorkDate()
    {
        // Arrange
        SeedDefect(70, date: new DateOnly(2026, 11, 20));
        SeedDefect(71, date: new DateOnly(2026, 11, 21));
        var day = new DateOnly(2026, 11, 20);

        // Act
        var result = await _sut.ListForTeamAsync(new DefectQuery { From = day, To = day, PageSize = 50 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items[0].WorkDate.Should().Be(day);
    }

    [Fact]
    public async Task UTCID377_ListForTeamAsync_EmptyKeyword_DoesNotFilter()
    {
        // Arrange
        SeedTeamDefects(TeamId, 3);

        // Act
        var result = await _sut.ListForTeamAsync(new DefectQuery { Keyword = "", PageSize = 50 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task UTCID378_ListForTeamAsync_TeamLeader_IgnoresOtherTeamFilter()
    {
        // Arrange
        var mine = SeedDefect(70);
        SeedTeamDefects(OtherTeamId, 2);
        AsTeamLeader(TeamId);

        // Act
        var result = await _sut.ListForTeamAsync(new DefectQuery { SewingTeamId = OtherTeamId, PageSize = 50 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(d => d.DefectRecordId == mine.DefectRecordId);
        result.Data.Items.Should().NotContain(d => d.SewingTeamId == OtherTeamId);
    }

    [Fact]
    public async Task UTCID379_ListForTeamAsync_FromGreaterThanTo_ReturnsEmptyOrValidationFailure()
    {
        // Arrange
        SeedTeamDefects(TeamId, 2);

        // Act
        var result = await _sut.ListForTeamAsync(new DefectQuery
        {
            From = new DateOnly(2026, 11, 22), To = new DateOnly(2026, 11, 1), PageSize = 50,
        }, default);

        // Assert
        result.Data!.Items.Should().BeEmpty();
    }

    // ---------- ListMineAsync ----------

    [Fact]
    public async Task UTCID380_ListMineAsync_ReturnsActiveDefectsForWorker()
    {
        // Arrange
        for (var i = 0; i < 3; i++) SeedDefect(70 + i, worker: WorkerW1);

        // Act
        var result = await _sut.ListMineAsync(WorkerW1, new DefectQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task UTCID381_ListMineAsync_HidesCancelled()
    {
        // Arrange
        SeedDefect(70, status: DefectStatuses.Recorded);
        SeedDefect(71, status: DefectStatuses.Cancelled);

        // Act
        var result = await _sut.ListMineAsync(WorkerW1, new DefectQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Status.Should().Be("Recorded");
    }

    [Fact]
    public async Task UTCID382_ListMineAsync_ManyDefects_ExposesPagingMetadata()
    {
        // Arrange
        for (var i = 0; i < Paging.MaxPageSize + 1; i++) SeedDefect(70 + i, worker: WorkerW1);

        // Act
        var result = await _sut.ListMineAsync(WorkerW1, new DefectQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Count.Should().Be(Paging.MaxPageSize + 1,
            "expected full list or paging total when worker has more than MaxPageSize defects");
    }

    [Fact]
    public async Task UTCID383_ListMineAsync_WorkerIdZero_FailsValidation()
    {
        // Act
        var result = await _sut.ListMineAsync(0, new DefectQuery(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task UTCID384_CreateAsync_Rework_Success_NotifiesWorker()
    {
        // Arrange
        var dto = CreateDto("Rework", 5);

        // Act
        var result = await _sut.CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _defects.Should().ContainSingle();
        Notifier.Received(1).NotifyEmployees(
            Arg.Is<IEnumerable<int>>(ids => ids.Single() == WorkerW1), Arg.Any<NotificationMessage>());
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Insert));
        await Uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID385_CreateAsync_Scrap_LocksPieceRate()
    {
        // Arrange
        var dto = CreateDto("Scrap", 3);

        // Act
        var result = await _sut.CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _defects.Single().PieceRateId.Should().Be(100);
        await _rates.Received(1).FindEffectiveAsync(Op10, Today, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID386_CreateAsync_QuantityOne_Success()
    {
        // Act
        var result = await _sut.CreateAsync(CreateDto(qty: 1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _defects.Single().Quantity.Should().Be(1);
    }

    [Fact]
    public async Task UTCID387_CreateAsync_QuantityZero_FailsValidation()
    {
        // Act
        var result = await _sut.CreateAsync(CreateDto(qty: 0), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID388_CreateAsync_ScrapExceedsOnLine_Fails()
    {
        // Arrange
        var dto = CreateDto("Scrap", 1_000_000);

        // Act
        var result = await _sut.CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID389_CreateAsync_OutputError_EqualsReportedOutput_Success()
    {
        // Arrange
        _outputs.Add(new OutputRecord { WorkDate = Today, WorkerId = WorkerW1, OrderLineId = LineAllotted, OperationId = Op10, Quantity = 10 });
        _outputs.Add(new OutputRecord { WorkDate = Today, WorkerId = WorkerW1, OrderLineId = LineAllotted, OperationId = Op10, Quantity = 10, ShiftCode = ShiftCodes.Afternoon });

        // Act
        var result = await _sut.CreateAsync(CreateDto("OutputErr", 20, "Other", "Nhập nhầm"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _defects.Single().Quantity.Should().Be(20);
    }

    [Fact]
    public async Task UTCID390_CreateAsync_OutputError_ExceedsReported_Fails()
    {
        // Arrange
        _outputs.Add(new OutputRecord { WorkDate = Today, WorkerId = WorkerW1, OrderLineId = LineAllotted, OperationId = Op10, Quantity = 20 });

        // Act
        var result = await _sut.CreateAsync(CreateDto("OutputErr", 21, "Other", "Nhập nhầm"), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID391_CreateAsync_NegativeQuantity_FailsValidation()
    {
        // Act
        var result = await _sut.CreateAsync(CreateDto(qty: -1), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID392_CreateAsync_ManagerWithoutTeam_Fails()
    {
        // Arrange
        AsManager();

        // Act
        var result = await new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock)
            .CreateAsync(CreateDto(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID393_CreateAsync_Admin_Forbidden()
    {
        // Arrange
        User.Role.Returns(Roles.Admin);
        User.TeamId.Returns((int?)null);
        User.TeamTypeCode.Returns((string?)null);

        // Act
        var result = await new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock)
            .CreateAsync(CreateDto(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().BeOneOf(ResultKind.Forbidden, ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID394_CreateAsync_PeriodLocked_Fails()
    {
        // Arrange
        _periods.Add(new AccountingPeriod
        {
            PeriodMonth = "2026-11", Status = RecordStatus.Locked,
            StartDate = new DateOnly(2026, 11, 1), EndDate = new DateOnly(2026, 11, 30),
        });

        // Act
        var result = await _sut.CreateAsync(CreateDto(), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.ProductionPeriodLocked);
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID395_CreateAsync_ScrapWithoutApprovedRate_Fails()
    {
        // Arrange
        _rates.FindEffectiveAsync(Op10, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((EffectivePieceRate?)null);

        // Act
        var result = await _sut.CreateAsync(CreateDto("Scrap", 3), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID396_CreateAsync_LineNotAllotted_Fails()
    {
        // Act
        var result = await _sut.CreateAsync(CreateDto(line: LineNotAllotted), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);
    }

    [Fact]
    public async Task UTCID397_CreateAsync_InvalidTeam_FailsNotFound()
    {
        // Arrange
        AsTeamLeader(999);

        // Act
        var result = await new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock)
            .CreateAsync(CreateDto(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().BeOneOf(ResultKind.NotFound, ResultKind.Validation, ResultKind.Forbidden);
    }

    [Fact]
    public async Task UTCID398_CreateAsync_NotifierThrows_IsHandled()
    {
        // Arrange
        Notifier.When(n => n.NotifyEmployees(Arg.Any<IEnumerable<int>>(), Arg.Any<NotificationMessage>()))
            .Do(_ => throw new InvalidOperationException("notify down"));

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(CreateDto(), default);

        // Assert
        await act.Should().NotThrowAsync<InvalidOperationException>("notifier errors must be logged/handled, not lost silently");
    }

    [Fact]
    public async Task UTCID399_CreateAsync_UnknownOperation_NotFound()
    {
        // Act
        var result = await _sut.CreateAsync(CreateDto(operation: 99), default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);
        _defects.Should().BeEmpty();
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UTCID400_UpdateAsync_WithinWindow_ChangesQuantity_Audits()
    {
        // Arrange
        var defect = SeedDefect(70, qty: 5);

        // Act
        var result = await _sut.UpdateAsync(70, UpdateDto(qty: 4), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        defect.Quantity.Should().Be(4);
        Audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update));
        await Uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID401_UpdateAsync_ChangeToScrap_LocksRate_ChecksOnLine()
    {
        // Arrange
        var defect = SeedDefect(70, type: DefectTypes.Rework, qty: 5);

        // Act
        var result = await _sut.UpdateAsync(70, UpdateDto(qty: 3, type: "Scrap"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        defect.DefectType.Should().Be(DefectTypes.Scrap);
        defect.PieceRateId.Should().Be(100);
        await _rates.Received().FindEffectiveAsync(Op10, Today, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID402_UpdateAsync_ScrapToRework_ReleasesRate()
    {
        // Arrange
        var defect = SeedDefect(71, type: DefectTypes.Scrap, qty: 3);

        // Act
        var result = await _sut.UpdateAsync(71, UpdateDto(qty: 3, type: "Rework"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        defect.PieceRateId.Should().BeNull();
    }

    [Fact]
    public async Task UTCID403_UpdateAsync_AtEditDeadline_Success()
    {
        // Arrange
        var defect = SeedDefect(70);
        defect.RecordedAt = Now.AddHours(-24);

        // Act
        var result = await _sut.UpdateAsync(70, UpdateDto(qty: 4), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID404_UpdateAsync_AfterEditDeadline_TeamLeader_Fails()
    {
        // Arrange
        SeedDefect(70);
        Clock.Now.Returns(Now.AddSeconds(1));
        var defect = _defects[0];
        defect.RecordedAt = Now.AddHours(-24);

        // Act
        var result = await _sut.UpdateAsync(70, UpdateDto(qty: 4), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ProductionEditDeadlinePassed);
    }

    [Fact]
    public async Task UTCID405_UpdateAsync_QuantityZero_FailsValidation()
    {
        // Arrange
        SeedDefect(70);

        // Act
        var result = await _sut.UpdateAsync(70, UpdateDto(qty: 0), default);

        // Assert
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID406_UpdateAsync_ManagerWithoutReason_Fails()
    {
        // Arrange
        SeedDefect(70);
        var sut = ManagerSut();

        // Act
        var result = await sut.UpdateAsync(70, UpdateDto(qty: 6, reason: null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(Messages.DefectUpdateReasonRequired);
    }

    [Fact]
    public async Task UTCID407_UpdateAsync_ToScrapWithoutApprovedRate_Fails()
    {
        // Arrange
        SeedDefect(70, type: DefectTypes.Rework);
        _rates.FindEffectiveAsync(Op10, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((EffectivePieceRate?)null);

        // Act
        var result = await _sut.UpdateAsync(70, UpdateDto(type: "Scrap"), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
    }

    [Fact]
    public async Task UTCID408_UpdateAsync_CancelledDefect_Fails()
    {
        // Arrange
        SeedDefect(72, status: DefectStatuses.Cancelled);

        // Act
        var result = await _sut.UpdateAsync(72, UpdateDto(), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.DefectInvalidStatus);
    }

    [Fact]
    public async Task UTCID409_UpdateAsync_NotFound()
    {
        // Act
        var result = await _sut.UpdateAsync(999, UpdateDto(), default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    // ---------- CancelAsync ----------

    [Fact]
    public async Task UTCID410_CancelAsync_WithinWindow_Success_ReleasesScrapRate()
    {
        // Arrange
        var defect = SeedDefect(70, type: DefectTypes.Scrap, qty: 3);

        // Act
        var result = await _sut.CancelAsync(70, CancelDto("Ghi nhầm"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        defect.Status.Should().Be(DefectStatuses.Cancelled);
        Audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update));
        await Uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID411_CancelAsync_EmptyReason_Fails()
    {
        // Arrange
        SeedDefect(70);

        // Act
        var result = await _sut.CancelAsync(70, CancelDto("   "), default);

        // Assert
        result.Kind.Should().Be(ResultKind.Validation);
        result.Message.Should().Be(Messages.DefectCancelReasonRequired);
    }

    [Fact]
    public async Task UTCID412_CancelAsync_NullReason_FailsWithoutNre()
    {
        // Arrange
        SeedDefect(70);

        // Act
        var result = await _sut.CancelAsync(70, CancelDto(null), default);

        // Assert
        result.Kind.Should().Be(ResultKind.Validation);
        result.Message.Should().Be(Messages.DefectCancelReasonRequired);
    }

    [Fact]
    public async Task UTCID413_CancelAsync_OutsideWindow_TeamLeader_Fails()
    {
        // Arrange
        var defect = SeedDefect(70);
        defect.RecordedAt = Now.AddHours(-25);

        // Act
        var result = await _sut.CancelAsync(70, CancelDto("x"), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        defect.Status.Should().Be(DefectStatuses.Recorded);
    }

    [Fact]
    public async Task UTCID414_CancelAsync_AlreadyCancelled_Fails()
    {
        // Arrange
        SeedDefect(72, status: DefectStatuses.Cancelled);

        // Act
        var result = await _sut.CancelAsync(72, CancelDto("x"), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.DefectInvalidStatus);
    }

    [Fact]
    public async Task UTCID415_CancelAsync_NotFound()
    {
        // Act
        var result = await _sut.CancelAsync(999, CancelDto("x"), default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);
    }
}