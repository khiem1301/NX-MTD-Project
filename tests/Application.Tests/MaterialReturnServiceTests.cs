using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>TH-10 / UC42: tổ cắt trả vải thừa theo phiếu cấp gốc, Thủ kho xác nhận vào kho NVL.</summary>
public class MaterialReturnServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 9, 0, 0);
    private const int CutTeam = 2;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<Team> _teams = new();
    private readonly List<MaterialIssue> _issues = new();
    private readonly List<MaterialIssueLine> _issueLines = new();
    private readonly List<MaterialReturn> _returns = new();
    private readonly List<MaterialReturnLine> _returnLines = new();
    private readonly Team _team = new() { TeamId = CutTeam, TeamName = "Tổ cắt", TeamType = TeamTypes.Cutting };
    private readonly Material _fabric = new() { MaterialId = 1, MaterialCode = "V-0001", MaterialName = "Vải", Unit = "m" };
    private readonly MaterialIssue _issue;
    private readonly MaterialIssueLine _line;

    public MaterialReturnServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(DateOnly.FromDateTime(Now));
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _codes.NextAsync<MaterialReturn>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<MaterialReturn, string>>>(),
            Arg.Any<CancellationToken>()).Returns("TV-20261028-001");
        _issue = new MaterialIssue
        {
            MaterialIssueId = 10, IssueCode = "PC-1", WarehouseType = WarehouseTypes.Material, ReceivingTeamId = CutTeam,
            ReceivingTeam = _team, Status = IssueStatuses.Received, OrderId = 7, Order = new CustomerOrder { OrderId = 7, OrderCode = "DH-1" },
        };
        _line = new MaterialIssueLine
        {
            MaterialIssueLineId = 100, MaterialIssueId = 10, MaterialIssue = _issue, MaterialId = 1, Material = _fabric,
            IssuedQty = 120, ConfirmedQty = 100,
        };
        _issue.MaterialIssueLines.Add(_line);
        _teams.Add(_team); _issues.Add(_issue); _issueLines.Add(_line);
        Setup(_teams); Setup(_issues); Setup(_issueLines); Setup(_returns); Setup(_returnLines);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private MaterialReturnService Service() => new(_uow, _stock, _codes, _notifier, _locks, _clock);

    private MaterialReturn Return(string status, decimal returned, decimal? confirmed = null)
    {
        var ret = new MaterialReturn
        {
            MaterialReturnId = _returns.Count + 1, ReturnCode = $"TV-{_returns.Count + 1}", MaterialIssueId = 10, MaterialIssue = _issue,
            Status = status, CreatedAt = Now,
        };
        var line = new MaterialReturnLine
        {
            MaterialReturnLineId = ret.MaterialReturnId * 10, MaterialReturnId = ret.MaterialReturnId, MaterialReturn = ret,
            MaterialIssueLineId = 100, MaterialIssueLine = _line, ReturnedQty = returned, ConfirmedQty = confirmed,
        };
        ret.MaterialReturnLines.Add(line);
        _returns.Add(ret); _returnLines.Add(line);
        return ret;
    }

    private List<MaterialReturn> CaptureAdds()
    {
        var added = new List<MaterialReturn>();
        _uow.Repository<MaterialReturn>().AddAsync(Arg.Do<MaterialReturn>(r =>
        {
            r.MaterialIssue = _issue;
            foreach (var l in r.MaterialReturnLines) { l.MaterialReturn = r; l.MaterialIssueLine = _line; }
            added.Add(r); _returns.Add(r); _returnLines.AddRange(r.MaterialReturnLines);
        }));
        return added;
    }

    private static CreateMaterialReturnDto Create(decimal qty) => new(10, null, new[] { new CreateReturnLineDto(100, qty) });

    // ---------- Lập phiếu trả ----------

    [Fact]
    public async Task Cutting_team_returns_part_of_the_fabric_it_received()
    {
        var added = CaptureAdds();

        var result = await Service().CreateAsync(Create(30), CutTeam, default);

        result.IsSuccess.Should().BeTrue();
        var ret = added.Should().ContainSingle().Subject;
        ret.Status.Should().Be(HandoverStatuses.Pending);
        ret.MaterialReturnLines.Single().ReturnedQty.Should().Be(30);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Return_cannot_exceed_what_was_received_and_not_yet_returned()
    {
        Return(HandoverStatuses.Confirmed, 40, confirmed: 38);   // đã trả 38 (kho đếm)
        Return(HandoverStatuses.Pending, 20);                    // đang chờ trả 20 → còn trả được 100 − 38 − 20 = 42

        var over = await Service().CreateAsync(Create(43), CutTeam, default);

        over.ErrorCode.Should().Be(ErrorCodes.ReturnExceedsIssued);
        over.Message.Should().Contain("còn trả được 42");

        CaptureAdds();
        (await Service().CreateAsync(Create(42), CutTeam, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Rejected_and_cancelled_returns_do_not_use_up_the_issued_qty()
    {
        Return(HandoverStatuses.Rejected, 100);
        Return(HandoverStatuses.Cancelled, 100);
        CaptureAdds();

        var result = await Service().CreateAsync(Create(100), CutTeam, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Only_a_cutting_team_can_return_fabric()
    {
        _team.TeamType = TeamTypes.Sewing;

        var result = await Service().CreateAsync(Create(10), CutTeam, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    [Fact]
    public async Task A_team_cannot_return_against_another_teams_issue_or_an_unreceived_issue()
    {
        (await Service().CreateAsync(Create(10), 99, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);

        _issue.Status = IssueStatuses.PendingReceipt;
        (await Service().CreateAsync(Create(10), CutTeam, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);

        _issue.Status = IssueStatuses.Received;
        _issue.WarehouseType = WarehouseTypes.SemiFinished;
        (await Service().CreateAsync(Create(10), CutTeam, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task Return_line_must_belong_to_the_chosen_issue()
    {
        var result = await Service().CreateAsync(new CreateMaterialReturnDto(10, null, new[] { new CreateReturnLineDto(999, 5) }), CutTeam, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Returnable_lists_only_lines_with_fabric_left_to_return()
    {
        Return(HandoverStatuses.Confirmed, 100, confirmed: 100);   // trả hết dòng này
        var other = new MaterialIssueLine
        {
            MaterialIssueLineId = 101, MaterialIssueId = 10, MaterialIssue = _issue, MaterialId = 1, Material = _fabric,
            IssuedQty = 50, ConfirmedQty = 50,
        };
        _issue.MaterialIssueLines.Add(other); _issueLines.Add(other);

        var lines = (await Service().GetReturnableAsync(CutTeam, default)).Data!;

        var only = lines.Should().ContainSingle().Subject;
        only.MaterialIssueLineId.Should().Be(101);
        only.RemainingQty.Should().Be(50);
    }

    // ---------- Thủ kho xác nhận / từ chối ----------

    [Fact]
    public async Task Confirm_adds_the_counted_fabric_back_to_the_material_warehouse()
    {
        var ret = Return(HandoverStatuses.Pending, 30);
        var lineId = ret.MaterialReturnLines.Single().MaterialReturnLineId;

        var result = await Service().ConfirmAsync(ret.MaterialReturnId,
            new ConfirmMaterialReturnDto(new[] { new ConfirmReturnLineDto(lineId, 30, null) }), 9, default);

        result.IsSuccess.Should().BeTrue();
        ret.Status.Should().Be(HandoverStatuses.Confirmed);
        await _stock.Received(1).PostAsync(
            Arg.Is<IReadOnlyList<StockMovement>>(m => m.Count == 1 && m[0].QtyChange == 30 && m[0].Key.MaterialId == 1
                && m[0].SourceDocType == StockDocTypes.MaterialReturn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_with_a_different_count_needs_a_reason_and_cannot_exceed_the_returned_qty()
    {
        var ret = Return(HandoverStatuses.Pending, 30);
        var lineId = ret.MaterialReturnLines.Single().MaterialReturnLineId;

        (await Service().ConfirmAsync(ret.MaterialReturnId, new ConfirmMaterialReturnDto(new[] { new ConfirmReturnLineDto(lineId, 28, null) }), 9, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        (await Service().ConfirmAsync(ret.MaterialReturnId, new ConfirmMaterialReturnDto(new[] { new ConfirmReturnLineDto(lineId, 31, "x") }), 9, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        var ok = await Service().ConfirmAsync(ret.MaterialReturnId, new ConfirmMaterialReturnDto(new[] { new ConfirmReturnLineDto(lineId, 28, "ướt 2m") }), 9, default);

        ok.IsSuccess.Should().BeTrue();
        ret.MaterialReturnLines.Single().ConfirmedQty.Should().Be(28);
        ret.MaterialReturnLines.Single().DiscrepancyReason.Should().Be("ướt 2m");
    }

    [Fact]
    public async Task Reject_and_cancel_work_only_while_pending_and_cancel_only_for_the_own_team()
    {
        var pending = Return(HandoverStatuses.Pending, 10);
        var done = Return(HandoverStatuses.Confirmed, 10, confirmed: 10);

        (await Service().RejectAsync(done.MaterialReturnId, new ReasonDto("x"), 9, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        (await Service().CancelAsync(pending.MaterialReturnId, 99, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        (await Service().CancelAsync(pending.MaterialReturnId, CutTeam, default)).IsSuccess.Should().BeTrue();
        pending.Status.Should().Be(HandoverStatuses.Cancelled);

        var second = Return(HandoverStatuses.Pending, 10);
        (await Service().RejectAsync(second.MaterialReturnId, new ReasonDto(" quá bẩn "), 9, default)).IsSuccess.Should().BeTrue();
        second.RejectReason.Should().Be("quá bẩn");
    }

    [Fact]
    public async Task A_team_cannot_open_another_teams_return()
    {
        var ret = Return(HandoverStatuses.Pending, 10);

        (await Service().GetByIdAsync(ret.MaterialReturnId, 99, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
        (await Service().GetByIdAsync(ret.MaterialReturnId, CutTeam, default)).IsSuccess.Should().BeTrue();
        (await Service().GetByIdAsync(ret.MaterialReturnId, null, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validators_reject_empty_duplicate_and_non_positive_lines()
    {
        var create = new CreateMaterialReturnDtoValidator();
        create.Validate(Create(5)).IsValid.Should().BeTrue();
        create.Validate(Create(0)).IsValid.Should().BeFalse();
        create.Validate(new CreateMaterialReturnDto(10, null, Array.Empty<CreateReturnLineDto>())).IsValid.Should().BeFalse();
        create.Validate(new CreateMaterialReturnDto(10, null, new[] { new CreateReturnLineDto(1, 5), new CreateReturnLineDto(1, 6) })).IsValid.Should().BeFalse();

        var confirm = new ConfirmMaterialReturnDtoValidator();
        confirm.Validate(new ConfirmMaterialReturnDto(new[] { new ConfirmReturnLineDto(1, 0, null) })).IsValid.Should().BeTrue();
        confirm.Validate(new ConfirmMaterialReturnDto(new[] { new ConfirmReturnLineDto(1, -1, null) })).IsValid.Should().BeFalse();
    }
}
