using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KI-06 + KI-11 (UC57, UC22): QA/QC nhập số đạt theo đợt; trần theo hàng trên chuyền; hạn sửa hết ca.</summary>
public class QcBatchServiceTests : ProductionTestBase
{
    private const int TeamId = 5;
    private const int LineId = 11;
    private static readonly byte[] RowVer = { 1 };
    private static readonly TimeOnly At = new(9, 0);

    private readonly List<QcBatch> _batches = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<DefectRecord> _defects = [];
    private readonly List<Team> _teams = [new() { TeamId = TeamId, TeamCode = "MAY1", TeamName = "Tổ may 1", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active }];
    private readonly List<OrderLine> _lines = [];
    private readonly List<AccountingPeriod> _periods = [];
    private readonly QcBatchService _sut;
    private int _nextId = 1;

    public QcBatchServiceTests()
    {
        SetupQueryable(_batches);
        SetupQueryable(_issueLines);
        SetupQueryable(_defects);
        SetupQueryable(_teams);
        SetupQueryable(_lines);
        SetupQueryable(_periods);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        AsQaQc();

        Repo<QcBatch>().When(r => r.AddAsync(Arg.Any<QcBatch>())).Do(c =>
        {
            var batch = c.Arg<QcBatch>();
            batch.QcBatchId = _nextId++;
            _batches.Add(batch);
        });
        Repo<QcBatch>().When(r => r.Remove(Arg.Any<QcBatch>())).Do(c => _batches.Remove(c.Arg<QcBatch>()));

        _lines.Add(new OrderLine
        {
            OrderLineId = LineId, OrderId = 1, ProductId = 1, ProductVariant = TestVariant.Make(1, "MH001", "Đen", "M"),
            Order = new CustomerOrder { OrderId = 1, OrderCode = "DH001", Status = OrderStatuses.InProduction },
        });
        // Tổ đã nhận 1000 bộ BTP của dòng đơn (phiếu cấp BTP đã nhận, dòng bộ đủ).
        _issueLines.Add(new MaterialIssueLine
        {
            OrderLineId = LineId, IssuedQty = 1000, ConfirmedQty = 1000,
            MaterialIssue = new MaterialIssue { WarehouseType = WarehouseTypes.SemiFinished, Status = IssueStatuses.Received, ReceivingTeamId = TeamId },
        });
        _sut = new QcBatchService(Uow, Guard(), User, Audit, Clock);
    }

    private QcBatch Batch(int qty, DateTime? deadline = null, DateOnly? date = null, TimeOnly? time = null)
    {
        var batch = new QcBatch
        {
            QcBatchId = _nextId++, WorkDate = date ?? Today, ShiftCode = ShiftCodes.Morning, BatchTime = time ?? At,
            SewingTeamId = TeamId, OrderLineId = LineId, FinishedQty = qty, InspectorId = 900,
            EditDeadline = deadline ?? Now.AddHours(1), RowVer = RowVer,
            SewingTeam = _teams[0], OrderLine = _lines[0], Inspector = new Employee { EmployeeId = 900, FullName = "QA Hoa" },
        };
        _batches.Add(batch);
        return batch;
    }

    private static CreateQcBatchDto Create(int qty = 100, TimeOnly? time = null, DateOnly? date = null, string shift = "MORNING") =>
        new(date ?? Today, shift, time ?? At, TeamId, LineId, qty);

    // ========== Tạo đợt QC ==========

    [Fact]
    public async Task Create_Valid_SavesWithInspectorMorningDeadlineAndLogsInsert()
    {
        var result = await _sut.CreateAsync(Create(100), default);

        result.IsSuccess.Should().BeTrue();
        var batch = _batches.Single();
        batch.InspectorId.Should().Be(900);                                       // người đăng nhập
        batch.EditDeadline.Should().Be(new DateTime(2026, 10, 21, 11, 30, 0));    // hết ca sáng = đầu nghỉ trưa
        batch.CreatedAt.Should().Be(Now);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(QcBatch) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task Create_AfternoonShift_DeadlineIsWorkEnd()
    {
        await _sut.CreateAsync(Create(shift: "Afternoon", time: new TimeOnly(14, 0)), default);

        _batches.Single().EditDeadline.Should().Be(new DateTime(2026, 10, 21, 16, 30, 0));
    }

    [Fact]
    public async Task Create_ExceedsPiecesOnTheLine_IsBlockedWithMsg19()
    {
        var result = await _sut.CreateAsync(Create(1001), default);

        result.ErrorCode.Should().Be(ErrorCodes.QcExceedsOnLine);
        result.Message.Should().Contain("1000");
        _batches.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_OnLineIsReceivedMinusFinishedMinusScrap()
    {
        Batch(300, time: new TimeOnly(8, 0));   // đã QC đạt 300
        _defects.Add(new DefectRecord { SewingTeamId = TeamId, OrderLineId = LineId, DefectType = DefectTypes.Scrap,
            Status = DefectStatuses.Recorded, Quantity = 100 });                                // 100 Scrap
        _defects.Add(new DefectRecord { SewingTeamId = TeamId, OrderLineId = LineId, DefectType = DefectTypes.Scrap,
            Status = DefectStatuses.Cancelled, Quantity = 50 });                                // phiếu hủy: không trừ
        _defects.Add(new DefectRecord { SewingTeamId = TeamId, OrderLineId = LineId, DefectType = DefectTypes.Rework,
            Status = DefectStatuses.Recorded, Quantity = 40 });                                 // Rework: không trừ khỏi chuyền

        var onLine = (await _sut.GetOnLineAsync(TeamId, LineId, default)).Data!;

        onLine.Received.Should().Be(1000);
        onLine.Finished.Should().Be(300);
        onLine.Scrap.Should().Be(100);
        onLine.OnLine.Should().Be(600);
        (await _sut.CreateAsync(Create(601), default)).ErrorCode.Should().Be(ErrorCodes.QcExceedsOnLine);
        (await _sut.CreateAsync(Create(600), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateBatch_IsRejected()
    {
        Batch(100);

        (await _sut.CreateAsync(Create(50), default)).ErrorCode.Should().Be(ErrorCodes.QcBatchDuplicate);
    }

    [Fact]
    public async Task Create_InvalidInput_IsRejected()
    {
        (await _sut.CreateAsync(Create(0), default)).ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        (await _sut.CreateAsync(Create(date: Today.AddDays(1)), default)).ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        (await _sut.CreateAsync(Create(shift: "NIGHT"), default)).ErrorCode.Should().Be(ErrorCodes.OutputInvalid);
        (await _sut.CreateAsync(Create() with { SewingTeamId = 99 }, default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Create_ClosedOrder_IsRejected()
    {
        _lines[0].Order.Status = OrderStatuses.Cancelled;

        (await _sut.CreateAsync(Create(), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentOrderInvalid);
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

    // ========== Sửa / xóa ==========

    [Fact]
    public async Task Update_WithinShift_ChangesQuantityAndLogsOldAndNew()
    {
        var batch = Batch(100);

        var result = await _sut.UpdateAsync(batch.QcBatchId, new UpdateQcBatchDto(150, null, RowVer), default);

        result.IsSuccess.Should().BeTrue();
        batch.FinishedQty.Should().Be(150);
        batch.UpdatedAt.Should().Be(Now);
        Repo<QcBatch>().Received(1).SetOriginalRowVersion(batch, RowVer);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.OldValues != null));
    }

    [Fact]
    public async Task Update_DoesNotCountTheEditedBatchAgainstTheCap()
    {
        var batch = Batch(900);

        (await _sut.UpdateAsync(batch.QcBatchId, new UpdateQcBatchDto(1000, null, RowVer), default)).IsSuccess.Should().BeTrue();
        (await _sut.UpdateAsync(batch.QcBatchId, new UpdateQcBatchDto(1001, null, RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.QcExceedsOnLine);
    }

    [Fact]
    public async Task Update_AfterShiftEnd_QaQcIsForbiddenButManagerCanWithReason()
    {
        var batch = Batch(100, deadline: Now.AddMinutes(-10));

        (await _sut.UpdateAsync(batch.QcBatchId, new UpdateQcBatchDto(90, "nhầm", RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.ProductionEditDeadlinePassed);   // MSG20

        AsManager();
        var manager = new QcBatchService(Uow, Guard(), User, Audit, Clock);
        (await manager.UpdateAsync(batch.QcBatchId, new UpdateQcBatchDto(90, null, RowVer), default)).Kind
            .Should().Be(ResultKind.Validation);                      // thiếu lý do
        (await manager.UpdateAsync(batch.QcBatchId, new UpdateQcBatchDto(90, "Nhập nhầm số", RowVer), default)).IsSuccess
            .Should().BeTrue();
        batch.FinishedQty.Should().Be(90);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Reason == "Nhập nhầm số"));
    }

    [Fact]
    public async Task Delete_WithinShift_RemovesBatchAndLogsDelete()
    {
        var batch = Batch(100);

        var result = await _sut.DeleteAsync(batch.QcBatchId, new DeleteQcBatchDto(null, RowVer), default);

        result.IsSuccess.Should().BeTrue();
        _batches.Should().BeEmpty();
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Delete && e.RecordId == batch.QcBatchId));
    }

    [Fact]
    public async Task Delete_AfterShiftEnd_QaQcIsForbidden()
    {
        var batch = Batch(100, deadline: Now.AddMinutes(-10));

        (await _sut.DeleteAsync(batch.QcBatchId, new DeleteQcBatchDto("x", RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.ProductionEditDeadlinePassed);
        _batches.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateOrDelete_UnknownBatch_IsNotFound()
    {
        (await _sut.UpdateAsync(99, new UpdateQcBatchDto(1, null, RowVer), default)).Kind.Should().Be(ResultKind.NotFound);
        (await _sut.DeleteAsync(99, new DeleteQcBatchDto(null, RowVer), default)).Kind.Should().Be(ResultKind.NotFound);
    }

    // ========== Danh sách ==========

    [Fact]
    public async Task List_FiltersByDateAndShowsCanEditPerRole()
    {
        Batch(100, deadline: Now.AddHours(1), time: new TimeOnly(8, 0));
        Batch(50, deadline: Now.AddMinutes(-10), time: new TimeOnly(7, 0));
        Batch(70, date: Today.AddDays(-1), time: new TimeOnly(7, 0));

        var leader = (await _sut.ListAsync(new QcBatchQuery { WorkDate = Today }, default)).Data!.Items;
        AsManager();
        var manager = (await new QcBatchService(Uow, Guard(), User, Audit, Clock)
            .ListAsync(new QcBatchQuery { WorkDate = Today }, default)).Data!.Items;

        leader.Should().HaveCount(2);
        leader.Single(i => i.FinishedQty == 100).CanEdit.Should().BeTrue();
        leader.Single(i => i.FinishedQty == 50).CanEdit.Should().BeFalse();   // quá hết ca
        manager.Should().OnlyContain(i => i.CanEdit);                          // Quản đốc luôn có quyền (kèm lý do)
        leader[0].OrderCode.Should().Be("DH001");
        leader[0].InspectorName.Should().Be("QA Hoa");
    }
}
