using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KI-08 (UC56): hàng hỏng Rework / Scrap — khóa đơn giá Scrap, báo công nhân, sửa / hủy (tổ trưởng 24 giờ, Quản đốc theo ràng buộc), công nhân chỉ xem.</summary>
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
    private readonly List<OutputRecord> _outputs = [];
    private readonly List<VwWorkerOutputCap> _caps = [];
    private readonly List<Request> _requests = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<QcBatch> _batches = [];
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
        SetupQueryable(_outputs);
        SetupQueryable(_caps);
        SetupQueryable(_requests);
        SetupQueryable(_issueLines);
        SetupQueryable(_batches);
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
        // Tổ đã nhận 100 bộ BTP của dòng đơn: hàng trên chuyền = 100 − QC đạt − Scrap chưa hủy.
        _issueLines.Add(new MaterialIssueLine
        {
            OrderLineId = LineId, IssuedQty = 100, ConfirmedQty = 100,
            MaterialIssue = new MaterialIssue { WarehouseType = WarehouseTypes.SemiFinished, Status = IssueStatuses.Received, ReceivingTeamId = TeamId },
        });
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
            SewingTeam = _teams.FirstOrDefault(t => t.TeamId == teamId) ?? new Team { TeamId = teamId, TeamCode = "T" + teamId, TeamName = "Tổ " + teamId },
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
        defect.EditDeadline.Should().Be(Now.AddHours(24));   // hạn sửa của tổ trưởng: 24 giờ kể từ lúc lập phiếu
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

    private static CancelDefectDto Cancel(string? reason = "Ghi nhầm công nhân") => new(reason, RowVer);

    /// <summary>Sửa phiếu <see cref="Existing"/> (công đoạn 1, công nhân 7, Scrap, 2 cái, Torn): chỉ đổi những gì truyền vào.</summary>
    private static UpdateDefectDto Update(string type = "Scrap", int qty = 2, string reasonCode = "Torn", string? note = null,
        int worker = 7, int operation = 1, string? shift = null, string? reason = null) =>
        new(shift, operation, worker, type, qty, reasonCode, note, reason, RowVer);

    [Fact]
    public async Task Cancel_WithinWindow_MarksCancelledLogsTheReasonAndNotifiesTheWorker()
    {
        var defect = Existing();

        var result = await _sut.CancelAsync(defect.DefectRecordId, Cancel("Ghi nhầm công nhân"), default);

        result.IsSuccess.Should().BeTrue();
        defect.Status.Should().Be(DefectStatuses.Cancelled);
        defect.UpdatedAt.Should().Be(Now);
        Repo<DefectRecord>().Received(1).SetOriginalRowVersion(defect, RowVer);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.RecordId == defect.DefectRecordId
                                                      && e.Reason == "Ghi nhầm công nhân"));
        Notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 7 })),
            Arg.Is<NotificationMessage>(m => m.Message.Contains("Ghi nhầm công nhân")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Cancel_WithoutReason_IsRejectedForTheLeaderToo(string? reason)
    {
        var defect = Existing();

        var result = await _sut.CancelAsync(defect.DefectRecordId, Cancel(reason), default);

        result.Kind.Should().Be(ResultKind.Validation);
        result.Message.Should().Be(Messages.DefectCancelReasonRequired);
        defect.Status.Should().Be(DefectStatuses.Recorded);
    }

    [Theory]
    [InlineData(24 * 60, true)]       // đúng 24 giờ kể từ lúc lập phiếu vẫn được
    [InlineData(24 * 60 + 1, false)]  // quá 1 phút thì tổ trưởng không sửa / hủy được
    public async Task Leader_CancelAndUpdate_OnlyWithin24HoursOfRecording(int minutesAgo, bool allowed)
    {
        var defect = Existing();
        defect.RecordedAt = Now.AddMinutes(-minutesAgo);

        var update = await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 5), default);
        var cancel = await _sut.CancelAsync(defect.DefectRecordId, Cancel(), default);

        update.IsSuccess.Should().Be(allowed);
        cancel.IsSuccess.Should().Be(allowed);
        if (!allowed)
        {
            update.ErrorCode.Should().Be(ErrorCodes.ProductionEditDeadlinePassed);
            cancel.Message.Should().Be(Messages.DefectEditWindowPassed);
            defect.Quantity.Should().Be(2);
            defect.Status.Should().Be(DefectStatuses.Recorded);
        }
    }

    [Fact]
    public async Task Cancel_OtherTeamsDefectOrAlreadyCancelled_IsNotAllowed()
    {
        var other = Existing(teamId: 6);
        var done = Existing(status: DefectStatuses.Cancelled);

        (await _sut.CancelAsync(other.DefectRecordId, Cancel(), default)).Kind.Should().Be(ResultKind.NotFound);
        (await _sut.CancelAsync(done.DefectRecordId, Cancel(), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalidStatus);
        (await _sut.UpdateAsync(other.DefectRecordId, Update(qty: 5), default)).Kind.Should().Be(ResultKind.NotFound);
        (await _sut.UpdateAsync(done.DefectRecordId, Update(qty: 5), default)).ErrorCode.Should().Be(ErrorCodes.DefectInvalidStatus);
    }

    // ========== Sửa phiếu ==========

    [Fact]
    public async Task Update_ChangesFieldsKeepsTheLockedRate_LogsAndNotifiesTheWorker()
    {
        var defect = Existing();

        var result = await _sut.UpdateAsync(defect.DefectRecordId,
            Update(qty: 5, reasonCode: "Stained", shift: "MORNING", reason: "Đếm lại"), default);

        result.IsSuccess.Should().BeTrue();
        defect.Quantity.Should().Be(5);
        defect.DefectReasonCode.Should().Be(DefectReasons.Stained);
        defect.ShiftCode.Should().Be("MORNING");
        defect.PieceRateId.Should().Be(50);   // cùng công đoạn, vẫn Scrap: giữ đơn giá đã khóa lúc ghi
        defect.UpdatedAt.Should().Be(Now);
        Repo<DefectRecord>().Received(1).SetOriginalRowVersion(defect, RowVer);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.Reason == "Đếm lại"));
        Notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 7 })),
            Arg.Is<NotificationMessage>(m => m.Message.Contains("5 cái")));
    }

    [Fact]
    public async Task Update_ToAnotherWorker_NotifiesBothWorkers()
    {
        var defect = Existing();
        _employees.Add(Worker(8));

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(worker: 8), default)).IsSuccess.Should().BeTrue();

        defect.ResponsibleWorkerId.Should().Be(8);
        Notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.OrderBy(i => i).SequenceEqual(new[] { 7, 8 })),
            Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task Update_WorkerOfAnotherTeamOrLeft_IsRejected()
    {
        var defect = Existing();
        _employees.Add(Worker(8, teamId: 6));
        _employees.Add(Worker(9, status: RecordStatus.Resigned));

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(worker: 8), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
        (await _sut.UpdateAsync(defect.DefectRecordId, Update(worker: 9), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
        defect.ResponsibleWorkerId.Should().Be(7);
    }

    [Fact]
    public async Task Update_ReworkToScrap_LocksTheCurrentApprovedRate()
    {
        var defect = Existing(type: DefectTypes.Rework);

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(type: "Scrap"), default)).IsSuccess.Should().BeTrue();

        defect.DefectType.Should().Be(DefectTypes.Scrap);
        defect.PieceRateId.Should().Be(100);
    }

    [Fact]
    public async Task Update_ScrapToRework_ClearsTheRate()
    {
        var defect = Existing();

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(type: "Rework"), default)).IsSuccess.Should().BeTrue();

        defect.DefectType.Should().Be(DefectTypes.Rework);
        defect.PieceRateId.Should().BeNull();
    }

    [Fact]
    public async Task Update_ChangingTheOperationOfAScrap_RelocksTheRateOfTheNewOperation()
    {
        var defect = Existing();
        _operations.Add(new Operation { OperationId = 2, ProductId = 1, OperationCode = "OP20", OperationName = "May tay",
            StationSequence = 2, Status = OperationStatuses.Active });
        _rates.FindEffectiveAsync(2, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EffectivePieceRate(200, 900m));

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(operation: 2), default)).IsSuccess.Should().BeTrue();

        defect.OperationId.Should().Be(2);
        defect.PieceRateId.Should().Be(200);
    }

    [Fact]
    public async Task Update_ToScrapWithoutAnApprovedRate_IsRejected()
    {
        var defect = Existing(type: DefectTypes.Rework);
        _rates.FindEffectiveAsync(1, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((EffectivePieceRate?)null);

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(type: "Scrap"), default)).ErrorCode
            .Should().Be(ErrorCodes.PieceRateNotApproved);
        defect.DefectType.Should().Be(DefectTypes.Rework);
    }

    // ----- Tăng số lượng bị giới hạn bởi hàng trên chuyền (QA/QC chưa nhận, chưa hủy) -----

    [Theory]
    [InlineData(10, 10, true)]    // Scrap 2 → 10 tăng 8, trên chuyền còn 10 − 2 = 8: vừa đủ
    [InlineData(10, 11, false)]   // tăng 9 > 8
    [InlineData(2, 3, false)]     // trên chuyền đã hết (2 đã là Scrap), không tăng thêm được
    public async Task Update_IncreasingAScrap_IsLimitedToTheGoodsStillOnTheLine(int received, int newQty, bool allowed)
    {
        var defect = Existing();
        _issueLines[0].ConfirmedQty = received;

        var result = await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: newQty), default);

        result.IsSuccess.Should().Be(allowed);
        if (!allowed) result.Message.Should().Contain("trên chuyền");
        defect.Quantity.Should().Be(allowed ? newQty : 2);
    }

    [Fact]
    public async Task Update_QcPassedGoodsReduceTheRoomToIncrease()
    {
        var defect = Existing();
        _batches.Add(new QcBatch { SewingTeamId = TeamId, OrderLineId = LineId, FinishedQty = 95 });   // 100 − 95 − 2 = 3 còn trên chuyền

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 6), default)).IsSuccess.Should().BeFalse();   // tăng 4 > 3
        (await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 5), default)).IsSuccess.Should().BeTrue();    // tăng 3
    }

    [Fact]
    public async Task Update_ReducingOrKeepingTheQuantity_IsNotCheckedAgainstTheLine()
    {
        var defect = Existing();
        defect.Quantity = 5;
        _issueLines[0].ConfirmedQty = 5;   // trên chuyền đã hết

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 5, reasonCode: "Stained"), default)).IsSuccess.Should().BeTrue();
        (await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 3, reasonCode: "Stained"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ReworkToScrap_NeedsTheWholeQuantityOnTheLine()
    {
        var defect = Existing(type: DefectTypes.Rework);
        _issueLines[0].ConfirmedQty = 1;

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(type: "Scrap"), default)).IsSuccess.Should().BeFalse();   // cần 2, còn 1

        _issueLines[0].ConfirmedQty = 2;
        (await _sut.UpdateAsync(defect.DefectRecordId, Update(type: "Scrap"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_IncreasingAnOutputError_IsNotLimitedByTheLine()
    {
        var defect = Existing(type: DefectTypes.OutputError);
        defect.DefectReasonCode = DefectReasons.Other;
        defect.ReasonNote = "Nhập nhầm";
        _issueLines[0].ConfirmedQty = 0;
        Output(10);

        (await _sut.UpdateAsync(defect.DefectRecordId, Update("OutputErr", 6, note: "Nhập nhầm"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNothingChanged_IsRejected()
    {
        var defect = Existing();

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(), default)).Message.Should().Be(Messages.DefectNothingChanged);
        Audit.DidNotReceive().Add(Arg.Any<AuditEntry>());
    }

    [Fact]
    public async Task Update_InvalidInput_IsRejected()
    {
        var defect = Existing();

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 0), default)).Kind.Should().Be(ResultKind.Validation);
        (await _sut.UpdateAsync(defect.DefectRecordId, Update(reasonCode: "Other"), default)).Message.Should().Be(Messages.DefectNoteRequired);
        (await _sut.UpdateAsync(defect.DefectRecordId, Update(type: "Bad"), default)).Message.Should().Be(Messages.DefectTypeInvalid);
        defect.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task UpdateOutputError_DoesNotCountItselfAgainstTheOutputOfTheDay()
    {
        var defect = Existing(type: DefectTypes.OutputError);
        defect.DefectReasonCode = DefectReasons.Other;
        defect.ReasonNote = "Nhập nhầm";
        Output(5);

        (await _sut.UpdateAsync(defect.DefectRecordId, Update("OutputErr", 6, note: "Nhập nhầm"), default)).IsSuccess.Should().BeFalse();
        (await _sut.UpdateAsync(defect.DefectRecordId, Update("OutputErr", 5, note: "Nhập nhầm"), default)).IsSuccess.Should().BeTrue();
        defect.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task UpdateOutputError_ReleasingMoreOutputThanTheCapAllows_IsRejected()
    {
        var defect = Existing(type: DefectTypes.OutputError);
        _caps.Add(new VwWorkerOutputCap { WorkerId = 7, OrderLineId = LineId, OperationId = 1, RemainingQty = 1 });

        // Đổi sang Rework trả lại 2 cái sản lượng nhưng công nhân chỉ còn chỗ cho 1.
        var result = await _sut.UpdateAsync(defect.DefectRecordId, Update(type: "Rework"), default);

        result.Message.Should().Be(Messages.DefectOutputErrorCancelOverCap);
        defect.DefectType.Should().Be(DefectTypes.OutputError);
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
    public async Task ListMine_FiltersByStatus()
    {
        Existing(workerId: 7, date: Today);
        Existing(workerId: 7, date: Today.AddDays(-1), status: DefectStatuses.Confirmed);

        var result = await _sut.ListMineAsync(7, new DefectQuery { Status = "Confirmed" }, default);

        result.Data!.Select(d => d.Status).Should().Equal("Confirmed");
    }

    [Fact]
    public async Task ListForTeam_LeaderSeesOwnTeamOnly_ManagerSeesAll_CanEditFollows24HourWindow()
    {
        Existing(teamId: TeamId);                       // lập 1 giờ trước: còn trong 24 giờ
        Aged(Existing(teamId: TeamId), 25);              // quá 24 giờ: tổ trưởng hết quyền sửa
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

    // ========== Quản đốc / Admin sửa, hủy (DEV-ALL-106) ==========

    private DefectService AsManagerService()
    {
        AsManager();
        return new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock);
    }

    private static DefectRecord Aged(DefectRecord d, int hours)
    {
        d.RecordedAt = Now.AddHours(-hours);
        return d;
    }

    [Fact]
    public async Task Manager_CanCancelAfter24Hours_WithAReason()
    {
        var defect = Aged(Existing(), 72);

        (await AsManagerService().CancelAsync(defect.DefectRecordId, new CancelDefectDto("Ghi nhầm người", RowVer), default))
            .IsSuccess.Should().BeTrue();

        defect.Status.Should().Be(DefectStatuses.Cancelled);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Reason == "Ghi nhầm người"));
    }

    [Fact]
    public async Task Manager_CancelWithoutReason_IsRejected()
    {
        var defect = Existing();

        var result = await AsManagerService().CancelAsync(defect.DefectRecordId, new CancelDefectDto(" ", RowVer), default);

        result.Message.Should().Be(Messages.DefectCancelReasonRequired);
        defect.Status.Should().Be(DefectStatuses.Recorded);
    }

    [Fact]
    public async Task Manager_UpdateNeedsAReason_AndWorksAfter24Hours()
    {
        var defect = Aged(Existing(), 72);
        var sut = AsManagerService();

        (await sut.UpdateAsync(defect.DefectRecordId, Update(qty: 6), default)).Message.Should().Be(Messages.DefectUpdateReasonRequired);
        (await sut.UpdateAsync(defect.DefectRecordId, Update(qty: 6, reason: "QA/QC đếm lại"), default)).IsSuccess.Should().BeTrue();

        defect.Quantity.Should().Be(6);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Reason == "QA/QC đếm lại"));
    }

    [Fact]
    public async Task LegacyConfirmedDefect_StillCancellableAndEditable()
    {
        var defect = Existing(status: DefectStatuses.Confirmed);

        (await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 4), default)).IsSuccess.Should().BeTrue();
        (await _sut.CancelAsync(defect.DefectRecordId, Cancel(), default)).IsSuccess.Should().BeTrue();
    }

    // ----- Ràng buộc chặn sửa / hủy -----

    [Fact]
    public async Task CancelAndUpdate_InALockedPeriod_AreRejected()
    {
        _periods.Add(new AccountingPeriod
        {
            PeriodMonth = "2026-10", Status = RecordStatus.Locked, StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31),
        });
        var defect = Existing();
        var sut = AsManagerService();

        (await sut.CancelAsync(defect.DefectRecordId, Cancel(), default)).ErrorCode.Should().Be(ErrorCodes.ProductionPeriodLocked);
        (await sut.UpdateAsync(defect.DefectRecordId, Update(qty: 9, reason: "x"), default)).ErrorCode
            .Should().Be(ErrorCodes.ProductionPeriodLocked);
        defect.Status.Should().Be(DefectStatuses.Recorded);
        defect.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task CancelAndUpdate_WhenTheOrderIsClosed_AreRejected()
    {
        var defect = Existing();
        _lines[0].Order.Status = OrderStatuses.Closed.First();

        (await _sut.CancelAsync(defect.DefectRecordId, Cancel(), default)).Message.Should().Be(Messages.DefectOrderClosed);
        (await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 9), default)).Message.Should().Be(Messages.DefectOrderClosed);
        defect.Status.Should().Be(DefectStatuses.Recorded);
    }

    [Theory]
    [InlineData(RequestStatuses.PendingApproval, true)]
    [InlineData(RequestStatuses.PendingProcessing, true)]
    [InlineData(RequestStatuses.Approved, true)]
    [InlineData(RequestStatuses.Completed, true)]
    [InlineData(RequestStatuses.Rejected, false)]
    [InlineData(RequestStatuses.NotCompleted, false)]
    [InlineData(RequestStatuses.Cancelled, false)]
    public async Task CancelAndUpdate_AreBlockedOnlyByAReplacementRequestStillInEffect(string requestStatus, bool blocked)
    {
        var defect = Existing();
        _requests.Add(new Request { RequestId = 1, RequestCode = "REQ-001", DefectRecordId = defect.DefectRecordId, Status = requestStatus });

        var update = await _sut.UpdateAsync(defect.DefectRecordId, Update(qty: 9), default);
        var cancel = await _sut.CancelAsync(defect.DefectRecordId, Cancel(), default);

        update.IsSuccess.Should().Be(!blocked);
        cancel.IsSuccess.Should().Be(!blocked);
        if (blocked) cancel.Message.Should().Contain("REQ-001");
    }

    [Fact]
    public async Task ListForTeam_Filters_ByStatusTypeAndTeam()
    {
        _teams.Add(new Team { TeamId = 6, TeamCode = "MAY2", TeamName = "Tổ may 2", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active });
        _employees.Add(Worker(8, teamId: 6));
        var recorded = Existing();
        var rework = Existing(type: DefectTypes.Rework);
        var confirmed = Existing(status: DefectStatuses.Confirmed);
        var cancelled = Existing(status: DefectStatuses.Cancelled);
        var otherTeam = Existing(workerId: 8, teamId: 6);
        var sut = AsManagerService();
        async Task<List<int>> Ids(DefectQuery q) =>
            (await sut.ListForTeamAsync(q, default)).Data!.Items.Select(d => d.DefectRecordId).OrderBy(i => i).ToList();
        int Id(DefectRecord d) => d.DefectRecordId;

        (await Ids(new DefectQuery { Status = "Confirmed" })).Should().Equal(Id(confirmed));
        (await Ids(new DefectQuery { Status = "Cancelled" })).Should().Equal(Id(cancelled));
        (await Ids(new DefectQuery { DefectType = "Rework" })).Should().Equal(Id(rework));
        (await Ids(new DefectQuery { SewingTeamId = 6 })).Should().Equal(Id(otherTeam));
        (await Ids(new DefectQuery { Status = "Recorded", DefectType = "Scrap" })).Should().BeEquivalentTo(new[] { Id(recorded), Id(otherTeam) });
    }

    [Fact]
    public async Task ListForTeam_TeamLeaderCannotWidenTheTeamWithTheTeamFilter()
    {
        _teams.Add(new Team { TeamId = 6, TeamCode = "MAY2", TeamName = "Tổ may 2", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active });
        _employees.Add(Worker(8, teamId: 6));
        var mine = Existing();
        Existing(workerId: 8, teamId: 6);

        var items = (await _sut.ListForTeamAsync(new DefectQuery { SewingTeamId = 6 }, default)).Data!.Items;

        items.Should().ContainSingle().Which.DefectRecordId.Should().Be(mine.DefectRecordId);
    }

    [Fact]
    public async Task ListForTeam_Manager_SearchesByWorkerNameProductCodeAndTeam_AndReturnsTeamName()
    {
        _teams.Add(new Team { TeamId = 6, TeamCode = "MAY2", TeamName = "Tổ may 2", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active });
        _employees.Add(Worker(8, teamId: 6));
        Existing(workerId: 7, teamId: TeamId);
        Existing(workerId: 8, teamId: 6);
        AsManager();
        var manager = new DefectService(Uow, Guard(), _rates, Notifier, Audit, User, Clock);
        async Task<IReadOnlyList<DefectDto>> Search(string keyword) =>
            (await manager.ListForTeamAsync(new DefectQuery { Keyword = keyword }, default)).Data!.Items;
        var productCode = _lines[0].ProductVariant.Product.ProductCode;

        (await Search("Công nhân 8")).Should().ContainSingle().Which.SewingTeamName.Should().Be("Tổ may 2");
        (await Search("Tổ may 1")).Should().ContainSingle().Which.ResponsibleWorkerId.Should().Be(7);
        (await Search(productCode)).Should().HaveCount(2);
        (await Search("khong-co")).Should().BeEmpty();
    }

    // ========== Nhập sai sản lượng (OUTPUT_ERR, DEV-ALL-79) ==========

    private void Output(int qty, int worker = 7, DateOnly? date = null) =>
        _outputs.Add(new OutputRecord { WorkDate = date ?? Today, WorkerId = worker, OrderLineId = LineId, OperationId = 1, Quantity = qty });

    private static CreateDefectDto CreateOutputError(int qty = 4, string? note = "Nhập nhầm 10 thành 20", int worker = 7, DateOnly? date = null) =>
        Create("OutputErr", qty, "Other", note, worker, date);

    [Fact]
    public async Task CreateOutputError_LocksRate_UsesOtherReasonAndNotifiesTheWorker()
    {
        Output(10);

        var result = await _sut.CreateAsync(CreateOutputError(), default);

        result.IsSuccess.Should().BeTrue();
        var defect = _defects.Single();
        defect.DefectType.Should().Be(DefectTypes.OutputError);
        defect.DefectReasonCode.Should().Be(DefectReasons.Other);
        defect.ReasonNote.Should().Be("Nhập nhầm 10 thành 20");
        defect.PieceRateId.Should().Be(100);
        Notifier.Received(1).NotifyEmployees(Arg.Any<IEnumerable<int>>(), Arg.Is<NotificationMessage>(m => m.Message.Contains("nhập sai sản lượng")));
    }

    [Fact]
    public async Task CreateOutputError_IgnoresTheReasonCodeSent_AlwaysOther()
    {
        Output(10);

        var result = await _sut.CreateAsync(Create("OutputErr", 4, "Torn", "Nhập nhầm"), default);

        result.IsSuccess.Should().BeTrue();
        _defects.Single().DefectReasonCode.Should().Be(DefectReasons.Other);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateOutputError_NeedsAnExplanation(string? note)
    {
        Output(10);

        var result = await _sut.CreateAsync(CreateOutputError(note: note), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(Messages.DefectNoteRequired);
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOutputError_WithoutOutputThatDay_IsRejected()
    {
        Output(10, date: Today.AddDays(-1));

        var result = await _sut.CreateAsync(CreateOutputError(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(Today.ToString("dd/MM/yyyy"));
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOutputError_OtherWorkersOutputDoesNotCount()
    {
        Output(10, worker: 8);

        (await _sut.CreateAsync(CreateOutputError(), default)).IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(1, true)]
    public async Task CreateOutputError_CannotExceedTheOutputOfTheDay(int qty, bool expected)
    {
        Output(10);

        (await _sut.CreateAsync(CreateOutputError(qty), default)).IsSuccess.Should().Be(expected);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task CreateOutputError_CountsTheDeductionsAlreadyRecorded(int qty, bool expected)
    {
        Output(10);
        Existing(type: DefectTypes.OutputError).Quantity = 7;

        var result = await _sut.CreateAsync(CreateOutputError(qty), default);

        result.IsSuccess.Should().Be(expected);
        if (!expected) result.Message.Should().Contain("chỉ còn trừ được 3");
    }

    [Fact]
    public async Task CreateOutputError_CancelledDeductionsAreNotCounted()
    {
        Output(10);
        Existing(type: DefectTypes.OutputError, status: DefectStatuses.Cancelled).Quantity = 7;

        (await _sut.CreateAsync(CreateOutputError(10), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateOutputError_WithoutApprovedRate_IsRejected()
    {
        Output(10);
        _rates.FindEffectiveAsync(1, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((EffectivePieceRate?)null);

        var result = await _sut.CreateAsync(CreateOutputError(), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
    }

    [Fact]
    public async Task CancelOutputError_WhenTheWorkerHasNoRoomLeftUnderTheCap_IsRejected()
    {
        var defect = Existing(type: DefectTypes.OutputError);
        _caps.Add(new VwWorkerOutputCap { WorkerId = 7, OrderLineId = LineId, OperationId = 1, RemainingQty = 1 });

        var result = await _sut.CancelAsync(defect.DefectRecordId, new CancelDefectDto("Ghi nhầm", RowVer), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(Messages.DefectOutputErrorCancelOverCap);
        defect.Status.Should().Be(DefectStatuses.Recorded);
    }

    [Fact]
    public async Task CancelOutputError_WithRoomUnderTheCap_ReturnsTheOutputToTheWorker()
    {
        var defect = Existing(type: DefectTypes.OutputError);
        _caps.Add(new VwWorkerOutputCap { WorkerId = 7, OrderLineId = LineId, OperationId = 1, RemainingQty = 2 });

        var result = await _sut.CancelAsync(defect.DefectRecordId, new CancelDefectDto("Ghi nhầm", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        defect.Status.Should().Be(DefectStatuses.Cancelled);
    }

    [Fact]
    public async Task ScrapAndRework_AreNotAffectedByTheOutputCheck()
    {
        (await _sut.CreateAsync(Create("Scrap"), default)).IsSuccess.Should().BeTrue();
        (await _sut.CreateAsync(Create("Rework", 2, "Torn"), default)).IsSuccess.Should().BeTrue();
    }
}
