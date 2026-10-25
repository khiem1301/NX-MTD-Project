using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KI-08 (UC56): hàng hỏng Rework / Scrap — khóa đơn giá Scrap, báo công nhân, hủy trong hạn, phản hồi của công nhân.</summary>
public class DefectServiceTests : ProductionTestBase
{
    private const int TeamId = 5;
    private const int LineId = 11;
    private static readonly byte[] RowVer = { 1 };

    private readonly IPieceRateService _rates = Substitute.For<IPieceRateService>();
    private readonly List<DefectRecord> _defects = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<Operation> _operations = [];
    private readonly List<Employee> _employees = [];
    private readonly List<Team> _teams = [new() { TeamId = TeamId, TeamCode = "MAY1", TeamName = "Tổ may 1", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active }];
    private readonly List<AccountingPeriod> _periods = [];
    private readonly DefectService _sut;
    private int _nextId = 1;

    public DefectServiceTests()
    {
        SetupQueryable(_defects);
        SetupQueryable(_lines);
        SetupQueryable(_operations);
        SetupQueryable(_employees);
        SetupQueryable(_teams);
        SetupQueryable(_periods);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        AsTeamLeader(TeamId);

        Repo<DefectRecord>().When(r => r.AddAsync(Arg.Any<DefectRecord>())).Do(c =>
        {
            var defect = c.Arg<DefectRecord>();
            defect.DefectRecordId = _nextId++;
            _defects.Add(defect);
        });

        _lines.Add(new OrderLine
        {
            OrderLineId = LineId, OrderId = 1, ProductId = 1,
            Order = new CustomerOrder { OrderId = 1, OrderCode = "DH001", Status = OrderStatuses.InProduction },
            ProductVariant = TestVariant.Make(1),
        });
        _operations.Add(new Operation { OperationId = 1, ProductId = 1, OperationCode = "OP10", OperationName = "May cổ",
            StationSequence = 1, Status = OperationStatuses.Active });
        _employees.Add(Worker(7));
        _rates.FindEffectiveAsync(1, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EffectivePieceRate(100, 1_200m));

        _sut = new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock);
    }

    private static Employee Worker(int id, int teamId = TeamId, string status = RecordStatus.Active, string role = RoleCodes.Worker) =>
        new() { EmployeeId = id, EmployeeCode = "CN" + id, FullName = "Công nhân " + id, TeamId = teamId, Status = status,
            Role = new Role { RoleCode = role } };

    private static CreateDefectDto Create(string type = "Scrap", int qty = 3, string reason = "Torn", string? note = null,
        int worker = 7, DateOnly? date = null, string? shift = "MORNING", int operation = 1) =>
        new(date ?? Today, shift, LineId, operation, worker, type, qty, reason, note);

    private DefectRecord Existing(int workerId = 7, string status = DefectStatuses.Recorded, DateTime? deadline = null,
        string type = DefectTypes.Scrap, int teamId = TeamId, DateOnly? date = null, string? code = null)
    {
        var defect = new DefectRecord
        {
            DefectRecordId = _nextId++, DefectCode = code ?? $"MAY1-261021-{_nextId:000}", WorkDate = date ?? Today,
            SewingTeamId = teamId, OrderLineId = LineId, OperationId = 1, ResponsibleWorkerId = workerId, DefectType = type,
            Quantity = 2, DefectReasonCode = DefectReasons.Torn, PieceRateId = type == DefectTypes.Scrap ? 50 : null,
            PieceRate = type == DefectTypes.Scrap ? new PieceRate { PieceRateId = 50, Rate = 1_000m } : null,
            EditDeadline = deadline ?? Now.AddHours(20), Status = status, RowVer = RowVer, RecordedAt = Now.AddHours(-1),
            Operation = _operations[0], OrderLine = _lines[0], ResponsibleWorker = _employees.First(e => e.EmployeeId == workerId),
        };
        _defects.Add(defect);
        return defect;
    }

    // ========== Ghi phiếu ==========

    [Fact]
    public async Task Create_Scrap_LocksApprovedRateNotifiesWorkerAndLogsInsert()
    {
        var result = await _sut.CreateAsync(Create(), default);

        result.IsSuccess.Should().BeTrue();
        var defect = _defects.Single();
        defect.DefectType.Should().Be(DefectTypes.Scrap);
        defect.PieceRateId.Should().Be(100);                 // khóa theo đơn giá duyệt tại ngày làm việc (1.200)
        defect.DefectReasonCode.Should().Be(DefectReasons.Torn);  // "Torn" PascalCase → mã DB
        defect.Status.Should().Be(DefectStatuses.Recorded);
        defect.SewingTeamId.Should().Be(TeamId);
        defect.RecordedBy.Should().Be(900);
        defect.WorkerNotifiedAt.Should().Be(Now);
        defect.EditDeadline.Should().Be(new DateTime(2026, 10, 22, 23, 59, 59));
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(DefectRecord) && e.Action == AuditActions.Insert));
        Notifier.Received(1).NotifyEmployees(
            Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 7 })),
            Arg.Is<NotificationMessage>(m => m.SourceType == NotificationSources.Production && m.Message.Contains("Scrap")));
    }

    [Fact]
    public async Task Create_Rework_NeedsNoRateAndHasNoScrapRate()
    {
        _rates.FindEffectiveAsync(1, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((EffectivePieceRate?)null);

        var result = await _sut.CreateAsync(Create(type: "Rework"), default);

        result.IsSuccess.Should().BeTrue();
        _defects.Single().PieceRateId.Should().BeNull();
    }

    [Fact]
    public async Task Create_ScrapWithoutApprovedRate_IsBlockedWithMsg18()
    {
        _rates.FindEffectiveAsync(1, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((EffectivePieceRate?)null);

        var result = await _sut.CreateAsync(Create(), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ReasonOther_RequiresNote()
    {
        (await _sut.CreateAsync(Create(reason: "Other", note: " "), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalid);

        (await _sut.CreateAsync(Create(reason: "Other", note: "Cháy do bàn là"), default)).IsSuccess.Should().BeTrue();
        _defects.Single().ReasonNote.Should().Be("Cháy do bàn là");
    }

    [Fact]
    public async Task Create_CodeIsTeamDateAndNextSequence()
    {
        Existing(code: "MAY1-261021-001");
        Existing(code: "MAY1-261021-004");
        Existing(code: "MAY1-261020-009");   // ngày khác: không tính

        await _sut.CreateAsync(Create(), default);

        _defects.Last().DefectCode.Should().Be("MAY1-261021-005");
    }

    [Fact]
    public async Task Create_WorkerFromAnotherTeamOrLeft_IsRejected()
    {
        _employees.Add(Worker(8, teamId: 6));
        _employees.Add(Worker(9, status: RecordStatus.Resigned));

        (await _sut.CreateAsync(Create(worker: 8), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
        (await _sut.CreateAsync(Create(worker: 9), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
    }

    [Fact]
    public async Task Create_InvalidInput_IsRejected()
    {
        (await _sut.CreateAsync(Create(type: "Burn"), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalid);
        (await _sut.CreateAsync(Create(reason: "Boredom"), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalid);
        (await _sut.CreateAsync(Create(qty: 0), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalid);
        (await _sut.CreateAsync(Create(date: Today.AddDays(1)), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalid);
        (await _sut.CreateAsync(Create(shift: "NIGHT"), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalid);
    }

    [Fact]
    public async Task Create_OperationOfOtherProductOrInactive_IsRejected()
    {
        _operations.Add(new Operation { OperationId = 2, ProductId = 2, OperationCode = "OP20", Status = OperationStatuses.Active });
        _operations.Add(new Operation { OperationId = 3, ProductId = 1, OperationCode = "OP30", Status = OperationStatuses.Discontinued });

        (await _sut.CreateAsync(Create(operation: 2), default)).ErrorCode.Should().Be(ErrorCodes.OperationProductMismatch);
        (await _sut.CreateAsync(Create(operation: 3), default)).ErrorCode.Should().Be(ErrorCodes.OperationNotActive);
    }

    [Fact]
    public async Task Create_ByManagerOrCuttingTeam_IsRejected()
    {
        AsManager();
        (await new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock).CreateAsync(Create(), default))
            .IsSuccess.Should().BeFalse();

        AsCuttingTeamLeader();
        (await new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock).CreateAsync(Create(), default))
            .Kind.Should().Be(ResultKind.Forbidden);
    }

    [Fact]
    public async Task Create_LockedPeriod_IsRejected()
    {
        _periods.Add(new AccountingPeriod
        {
            PeriodMonth = "2026-10", Status = RecordStatus.Locked, StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31),
        });

        (await _sut.CreateAsync(Create(), default)).ErrorCode.Should().Be(ErrorCodes.ProductionPeriodLocked);
    }

    // ========== Hủy phiếu ==========

    [Fact]
    public async Task Cancel_WithinDeadline_MarksCancelledAndLogs()
    {
        var defect = Existing();

        var result = await _sut.CancelAsync(defect.DefectRecordId, new CancelDefectDto(null, RowVer), default);

        result.IsSuccess.Should().BeTrue();
        defect.Status.Should().Be(DefectStatuses.Cancelled);
        defect.UpdatedAt.Should().Be(Now);
        Repo<DefectRecord>().Received(1).SetOriginalRowVersion(defect, RowVer);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.RecordId == defect.DefectRecordId));
    }

    [Fact]
    public async Task Cancel_AfterDeadline_LeaderForbiddenManagerNeedsReason()
    {
        var defect = Existing(deadline: Now.AddMinutes(-5));

        (await _sut.CancelAsync(defect.DefectRecordId, new CancelDefectDto("x", RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.ProductionEditDeadlinePassed);

        AsManager();
        var manager = new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock);
        (await manager.CancelAsync(defect.DefectRecordId, new CancelDefectDto(null, RowVer), default)).Kind
            .Should().Be(ResultKind.Validation);
        (await manager.CancelAsync(defect.DefectRecordId, new CancelDefectDto("Ghi nhầm công nhân", RowVer), default))
            .IsSuccess.Should().BeTrue();
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Reason == "Ghi nhầm công nhân"));
    }

    [Fact]
    public async Task Cancel_OtherTeamsDefectOrAlreadyCancelled_IsNotAllowed()
    {
        var other = Existing(teamId: 6);
        var done = Existing(status: DefectStatuses.Cancelled);

        (await _sut.CancelAsync(other.DefectRecordId, new CancelDefectDto(null, RowVer), default)).Kind.Should().Be(ResultKind.NotFound);
        (await _sut.CancelAsync(done.DefectRecordId, new CancelDefectDto(null, RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.DefectInvalidStatus);
    }

    // ========== Công nhân phản hồi ==========

    [Fact]
    public async Task Object_ByResponsibleWorker_StoresObjectionAndNotifiesLeadersAndDeputies()
    {
        var defect = Existing();
        _employees.Add(Worker(20, role: RoleCodes.TeamLeader));
        _employees.Add(Worker(21, role: RoleCodes.TeamDeputy));
        _employees.Add(Worker(22, teamId: 6, role: RoleCodes.TeamLeader));   // tổ khác: không nhận

        var result = await _sut.ObjectAsync(7, defect.DefectRecordId, new ObjectDefectDto("  Không phải lỗi của em ", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        defect.WorkerObjection.Should().Be("Không phải lỗi của em");
        defect.ObjectedAt.Should().Be(Now);
        Notifier.Received(1).NotifyEmployees(
            Arg.Is<IEnumerable<int>>(ids => ids.OrderBy(i => i).SequenceEqual(new[] { 20, 21 })),
            Arg.Is<NotificationMessage>(m => m.Message.Contains("Công nhân 7") && m.Message.Contains("Không phải lỗi của em")));
    }

    [Fact]
    public async Task Object_ByAnotherWorkerOrEmptyText_IsRejected()
    {
        var defect = Existing(workerId: 7);
        _employees.Add(Worker(8));

        (await _sut.ObjectAsync(8, defect.DefectRecordId, new ObjectDefectDto("x", RowVer), default)).Kind.Should().Be(ResultKind.NotFound);
        (await _sut.ObjectAsync(7, defect.DefectRecordId, new ObjectDefectDto("  ", RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.DefectInvalid);
        defect.WorkerObjection.Should().BeNull();
    }

    [Fact]
    public async Task Object_OnCancelledDefect_IsRejected()
    {
        var defect = Existing(status: DefectStatuses.Cancelled);

        (await _sut.ObjectAsync(7, defect.DefectRecordId, new ObjectDefectDto("x", RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.DefectInvalidStatus);
    }

    // ========== Xem ==========

    [Fact]
    public async Task ListMine_ReturnsOwnActiveDefectsPascalCaseNewestFirst()
    {
        Existing(workerId: 7, date: Today.AddDays(-2));
        Existing(workerId: 7, date: Today);
        Existing(workerId: 7, status: DefectStatuses.Cancelled);   // đã hủy: không hiện
        _employees.Add(Worker(8));
        Existing(workerId: 8);                                     // của người khác

        var result = await _sut.ListMineAsync(7, new DefectQuery(), default);

        result.Data!.Select(d => d.WorkDate).Should().Equal(Today, Today.AddDays(-2));
        result.Data[0].DefectType.Should().Be("Scrap");
        result.Data[0].DefectReasonCode.Should().Be("Torn");
        result.Data[0].Status.Should().Be("Recorded");
    }

    [Fact]
    public async Task ListForTeam_LeaderSeesOwnTeamOnly_ManagerSeesAll_CanEditFollowsDeadline()
    {
        Existing(teamId: TeamId, deadline: Now.AddHours(1));
        Existing(teamId: TeamId, deadline: Now.AddMinutes(-5));
        _employees.Add(Worker(8, teamId: 6));
        Existing(workerId: 8, teamId: 6);

        var leader = (await _sut.ListForTeamAsync(new DefectQuery(), default)).Data!.Items;
        AsManager();
        var manager = (await new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock)
            .ListForTeamAsync(new DefectQuery(), default)).Data!.Items;

        leader.Should().HaveCount(2);
        leader.Count(d => d.CanEdit).Should().Be(1);
        manager.Should().HaveCount(3);
        manager.Should().OnlyContain(d => d.CanEdit);
    }
}
