using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Kết thúc đơn của tổ: lệch kiểm cuối đơn → phiếu hàng hỏng cho công nhân, chốt KPI, tăng ca khi không đạt.</summary>
public class EndOfOrderServiceTests : ProductionTestBase
{
    private const int TeamId = 5, OrderId = 1, ProductId = 1, FinalOp = 9, LineId = 11;

    private readonly IPieceRateService _rates = Substitute.For<IPieceRateService>();
    private readonly IRequestService _requests = Substitute.For<IRequestService>();
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
        SetupQueryable(new List<AccountingPeriod>());
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        Repo<DefectRecord>().When(r => r.AddAsync(Arg.Any<DefectRecord>())).Do(c =>
        {
            var d = c.Arg<DefectRecord>();
            d.DefectRecordId = _defects.Count + 1;
            _defects.Add(d);
        });
        AsManager();
        Settings.GetIntAsync(SettingKeys.KpiOtAutoThresholdPct, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(10);
        _rates.FindEffectiveAsync(FinalOp, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EffectivePieceRate(70, 1_000m));

        _lines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, OrderedQty = 100 }
            .WithVariant(TestVariant.Make(ProductId, "MH001", "Đen", "M")));
        _operations.Add(new Operation
        {
            OperationId = FinalOp, ProductId = ProductId, OperationCode = "OPF", OperationName = "Hoàn thiện",
            IsFinalStep = true, Status = OperationStatuses.Active,
        });
        _employees.Add(new Employee { EmployeeId = 7, EmployeeCode = "CN7", FullName = "Công nhân 7" });
        _employees.Add(new Employee { EmployeeId = 8, EmployeeCode = "CN8", FullName = "Công nhân 8" });
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

    private EndOfOrderService Sut() => new(Uow, Guard(), _rates, _requests, Notifier, Audit, Settings, User, Clock);

    private void Reported(int worker, int qty) => _outputs.Add(new OutputRecord
    {
        SewingTeamId = TeamId, WorkerId = worker, OperationId = FinalOp, OrderLineId = LineId, Quantity = qty,
    });

    private void Qc(int qty) => _qc.Add(new QcBatch { SewingTeamId = TeamId, OrderLineId = LineId, FinishedQty = qty });

    [Theory]
    [InlineData(10, new[] { 6, 4 }, new[] { 6, 4 })]
    [InlineData(5, new[] { 70, 30 }, new[] { 4, 1 })]
    [InlineData(1, new[] { 10, 10, 10 }, new[] { 1, 0, 0 })]
    [InlineData(0, new[] { 3, 3 }, new[] { 0, 0 })]
    public void Distribute_SplitsByWeightAndKeepsTheTotal(int total, int[] weights, int[] expected) =>
        EndOfOrderService.Distribute(total, weights).Should().Equal(expected);

    [Fact]
    public async Task Preview_SplitsTheMismatchBetweenWorkersByReportedOutput()
    {
        Reported(7, 60); Reported(8, 40);   // báo 100
        Qc(90);                              // QC đạt 90 → lệch 10

        var preview = (await Sut().PreviewAsync(1, default)).Data!;

        preview.ActualQty.Should().Be(90);
        preview.Rows.Should().Contain(r => r.WorkerId == 7 && r.MismatchQty == 6)
            .And.Contain(r => r.WorkerId == 8 && r.MismatchQty == 4);
        preview.Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_ScrapAlreadyRecordedIsNotCountedAsMismatch()
    {
        Reported(7, 100);
        Qc(90);
        _defects.Add(new DefectRecord
        {
            SewingTeamId = TeamId, OrderLineId = LineId, DefectType = DefectTypes.Scrap, Status = DefectStatuses.Recorded, Quantity = 10,
        });

        (await Sut().PreviewAsync(1, default)).Data!.Rows.Should().OnlyContain(r => r.MismatchQty == 0);
    }

    [Fact]
    public async Task Preview_WithoutAFinalOperationOrApprovedRate_IsBlocked()
    {
        Reported(7, 100); Qc(90);
        _operations[0].IsFinalStep = false;
        (await Sut().PreviewAsync(1, default)).Data!.Blockers.Should().ContainSingle().Which.Should().Be(Messages.KpiFinishNoFinalOperation);

        _operations[0].IsFinalStep = true;
        _rates.FindEffectiveAsync(FinalOp, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((EffectivePieceRate?)null);
        (await Sut().FinishAsync(1, default)).ErrorCode.Should().Be(ErrorCodes.KpiFinishBlocked);
        _target.Status.Should().Be(KpiStatuses.Active);
        _defects.Should().BeEmpty();
    }

    [Fact]
    public async Task Finish_WritesScrapDefectsForTheMismatch_ClosesKpiAndAutoApprovesSmallShortfallOvertime()
    {
        Reported(7, 60); Reported(8, 40); Qc(95);   // thiếu 5% ≤ 10%, lệch 5

        var result = await Sut().FinishAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        _defects.Should().OnlyContain(d => d.DefectType == DefectTypes.Scrap && d.DefectReasonCode == DefectReasons.EndOfOrderMismatch
            && d.PieceRateId == 70 && d.OperationId == FinalOp && d.SewingTeamId == TeamId);
        _defects.Sum(d => d.Quantity).Should().Be(5);
        _defects.Select(d => d.ResponsibleWorkerId).Should().BeSubsetOf(new[] { 7, 8 });
        _target.Status.Should().Be(KpiStatuses.Closed);
        _target.Result.Should().Be(KpiResults.NotAchieved);
        await _requests.Received(1).StageSystemRequestAsync(
            Arg.Is<RequestDraft>(r => r.RequestType == RequestTypes.Overtime && r.KpiTargetId == 1),
            Arg.Any<CancellationToken>(), TeamId, Arg.Is<string?>(n => n != null));   // tự duyệt
        Notifier.Received().NotifyEmployees(Arg.Any<IEnumerable<int>>(), Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task Finish_LargeShortfall_SendsOvertimeToTheLeaderWithoutAutoApproval()
    {
        Reported(7, 60); Qc(60);   // thiếu 40%, không lệch

        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        _defects.Should().BeEmpty();
        await _requests.Received(1).StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>(), TeamId, null);
    }

    [Fact]
    public async Task Finish_WhenTheTargetIsMet_ClosesAsAchievedWithoutOvertime()
    {
        Reported(7, 100); Qc(100);

        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();

        _target.Result.Should().Be(KpiResults.Achieved);
        await _requests.DidNotReceiveWithAnyArgs().StageSystemRequestAsync(default!, default);
    }

    [Fact]
    public async Task Finish_CannotBeRepeatedOnAClosedTarget()
    {
        Reported(7, 100); Qc(90);
        (await Sut().FinishAsync(1, default)).IsSuccess.Should().BeTrue();
        var firstDefects = _defects.Count;

        (await Sut().FinishAsync(1, default)).ErrorCode.Should().Be(ErrorCodes.KpiInvalidStatus);
        _defects.Should().HaveCount(firstDefects);
    }
}
