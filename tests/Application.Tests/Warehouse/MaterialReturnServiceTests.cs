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

/// <summary>
/// TH-10 / UC42 (DEV-ALL-112): tổ cắt trả NVL theo lệnh cắt, tổ may trả NVL (không theo đơn) và BTP (theo đơn – mã – màu – size),
/// cả hai tổ trả được NVL phế phẩm; Thủ kho xác nhận vào kho.
/// </summary>
public class MaterialReturnServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 9, 0, 0);
    private const int CutTeam = 2;
    private const int SewTeam = 5;
    private const int WorkOrderId = 30;
    private const int OrderId = 7;
    private const int LineId = 70;
    private const int FabricId = 1;
    private const int ThreadId = 2;
    private const int ScrapKgId = 3;
    private const int ScrapPieceId = 4;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<Team> _teams =
    [
        new() { TeamId = CutTeam, TeamName = "Tổ cắt", TeamType = TeamTypes.Cutting },
        new() { TeamId = SewTeam, TeamName = "Tổ may", TeamType = TeamTypes.Sewing },
        new() { TeamId = 9, TeamName = "Văn phòng", TeamType = TeamTypes.Office },
    ];

    private readonly List<WorkOrder> _workOrders = [new() { WorkOrderId = WorkOrderId, OrderId = OrderId, CuttingTeamId = CutTeam, Status = WorkOrderStatuses.Open }];
    private readonly List<Material> _materials =
    [
        new() { MaterialId = FabricId, MaterialCode = "V-0001", MaterialName = "Vải", Unit = "m", Category = "MAIN_FABRIC", Status = MaterialStatuses.Active },
        new() { MaterialId = ThreadId, MaterialCode = "PL-0001", MaterialName = "Chỉ", Unit = "cuộn", Category = "THREAD", Status = MaterialStatuses.Active },
        new() { MaterialId = ScrapKgId, MaterialCode = "PP-0001", MaterialName = "Vải vụn", Unit = "kg", Category = "SCRAP_FABRIC", Status = MaterialStatuses.Active },
        new() { MaterialId = ScrapPieceId, MaterialCode = "PP-0002", MaterialName = "Kim gãy", Unit = "cái", Category = "SCRAP_NEEDLE", Status = MaterialStatuses.Active },
    ];

    private readonly List<MaterialIssue> _issues = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<MaterialReturn> _returns = [];
    private readonly List<MaterialReturnLine> _returnLines = [];
    private readonly List<CuttingLayFabric> _layFabrics = [];
    private readonly List<QcBatch> _qc = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<MaterialReturn> _added = [];

    public MaterialReturnServiceTests()
    {
        // Gán Material.MaterialType (cột tính của DB) theo Category để lọc phế phẩm giống DB.
        foreach (var m in _materials)
            m.MaterialType = m.Category.StartsWith("SCRAP") ? MaterialTypes.Scrap : m.Category == "THREAD" ? MaterialTypes.Trim : MaterialTypes.Fabric;
        _orderLines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = 3 }.WithVariant(TestVariant.Make(3, "MH3")));
        _clock.Now.Returns(Now);
        _clock.Today.Returns(DateOnly.FromDateTime(Now));
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _codes.NextAsync<MaterialReturn>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<MaterialReturn, string>>>(),
            Arg.Any<CancellationToken>()).Returns("TV-20261028-001");
        Setup(_teams); Setup(_workOrders); Setup(_materials); Setup(_issues); Setup(_issueLines); Setup(_returns); Setup(_returnLines);
        Setup(_layFabrics); Setup(_qc); Setup(_orderLines);
        var returnRepo = Substitute.For<IGenericRepository<MaterialReturn>>();
        returnRepo.Query().Returns(_ => _returns.AsTestAsyncQueryable());
        returnRepo.AddAsync(Arg.Do<MaterialReturn>(r =>
        {
            r.MaterialReturnId = 500 + _returns.Count;
            r.ReturningTeam = _teams.Single(t => t.TeamId == r.ReturningTeamId);
            foreach (var l in r.MaterialReturnLines)
            {
                l.MaterialReturn = r;
                l.Material = _materials.SingleOrDefault(m => m.MaterialId == l.MaterialId);
                l.OrderLine = _orderLines.SingleOrDefault(o => o.OrderLineId == l.OrderLineId);
            }
            _added.Add(r); _returns.Add(r); _returnLines.AddRange(r.MaterialReturnLines);
        })).Returns(Task.CompletedTask);
        _uow.Repository<MaterialReturn>().Returns(returnRepo);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private MaterialReturnService Service() =>
        new(_uow, _stock, _codes, _notifier, _locks, _clock, new MaterialReturnCapacity(_uow));

    /// <summary>Phiếu cấp đã nhận một NVL cho tổ (theo lệnh cắt nếu có).</summary>
    private void Received(int teamId, int materialId, decimal qty, int? workOrderId = null)
    {
        var issue = new MaterialIssue
        {
            MaterialIssueId = 100 + _issues.Count, WarehouseType = WarehouseTypes.Material, ReceivingTeamId = teamId,
            WorkOrderId = workOrderId, OrderId = OrderId, Status = IssueStatuses.Received,
        };
        var line = new MaterialIssueLine
        {
            MaterialIssueLineId = 200 + _issueLines.Count, MaterialIssue = issue, MaterialId = materialId,
            Material = _materials.Single(m => m.MaterialId == materialId), IssuedQty = qty, ConfirmedQty = qty,
        };
        issue.MaterialIssueLines.Add(line);
        _issues.Add(issue); _issueLines.Add(line);
    }

    private void ReceivedPieces(int teamId, decimal qty)
    {
        var issue = new MaterialIssue
        {
            MaterialIssueId = 100 + _issues.Count, WarehouseType = WarehouseTypes.SemiFinished, ReceivingTeamId = teamId,
            OrderId = OrderId, Status = IssueStatuses.Received,
        };
        var line = new MaterialIssueLine
        {
            MaterialIssueLineId = 200 + _issueLines.Count, MaterialIssue = issue, OrderLineId = LineId,
            OrderLine = _orderLines[0], IssuedQty = qty, ConfirmedQty = qty,
        };
        issue.MaterialIssueLines.Add(line);
        _issues.Add(issue); _issueLines.Add(line);
    }

    private void PriorReturn(int teamId, string warehouse, string status, decimal qty, int? materialId = null, int? orderLineId = null,
        int? workOrderId = null)
    {
        var ret = new MaterialReturn
        {
            MaterialReturnId = 500 + _returns.Count, ReturningTeamId = teamId, WarehouseType = warehouse, Status = status,
            WorkOrderId = workOrderId,
        };
        var line = new MaterialReturnLine
        {
            MaterialReturnLineId = 600 + _returnLines.Count, MaterialReturn = ret, MaterialId = materialId,
            Material = _materials.SingleOrDefault(m => m.MaterialId == materialId), OrderLineId = orderLineId, ReturnedQty = qty,
            ConfirmedQty = status == HandoverStatuses.Confirmed ? qty : null,
        };
        ret.MaterialReturnLines.Add(line);
        _returns.Add(ret); _returnLines.Add(line);
    }

    private void Cut(decimal? usedM) => _layFabrics.Add(new CuttingLayFabric
    {
        MaterialId = FabricId, UsedM = usedM, CuttingLay = new CuttingLay { WorkOrderId = WorkOrderId },
    });

    private static CreateMaterialReturnDto Material(int? workOrderId, params (int Id, decimal Qty)[] lines) =>
        new(WarehouseTypes.Material, workOrderId, null, null, lines.Select(l => new CreateReturnLineDto(l.Id, null, l.Qty)).ToList());

    private static CreateMaterialReturnDto Pieces(int? orderId, decimal qty, int lineId = LineId) =>
        new(WarehouseTypes.SemiFinished, null, orderId, null, [new CreateReturnLineDto(null, lineId, qty)]);

    // ---------- Tổ cắt: NVL theo lệnh cắt ----------

    [Fact]
    public async Task CreateAsync_CuttingTeamReturnsFabricOfItsWorkOrder_SavesPendingReturnWithOrderAndWorkOrder()
    {
        Received(CutTeam, FabricId, 100, WorkOrderId);

        var result = await Service().CreateAsync(Material(WorkOrderId, (FabricId, 30)), CutTeam, default);

        result.IsSuccess.Should().BeTrue();
        var ret = _added.Should().ContainSingle().Subject;
        ret.Status.Should().Be(HandoverStatuses.Pending);
        ret.ReturningTeamId.Should().Be(CutTeam);
        ret.WorkOrderId.Should().Be(WorkOrderId);
        ret.OrderId.Should().Be(OrderId);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_CuttingTeamFabricNotReceivedForThatWorkOrder_ReturnsFailure()
    {
        Received(CutTeam, FabricId, 100, workOrderId: 99);

        var result = await Service().CreateAsync(Material(WorkOrderId, (FabricId, 10)), CutTeam, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        result.Message.Should().Be(WarehouseMessages.ReturnItemNotReturnable);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public async Task CreateAsync_CuttingTeam_CannotReturnMoreThanReceivedMinusCutMinusReturned(int qty, bool expected)
    {
        Received(CutTeam, FabricId, 100, WorkOrderId);
        Cut(30);
        PriorReturn(CutTeam, WarehouseTypes.Material, HandoverStatuses.Pending, 10, FabricId, workOrderId: WorkOrderId);

        var result = await Service().CreateAsync(Material(WorkOrderId, (FabricId, qty)), CutTeam, default);

        result.IsSuccess.Should().Be(expected);
        if (!expected) result.ErrorCode.Should().Be(ErrorCodes.ReturnExceedsIssued);
    }

    [Fact]
    public async Task CreateAsync_CuttingTeamLayWithoutLength_IsNotCappedByCutting()
    {
        Received(CutTeam, FabricId, 100, WorkOrderId);
        Cut(null);

        var result = await Service().CreateAsync(Material(WorkOrderId, (FabricId, 100)), CutTeam, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_RejectedAndCancelledReturns_DoNotUseUpTheReceivedQty()
    {
        Received(CutTeam, FabricId, 100, WorkOrderId);
        PriorReturn(CutTeam, WarehouseTypes.Material, HandoverStatuses.Rejected, 50, FabricId, workOrderId: WorkOrderId);
        PriorReturn(CutTeam, WarehouseTypes.Material, HandoverStatuses.Cancelled, 40, FabricId, workOrderId: WorkOrderId);

        var result = await Service().CreateAsync(Material(WorkOrderId, (FabricId, 100)), CutTeam, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_CuttingTeamNonScrapWithoutWorkOrder_ReturnsFailure()
    {
        Received(CutTeam, FabricId, 100, WorkOrderId);

        var result = await Service().CreateAsync(Material(null, (FabricId, 10)), CutTeam, default);

        result.Message.Should().Be(WarehouseMessages.ReturnWorkOrderRequired);
    }

    [Fact]
    public async Task CreateAsync_WorkOrderOfAnotherTeam_ReturnsFailure()
    {
        _workOrders[0].CuttingTeamId = 99;

        var result = await Service().CreateAsync(Material(WorkOrderId, (ScrapKgId, 1)), CutTeam, default);

        result.Message.Should().Be(WarehouseMessages.ReturnWorkOrderInvalid);
    }

    [Fact]
    public async Task CreateAsync_CuttingTeamReturnsBtp_ReturnsFailure()
    {
        var result = await Service().CreateAsync(Pieces(OrderId, 1), CutTeam, default);

        result.Message.Should().Be(WarehouseMessages.ReturnWarehouseNotAllowed);
    }

    // ---------- Tổ may: NVL không theo đơn ----------

    [Fact]
    public async Task CreateAsync_SewingTeamReturnsMaterialReceivedOnSeveralIssues_UsesTheTotal()
    {
        Received(SewTeam, ThreadId, 30);
        Received(SewTeam, ThreadId, 20, workOrderId: null);

        var tooMuch = await Service().CreateAsync(Material(null, (ThreadId, 51)), SewTeam, default);
        var ok = await Service().CreateAsync(Material(null, (ThreadId, 50)), SewTeam, default);

        ok.IsSuccess.Should().BeTrue();
        tooMuch.ErrorCode.Should().Be(ErrorCodes.ReturnExceedsIssued);
    }

    [Fact]
    public async Task CreateAsync_SewingTeamMaterialNeverReceived_ReturnsFailure()
    {
        var result = await Service().CreateAsync(Material(null, (ThreadId, 1)), SewTeam, default);

        result.Message.Should().Be(WarehouseMessages.ReturnItemNotReturnable);
    }

    [Fact]
    public async Task CreateAsync_SewingTeamPendingReturn_CountsAsReturned()
    {
        Received(SewTeam, ThreadId, 30);
        PriorReturn(SewTeam, WarehouseTypes.Material, HandoverStatuses.Pending, 25, ThreadId);

        var result = await Service().CreateAsync(Material(null, (ThreadId, 6)), SewTeam, default);

        result.ErrorCode.Should().Be(ErrorCodes.ReturnExceedsIssued);
    }

    [Fact]
    public async Task CreateAsync_NonFabricMaterialWithDecimalQty_ReturnsFailureNamingIt()
    {
        Received(SewTeam, ThreadId, 30);

        var result = await Service().CreateAsync(Material(null, (ThreadId, 1.5m)), SewTeam, default);

        result.Message.Should().Be(string.Format(WarehouseMessages.ReturnWholeQtyRequired, "Chỉ"));
    }

    // ---------- Phế phẩm ----------

    [Theory]
    [InlineData(SewTeam)]
    [InlineData(CutTeam)]
    public async Task CreateAsync_ScrapMaterial_HasNoCapAndNeedsNoReceipt(int teamId)
    {
        var result = await Service().CreateAsync(Material(null, (ScrapKgId, 12.5m), (ScrapPieceId, 40)), teamId, default);

        result.IsSuccess.Should().BeTrue();
        _added.Single().MaterialReturnLines.Select(l => l.MaterialId).Should().BeEquivalentTo([ScrapKgId, ScrapPieceId]);
    }

    [Fact]
    public async Task CreateAsync_ScrapCountedInPieces_RejectsDecimalButKgAllowsIt()
    {
        var piece = await Service().CreateAsync(Material(null, (ScrapPieceId, 1.5m)), SewTeam, default);
        var kg = await Service().CreateAsync(Material(null, (ScrapKgId, 1.5m)), SewTeam, default);

        piece.Message.Should().Be(string.Format(WarehouseMessages.ReturnWholeQtyRequired, "Kim gãy"));
        kg.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ScrapAlongsideFabricOfWorkOrder_OnlyFabricIsCapped()
    {
        Received(CutTeam, FabricId, 10, WorkOrderId);

        var result = await Service().CreateAsync(Material(WorkOrderId, (FabricId, 11), (ScrapKgId, 500)), CutTeam, default);

        result.ErrorCode.Should().Be(ErrorCodes.ReturnExceedsIssued);
        result.Message.Should().Contain("V-0001").And.NotContain("PP-0001");
    }

    [Fact]
    public async Task CreateAsync_DiscontinuedScrapMaterial_ReturnsFailure()
    {
        _materials.Single(m => m.MaterialId == ScrapKgId).Status = MaterialStatuses.Discontinued;

        var result = await Service().CreateAsync(Material(null, (ScrapKgId, 1)), SewTeam, default);

        result.Message.Should().Be(WarehouseMessages.IssueMaterialInvalid);
    }

    // ---------- Tổ may: BTP theo đơn – mã – màu – size ----------

    [Fact]
    public async Task CreateAsync_SewingTeamReturnsBtp_SavesLineOfTheOrderLine()
    {
        ReceivedPieces(SewTeam, 100);

        var result = await Service().CreateAsync(Pieces(OrderId, 10), SewTeam, default);

        result.IsSuccess.Should().BeTrue();
        var ret = _added.Single();
        ret.WarehouseType.Should().Be(WarehouseTypes.SemiFinished);
        ret.OrderId.Should().Be(OrderId);
        ret.MaterialReturnLines.Single().OrderLineId.Should().Be(LineId);
    }

    [Theory]
    [InlineData(55, true)]
    [InlineData(56, false)]
    public async Task CreateAsync_Btp_CannotReturnMoreThanReceivedMinusQcPassedMinusReturned(int qty, bool expected)
    {
        ReceivedPieces(SewTeam, 100);
        _qc.Add(new QcBatch { SewingTeamId = SewTeam, OrderLineId = LineId, FinishedQty = 30 });
        PriorReturn(SewTeam, WarehouseTypes.SemiFinished, HandoverStatuses.Confirmed, 15, orderLineId: LineId);

        var result = await Service().CreateAsync(Pieces(OrderId, qty), SewTeam, default);

        result.IsSuccess.Should().Be(expected);
        if (!expected) result.Message.Should().Contain("MH3").And.Contain("còn trả được 55");
    }

    [Fact]
    public async Task CreateAsync_BtpWithoutOrder_ReturnsFailure()
    {
        var result = await Service().CreateAsync(Pieces(null, 1), SewTeam, default);

        result.Message.Should().Be(WarehouseMessages.ReturnOrderRequired);
    }

    [Fact]
    public async Task CreateAsync_BtpOfOtherOrder_ReturnsFailure()
    {
        ReceivedPieces(SewTeam, 100);

        var result = await Service().CreateAsync(Pieces(OrderId + 1, 1), SewTeam, default);

        result.Message.Should().Be(WarehouseMessages.ReturnItemNotReturnable);
    }

    [Fact]
    public async Task CreateAsync_OfficeTeam_ReturnsTeamMismatch()
    {
        var result = await Service().CreateAsync(Material(null, (ScrapKgId, 1)), 9, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    // ---------- Danh sách còn trả được ----------

    [Fact]
    public async Task GetReturnableAsync_CuttingTeam_ListsFabricOfWorkOrderWithCutAndCap()
    {
        Received(CutTeam, FabricId, 100, WorkOrderId);
        Received(CutTeam, ThreadId, 5, 99);
        Cut(30);

        var result = await Service().GetReturnableAsync(CutTeam, new ReturnableQuery(WarehouseTypes.Material, WorkOrderId, null), default);

        var line = result.Data!.Should().ContainSingle().Subject;
        (line.ReceivedQty, line.UsedQty, line.RemainingQty).Should().Be((100m, 30m, 70m));
    }

    [Fact]
    public async Task GetReturnableAsync_SewingTeam_ListsMaterialsWithoutScrapAndWithoutExhaustedOnes()
    {
        Received(SewTeam, ThreadId, 30);
        Received(SewTeam, FabricId, 10);
        PriorReturn(SewTeam, WarehouseTypes.Material, HandoverStatuses.Confirmed, 10, FabricId);

        var result = await Service().GetReturnableAsync(SewTeam, new ReturnableQuery(WarehouseTypes.Material, null, null), default);

        result.Data!.Select(l => l.MaterialId).Should().Equal(ThreadId);
    }

    [Fact]
    public async Task GetReturnableAsync_SewingTeamBtp_ListsOrderLineWithQcPassed()
    {
        ReceivedPieces(SewTeam, 100);
        _qc.Add(new QcBatch { SewingTeamId = SewTeam, OrderLineId = LineId, FinishedQty = 40 });

        var result = await Service().GetReturnableAsync(SewTeam, new ReturnableQuery(WarehouseTypes.SemiFinished, null, OrderId), default);

        var line = result.Data!.Should().ContainSingle().Subject;
        (line.OrderLineId, line.ItemCode, line.ItemName, line.UsedQty, line.RemainingQty).Should().Be((LineId, "MH3", "Đen/M", 40m, 60m));
    }

    // ---------- Thủ kho xác nhận ----------

    [Fact]
    public async Task ConfirmAsync_MaterialReturn_AddsCountedQtyToTheMaterialWarehouse()
    {
        PriorReturn(SewTeam, WarehouseTypes.Material, HandoverStatuses.Pending, 8, ScrapKgId);
        IReadOnlyList<StockMovement>? posted = null;
        _stock.PostAsync(Arg.Do<IReadOnlyList<StockMovement>>(m => posted = m), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await Service().ConfirmAsync(500, new ConfirmMaterialReturnDto([new ConfirmReturnLineDto(600, 8, null)]), 900, default);

        result.IsSuccess.Should().BeTrue();
        _returns[0].Status.Should().Be(HandoverStatuses.Confirmed);
        posted.Should().ContainSingle().Which.Should().Match<StockMovement>(m => m.Key.Equals(StockKey.Material(ScrapKgId)) && m.QtyChange == 8);
    }

    [Fact]
    public async Task ConfirmAsync_BtpReturn_AddsCountedQtyToTheSemiFinishedWarehouse()
    {
        PriorReturn(SewTeam, WarehouseTypes.SemiFinished, HandoverStatuses.Pending, 10, orderLineId: LineId);
        IReadOnlyList<StockMovement>? posted = null;
        _stock.PostAsync(Arg.Do<IReadOnlyList<StockMovement>>(m => posted = m), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await Service().ConfirmAsync(500, new ConfirmMaterialReturnDto([new ConfirmReturnLineDto(600, 9, "Thiếu 1 bộ")]), 900, default);

        result.IsSuccess.Should().BeTrue();
        posted.Should().ContainSingle().Which.Should().Match<StockMovement>(m => m.Key.Equals(StockKey.SemiFinished(LineId, null)) && m.QtyChange == 9);
    }

    [Fact]
    public async Task ConfirmAsync_DifferentCountWithoutReason_ReturnsFailure()
    {
        PriorReturn(SewTeam, WarehouseTypes.Material, HandoverStatuses.Pending, 8, ScrapKgId);

        var noReason = await Service().ConfirmAsync(500, new ConfirmMaterialReturnDto([new ConfirmReturnLineDto(600, 7, null)]), 900, default);
        var tooMany = await Service().ConfirmAsync(500, new ConfirmMaterialReturnDto([new ConfirmReturnLineDto(600, 9, "x")]), 900, default);

        noReason.Message.Should().Be(WarehouseMessages.DiscrepancyReasonRequired);
        tooMany.Message.Should().Be(WarehouseMessages.HandoverConfirmedExceedsHanded);
        _returns[0].Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task RejectAndCancel_WorkOnlyWhilePending_AndCancelOnlyForTheOwnTeam()
    {
        PriorReturn(SewTeam, WarehouseTypes.Material, HandoverStatuses.Pending, 8, ScrapKgId);

        (await Service().CancelAsync(500, CutTeam, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        (await Service().RejectAsync(500, new ReasonDto("Rách"), 900, default)).IsSuccess.Should().BeTrue();
        (await Service().RejectAsync(500, new ReasonDto("Rách"), 900, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnOfAnotherTeam_ReturnsTeamMismatch()
    {
        PriorReturn(SewTeam, WarehouseTypes.Material, HandoverStatuses.Pending, 8, ScrapKgId);
        _returns[0].ReturningTeam = _teams[1];

        (await Service().GetByIdAsync(500, CutTeam, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    // ---------- Validator ----------

    [Theory]
    [InlineData(WarehouseTypes.Material, 1, null, true)]
    [InlineData(WarehouseTypes.Material, null, 1, false)]
    [InlineData(WarehouseTypes.SemiFinished, null, 1, true)]
    [InlineData(WarehouseTypes.SemiFinished, 1, null, false)]
    [InlineData("FINISHED", 1, null, false)]
    public void Validator_LineKindMustMatchWarehouse(string warehouse, int? materialId, int? orderLineId, bool valid)
    {
        var dto = new CreateMaterialReturnDto(warehouse, null, 1, null, [new CreateReturnLineDto(materialId, orderLineId, 1)]);

        new CreateMaterialReturnDtoValidator().Validate(dto).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData(1000000, true)]
    [InlineData(1000001, false)]
    [InlineData(1.5, false)]
    public void Validator_BtpQtyIsWholeUpToOneMillion(double qty, bool valid)
    {
        var dto = new CreateMaterialReturnDto(WarehouseTypes.SemiFinished, null, 1, null, [new CreateReturnLineDto(null, 1, (decimal)qty)]);

        new CreateMaterialReturnDtoValidator().Validate(dto).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_SameItemTwice_IsRejected()
    {
        var dto = new CreateMaterialReturnDto(WarehouseTypes.Material, null, null, null,
            [new CreateReturnLineDto(1, null, 1), new CreateReturnLineDto(1, null, 2)]);

        new CreateMaterialReturnDtoValidator().Validate(dto).IsValid.Should().BeFalse();
    }
}
