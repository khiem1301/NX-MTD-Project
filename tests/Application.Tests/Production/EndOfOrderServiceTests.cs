using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Kết thúc đơn của tổ: cảnh báo lệch kiểm cuối đơn theo từng công đoạn (không tự ghi phiếu / trừ lương), chốt KPI, tăng ca khi không đạt.</summary>
public class EndOfOrderServiceTests : ProductionTestBase
{
    private const int TeamId = 5, OrderId = 1, ProductId = 1, FinalOp = 9, CollarOp = 8, SleeveOp = 7, LineId = 11, Line2Id = 12;

    private readonly List<KpiTarget> _targets = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<QcBatch> _qc = [];
    private readonly List<DefectRecord> _defects = [];
    private readonly List<Operation> _operations = [];
    private readonly List<OutputRecord> _outputs = [];
    private readonly List<Employee> _employees = [];
    private readonly KpiTarget _target;

    public EndOfOrderServiceTests()
    {
        SetupQueryable(_targets); SetupQueryable(_lines); SetupQueryable(_qc); SetupQueryable(_defects);
        SetupQueryable(_operations); SetupQueryable(_outputs); SetupQueryable(_employees);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        AsManager();
        Settings.GetIntAsync(SettingKeys.KpiOtAutoThresholdPct, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(10);

        _lines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, OrderedQty = 100 }
            .WithVariant(TestVariant.Make(ProductId, "MH001", "Đen", "M")));
        _lines.Add(new OrderLine { OrderLineId = Line2Id, OrderId = OrderId, OrderedQty = 100 }
            .WithVariant(TestVariant.Make(ProductId, "MH001", "Đen", "L")));
        _operations.Add(new Operation { OperationId = CollarOp, ProductId = ProductId, OperationCode = "OP10", OperationName = "May cổ", Status = OperationStatuses.Active });
        _operations.Add(new Operation { OperationId = SleeveOp, ProductId = ProductId, OperationCode = "OP20", OperationName = "May tay", Status = OperationStatuses.Active });
        _operations.Add(new Operation
        {
            OperationId = FinalOp, ProductId = ProductId, OperationCode = "OPF", OperationName = "Hoàn thiện",
            IsFinalStep = true, Status = OperationStatuses.Active,
        });
        for (var id = 7; id <= 12; id++)
            _employees.Add(new Employee { EmployeeId = id, EmployeeCode = "CN" + id, FullName = "Công nhân " + id });
        _target = new KpiTarget
        {
            KpiTargetId = 1, OrderId = OrderId, ProductId = ProductId, SewingTeamId = TeamId, TargetFinishedQty = 100,
            Status = KpiStatuses.Active,
            Order = new CustomerOrder { OrderId = OrderId, OrderCode = "DH001" },
            Product = new Product { ProductId = ProductId, ProductCode = "MH001" },
            SewingTeam = new Team { TeamId = TeamId, TeamCode = "MAY1", TeamName = "Tổ may 1" },
        };
        _targets.Add(_target);
    }

    private EndOfOrderService Sut() => new(Uow, Notifier, Audit, Settings);

    private void Reported(int worker, int qty, int operation = FinalOp, int line = LineId) => _outputs.Add(new OutputRecord
    {
        SewingTeamId = TeamId, WorkerId = worker, OperationId = operation, OrderLineId = line, Quantity = qty,
    });

    private void Qc(int qty, int line = LineId) => _qc.Add(new QcBatch { SewingTeamId = TeamId, OrderLineId = line, FinishedQty = qty });

    private async Task<EndOfOrderPreviewDto> Preview(int? teamId = null) => (await Sut().PreviewAsync(1, teamId, default)).Data!;

    private static bool IsMismatchNote(NotificationMessage m) => m.Title == Messages.NotifyEndOfOrderMismatchTitle;

    // ========== So lệch theo từng công đoạn ==========

    [Fact]
    public async Task Preview_ComparesEveryOperationWithQcPassedPlusScrap_AndListsWhoReportedWhat()
    {
        Qc(90);
        _defects.Add(new DefectRecord
        {
            SewingTeamId = TeamId, OrderLineId = LineId, DefectType = DefectTypes.Scrap, Status = DefectStatuses.Recorded, Quantity = 4,
        });   // mỗi công đoạn tối đa 94
        Reported(7, 60, CollarOp); Reported(8, 40, CollarOp);   // may cổ báo 100 → lệch 6
        Reported(9, 94, SleeveOp);                              // may tay báo 94 → không lệch
        Reported(10, 50, FinalOp); Reported(11, 47, FinalOp);   // hoàn thiện báo 97 → lệch 3

        var preview = await Preview();

        preview.ActualQty.Should().Be(90);
        preview.Operations.Select(o => (o.OperationName, o.MismatchQty)).Should().Equal(("May cổ", 6), ("Hoàn thiện", 3));   // lệch nhiều nhất lên đầu
        var collar = preview.Operations[0].Lines.Should().ContainSingle().Subject;
        (collar.Color, collar.SizeCode, collar.ReportedQty, collar.QcQty, collar.ScrapQty, collar.MismatchQty)
            .Should().Be(("Đen", "M", 100, 90, 4, 6));
        // Hệ thống không biết ai báo dư: liệt kê mọi người đã báo công đoạn đó, sản lượng nhiều nhất trước.
        collar.Workers.Select(w => (w.WorkerId, w.ReportedQty)).Should().Equal((7, 60), (8, 40));
        preview.Operations[1].Lines.Single().Workers.Select(w => (w.WorkerId, w.ReportedQty)).Should().Equal((10, 50), (11, 47));
        preview.Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_OperationsAtOrBelowTheLimit_AreNotListed()
    {
        Qc(100);
        Reported(7, 100, CollarOp); Reported(8, 100, FinalOp); Reported(9, 60, SleeveOp);   // báo thiếu cũng không phải lệch dư

        (await Preview()).Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_ListsOnlyTheLinesThatAreOff_MostOffFirst()
    {
        Qc(90, LineId); Qc(90, Line2Id);
        Reported(7, 95, CollarOp, LineId);      // lệch 5
        Reported(8, 100, CollarOp, Line2Id);    // lệch 10
        Reported(9, 90, CollarOp, LineId);      // dòng khác của công đoạn khác không lẫn: cộng cùng dòng LineId → 185 − 90 = 95

        var collar = (await Preview()).Operations.Single();

        collar.Lines.Select(l => (l.SizeCode, l.MismatchQty)).Should().Equal(("M", 95), ("L", 10));
        collar.MismatchQty.Should().Be(105);
    }

    [Fact]
    public async Task Preview_QcInThePeriodOfAnotherTargetOfTheSameTeam_IsNotCountedAgain()
    {
        var firstDay = new DateOnly(2026, 9, 21);
        _targets.Add(new KpiTarget
        {
            KpiTargetId = 2, OrderId = OrderId, ProductId = ProductId, SewingTeamId = TeamId, Status = KpiStatuses.Closed,
            StartDate = firstDay, EndDate = firstDay.AddDays(9),
        });
        _qc.Add(new QcBatch { SewingTeamId = TeamId, OrderLineId = LineId, FinishedQty = 70, WorkDate = firstDay.AddDays(1) });
        _qc.Add(new QcBatch { SewingTeamId = TeamId, OrderLineId = LineId, FinishedQty = 40, WorkDate = firstDay.AddDays(15) });

        var preview = await Preview();

        preview.ActualQty.Should().Be(40);
        preview.Achieved.Should().BeFalse();
    }

    [Fact]
    public async Task Preview_ScrapAlreadyRecordedIsNotCountedAsMismatch()
    {
        Reported(7, 100); Qc(90);
        _defects.Add(new DefectRecord
        {
            SewingTeamId = TeamId, OrderLineId = LineId, DefectType = DefectTypes.Scrap, Status = DefectStatuses.Recorded, Quantity = 10,
        });

        (await Preview()).Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_ATeamLeaderSeesOnlyTheTargetOfTheirOwnTeam()
    {
        Reported(7, 100); Qc(90);

        (await Sut().PreviewAsync(1, TeamId, default)).IsSuccess.Should().BeTrue();
        (await Sut().PreviewAsync(1, TeamId + 1, default)).Kind.Should().Be(ResultKind.NotFound);
        (await Sut().PreviewAsync(99, TeamId, default)).Kind.Should().Be(ResultKind.NotFound);
    }

    // ========== Kết thúc đơn ==========

    [Fact]
    public async Task Finish_WithMismatches_WritesNoDefect_AndNotifiesAShortSummaryWithTheTopOperationsAndWorkers()
    {
        Qc(95);   // thiếu 5% ≤ 10%
        Reported(7, 60, CollarOp); Reported(8, 40, CollarOp); Reported(9, 10, CollarOp);   // may cổ lệch 15, 3 người
        Reported(10, 100, FinalOp);                                                         // hoàn thiện lệch 5

        var result = await Sut().FinishAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        _defects.Should().BeEmpty();   // không tự ghi phiếu, không trừ lương
        Notifier.DidNotReceiveWithAnyArgs().NotifyEmployees(default!, default!);
        _target.Status.Should().Be(KpiStatuses.Closed);
        _target.Result.Should().Be(KpiResults.NotAchieved);
        string? summary = null;
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => IsMismatchNote(m) && Capture(m, out summary)), Arg.Any<CancellationToken>());
        summary.Should().Contain("May cổ: lệch 15").And.Contain("CN7 Công nhân 7 60, CN8 Công nhân 8 40 và 1 người khác")
            .And.Contain("Hoàn thiện: lệch 5").And.Contain("Bấm để xem chi tiết");
        summary!.Length.Should().BeLessThan(1000);
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => m.Message.Contains("nên tăng ca")), Arg.Any<CancellationToken>());   // thiếu ít
    }

    private static bool Capture(NotificationMessage m, out string? message)
    {
        message = m.Message;
        return true;
    }

    [Fact]
    public async Task Finish_MismatchNoteOpensTheDetailInPlace_ByRefInsteadOfALink()
    {
        Reported(7, 100); Qc(90);

        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => IsMismatchNote(m) && m.LinkUrl == null && m.RefTable == NotificationRefs.KpiMismatch && m.RefId == 1), Arg.Any<CancellationToken>());
        await Notifier.Received(1).NotifyTeamLeadersAsync(TeamId,
            Arg.Is<NotificationMessage>(m => IsMismatchNote(m) && m.LinkUrl == null && m.RefTable == NotificationRefs.KpiMismatch && m.RefId == 1), Arg.Any<CancellationToken>());
        // Thông báo thiếu KPI vẫn có link, nhưng đến trang có thật (trước đây trỏ vào /kpi không có route).
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => m.Title == "KPI không đạt khi kết thúc đơn" && m.LinkUrl == "/kpi/targets"), Arg.Any<CancellationToken>());
        await Notifier.Received(1).NotifyTeamLeadersAsync(TeamId,
            Arg.Is<NotificationMessage>(m => m.Title == "KPI không đạt khi kết thúc đơn" && m.LinkUrl == "/"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_ManyOperationsOff_NamesOnlyTheThreeWorstAndCountsTheRest()
    {
        Qc(0);
        for (var i = 0; i < 5; i++)
        {
            var id = 100 + i;
            _operations.Add(new Operation { OperationId = id, ProductId = ProductId, OperationCode = "X" + i, OperationName = "Công đoạn " + i, Status = OperationStatuses.Active });
            Reported(7, 10 + i, id);   // lệch 10..14
        }

        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => IsMismatchNote(m) && m.Message.Contains("ở 5 công đoạn") && m.Message.Contains("Công đoạn 4: lệch 14")
                && m.Message.Contains("Công đoạn 2: lệch 12") && !m.Message.Contains("Công đoạn 1: lệch 11")
                && m.Message.Contains("và 2 công đoạn khác")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_ManyWorkersAndOperations_NeverExceedsTheNotificationColumn()
    {
        Qc(0);
        for (var op = 0; op < 20; op++)
        {
            var id = 200 + op;
            _operations.Add(new Operation { OperationId = id, ProductId = ProductId, OperationCode = "Y" + op, OperationName = "Công đoạn dài " + new string('x', 40) + op, Status = OperationStatuses.Active });
            for (var w = 7; w <= 12; w++) Reported(w, 5 + op, id);
        }

        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => IsMismatchNote(m) && m.Message.Length <= 1000), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_LargeShortfallWithoutMismatch_OnlyLetsTheTeamLeaderDecideOnOvertime()
    {
        Reported(7, 60); Qc(60);   // thiếu 40%, không lệch

        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        await Notifier.Received(1).NotifyTeamLeadersAsync(TeamId,
            Arg.Is<NotificationMessage>(m => m.Message.Contains("tổ trưởng quyết định")), Arg.Any<CancellationToken>());
        await Notifier.DidNotReceive().NotifyTeamLeadersAsync(TeamId,
            Arg.Is<NotificationMessage>(m => IsMismatchNote(m)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_WhenTheTargetIsMetAndNothingIsOff_ClosesAsAchievedWithoutAnyNotice()
    {
        Reported(7, 100); Qc(100);

        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        _target.Result.Should().Be(KpiResults.Achieved);
        await Notifier.DidNotReceive().NotifyTeamLeadersAsync(Arg.Any<int>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_CannotBeRepeatedOnAClosedTarget()
    {
        Reported(7, 100); Qc(90);
        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        (await Sut().FinishAsync(1, default)).ErrorCode.Should().Be(ErrorCodes.KpiInvalidStatus);
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => IsMismatchNote(m)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_OfAClosedTarget_StillWorksForTheDetailWindow()
    {
        Reported(7, 100); Qc(90);
        await Sut().FinishAsync(1, default);

        var preview = await Preview();

        preview.CanFinish.Should().BeFalse();
        preview.Operations.Should().ContainSingle();
    }
}
