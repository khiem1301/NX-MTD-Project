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

/// <summary>TA-02 / UC48, UC49: kiểm kê kho — Thủ kho lập, Quản đốc / Admin duyệt điều chỉnh kho hoặc trả lại.</summary>
public class StocktakeServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 10);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IStockRepository _stockRepo = Substitute.For<IStockRepository>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<Stocktake> _stocktakes = new();
    private readonly List<StocktakeLine> _lines = new();
    private readonly List<Material> _materials = new() { new Material { MaterialId = 1, MaterialCode = "V-0001", MaterialName = "Vải", Unit = "m" } };
    private readonly List<OrderLine> _orderLines = new() { new OrderLine { OrderLineId = 101, Color = "Đen", SizeCode = "M" } };

    public StocktakeServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _stockRepo.GetOnHandAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(100m);
        _codes.NextAsync<Stocktake>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<Stocktake, string>>>(),
            Arg.Any<CancellationToken>()).Returns("KK-20261110-001");
        Setup(_stocktakes); Setup(_lines); Setup(_materials); Setup(_orderLines);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private StocktakeService Service() => new(_uow, _stock, _stockRepo, _codes, _notifier, _audit, _clock);

    private List<Stocktake> CaptureAdds()
    {
        var added = new List<Stocktake>();
        _uow.Repository<Stocktake>().AddAsync(Arg.Do<Stocktake>(s => { added.Add(s); _stocktakes.Add(s); _lines.AddRange(s.StocktakeLines); }));
        return added;
    }

    private Stocktake Existing(string status, string warehouse = WarehouseTypes.Material, params (decimal Book, decimal? Counted)[] lines)
    {
        var s = new Stocktake
        {
            StocktakeId = _stocktakes.Count + 1, StocktakeCode = $"KK-{_stocktakes.Count + 1}", WarehouseType = warehouse,
            StocktakeDate = Today, Status = status, CreatedAt = Now,
        };
        var i = 0;
        foreach (var (book, counted) in lines)
        {
            var l = new StocktakeLine
            {
                StocktakeLineId = s.StocktakeId * 10 + i++, StocktakeId = s.StocktakeId, Stocktake = s, BookQty = book, CountedQty = counted,
                MaterialId = warehouse == WarehouseTypes.Material ? 1 : null, MaterialColor = warehouse == WarehouseTypes.Material ? "Đen" : null,
                OrderLineId = warehouse == WarehouseTypes.Material ? null : 101,
            };
            s.StocktakeLines.Add(l); _lines.Add(l);
        }
        _stocktakes.Add(s);
        return s;
    }

    private static SaveStocktakeDto Material(decimal? counted, string color = "Đen") =>
        new(WarehouseTypes.Material, " Kho vải ", Today, new[] { new SaveStocktakeLineDto(1, color, null, counted, null) });

    // ---------- Lập phiếu ----------

    [Fact]
    public async Task Create_snapshots_the_book_qty_and_notifies_the_approvers()
    {
        var added = CaptureAdds();

        var result = await Service().CreateAsync(Material(92), default);

        result.IsSuccess.Should().BeTrue();
        var s = added.Should().ContainSingle().Subject;
        s.Status.Should().Be(StocktakeStatuses.PendingApproval);
        s.Scope.Should().Be("Kho vải");
        var line = s.StocktakeLines.Single();
        line.BookQty.Should().Be(100);
        line.CountedQty.Should().Be(92);
        line.MaterialColor.Should().Be("Đen");
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_count_means_the_line_matches_the_book()
    {
        var added = CaptureAdds();

        await Service().CreateAsync(Material(null), default);

        added.Single().StocktakeLines.Single().CountedQty.Should().BeNull();
    }

    [Fact]
    public async Task Create_rejects_bad_warehouse_future_date_negative_count_and_wrong_line_target()
    {
        (await Service().CreateAsync(new SaveStocktakeDto("OTHER", null, Today, new[] { new SaveStocktakeLineDto(1, "Đen", null, 1, null) }), default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        (await Service().CreateAsync(Material(5) with { StocktakeDate = Today.AddDays(1) }, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        (await Service().CreateAsync(Material(-1), default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        // Kho BTP phải chọn dòng đơn, không chọn NVL
        (await Service().CreateAsync(new SaveStocktakeDto(WarehouseTypes.SemiFinished, null, Today,
            new[] { new SaveStocktakeLineDto(1, "Đen", null, 5, null) }), default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Create_rejects_duplicate_lines_and_unknown_items()
    {
        var twice = new SaveStocktakeDto(WarehouseTypes.Material, null, Today,
            new[] { new SaveStocktakeLineDto(1, "Đen", null, 5, null), new SaveStocktakeLineDto(1, " Đen ", null, 6, null) });
        (await Service().CreateAsync(twice, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);

        var unknown = new SaveStocktakeDto(WarehouseTypes.Material, null, Today, new[] { new SaveStocktakeLineDto(99, "Đen", null, 5, null) });
        (await Service().CreateAsync(unknown, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task Piece_warehouses_use_the_order_line_as_the_key()
    {
        var added = CaptureAdds();

        var result = await Service().CreateAsync(new SaveStocktakeDto(WarehouseTypes.Finished, null, Today,
            new[] { new SaveStocktakeLineDto(null, null, 101, 40, null) }), default);

        result.IsSuccess.Should().BeTrue();
        added.Single().StocktakeLines.Single().OrderLineId.Should().Be(101);
        await _stockRepo.Received().GetOnHandAsync(Arg.Is<StockKey>(k => k.WarehouseType == WarehouseTypes.Finished && k.OrderLineId == 101), Arg.Any<CancellationToken>());
    }

    // ---------- Duyệt ----------

    [Fact]
    public async Task Approve_posts_one_adjustment_per_line_that_differs_from_the_book()
    {
        var s = Existing(StocktakeStatuses.PendingApproval, WarehouseTypes.Material, (100, 92), (50, 50), (30, null), (10, 14));

        var result = await Service().ApproveAsync(s.StocktakeId, default);

        result.IsSuccess.Should().BeTrue();
        s.Status.Should().Be(StocktakeStatuses.Approved);
        s.ApprovedAt.Should().Be(Now);
        await _stock.Received(1).PostAsync(
            Arg.Is<IReadOnlyList<StockMovement>>(m => m.Count == 2 && m[0].QtyChange == -8 && m[1].QtyChange == 4
                && m.All(x => x.SourceDocType == StockDocTypes.Stocktake && x.Key.WarehouseType == WarehouseTypes.Material)),
            Arg.Any<CancellationToken>());
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Stocktake) && e.Action == AuditActions.Approve));
    }

    [Fact]
    public async Task Approve_with_everything_matching_does_not_touch_the_stock()
    {
        var s = Existing(StocktakeStatuses.PendingApproval, WarehouseTypes.Material, (100, 100), (20, null));

        var result = await Service().ApproveAsync(s.StocktakeId, default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(WarehouseMessages.StocktakeApprovedNoChange);
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_keeps_the_stocktake_pending_when_the_adjustment_would_make_stock_negative()
    {
        var s = Existing(StocktakeStatuses.PendingApproval, WarehouseTypes.Material, (100, 10));
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, "x"));

        var result = await Service().ApproveAsync(s.StocktakeId, default);

        result.IsSuccess.Should().BeFalse();
        s.Status.Should().Be(StocktakeStatuses.PendingApproval);
    }

    [Fact]
    public async Task Piece_warehouse_adjustments_use_the_matching_warehouse_key()
    {
        var s = Existing(StocktakeStatuses.PendingApproval, WarehouseTypes.SemiFinished, (60, 55));

        await Service().ApproveAsync(s.StocktakeId, default);

        await _stock.Received(1).PostAsync(
            Arg.Is<IReadOnlyList<StockMovement>>(m => m.Single().Key.WarehouseType == WarehouseTypes.SemiFinished
                && m.Single().Key.OrderLineId == 101 && m.Single().QtyChange == -5),
            Arg.Any<CancellationToken>());
    }

    // ---------- Trả lại / sửa lại ----------

    [Fact]
    public async Task Return_needs_a_pending_stocktake_and_stores_the_reason()
    {
        var pending = Existing(StocktakeStatuses.PendingApproval, WarehouseTypes.Material, (100, 90));
        var approved = Existing(StocktakeStatuses.Approved, WarehouseTypes.Material, (100, 90));

        (await Service().ReturnAsync(approved.StocktakeId, new ReasonDto("x"), default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        var result = await Service().ReturnAsync(pending.StocktakeId, new ReasonDto(" đếm lại ô B3 "), default);

        result.IsSuccess.Should().BeTrue();
        pending.Status.Should().Be(StocktakeStatuses.Returned);
        pending.ReturnReason.Should().Be("đếm lại ô B3");
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Only_a_returned_stocktake_can_be_edited_and_resubmitted_with_a_fresh_snapshot()
    {
        var pending = Existing(StocktakeStatuses.PendingApproval, WarehouseTypes.Material, (100, 90));
        (await Service().UpdateAsync(pending.StocktakeId, Material(95), default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);

        var returned = Existing(StocktakeStatuses.Returned, WarehouseTypes.Material, (100, 90));
        returned.ReturnReason = "đếm lại";
        _stockRepo.GetOnHandAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(120m);

        var result = await Service().UpdateAsync(returned.StocktakeId, Material(118), default);

        result.IsSuccess.Should().BeTrue();
        returned.Status.Should().Be(StocktakeStatuses.PendingApproval);
        returned.ReturnReason.Should().BeNull();
        var line = returned.StocktakeLines.Single();
        line.BookQty.Should().Be(120);
        line.CountedQty.Should().Be(118);
    }

    [Fact]
    public async Task A_returned_stocktake_cannot_switch_to_another_warehouse()
    {
        var returned = Existing(StocktakeStatuses.Returned, WarehouseTypes.Material, (100, 90));

        var result = await Service().UpdateAsync(returned.StocktakeId, new SaveStocktakeDto(WarehouseTypes.Finished, null, Today,
            new[] { new SaveStocktakeLineDto(null, null, 101, 5, null) }), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Detail_shows_the_difference_per_line()
    {
        var s = Existing(StocktakeStatuses.PendingApproval, WarehouseTypes.Material, (100, 92), (30, null));
        s.StocktakeLines.First().Material = _materials[0];
        s.StocktakeLines.Last().Material = _materials[0];

        var dto = (await Service().GetByIdAsync(s.StocktakeId, default)).Data!;

        dto.Lines.Select(l => l.DifferenceQty).Should().Equal(-8m, 0m);
    }

    [Fact]
    public void Validator_requires_a_known_warehouse_lines_and_non_negative_counts()
    {
        var validator = new SaveStocktakeDtoValidator();
        validator.Validate(Material(5)).IsValid.Should().BeTrue();
        validator.Validate(Material(null)).IsValid.Should().BeTrue();
        validator.Validate(Material(-1)).IsValid.Should().BeFalse();
        validator.Validate(Material(5) with { WarehouseType = "X" }).IsValid.Should().BeFalse();
        validator.Validate(Material(5) with { Lines = Array.Empty<SaveStocktakeLineDto>() }).IsValid.Should().BeFalse();
    }
}
