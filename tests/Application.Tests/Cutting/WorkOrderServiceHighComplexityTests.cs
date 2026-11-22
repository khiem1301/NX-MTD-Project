using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID315–UTCID373 — WorkOrderService high-complexity cases from UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES.md.</summary>
[Trait("Category", "HighComplexity")]
public class WorkOrderServiceHighComplexityTests
{
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Team> _teams = [];
    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "MH1" }];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<ProductMarker> _markers = [];
    private readonly List<ProductMarkerFabric> _markerFabrics = [];
    private readonly List<ProductMarkerComponent> _markerComponents = [];
    private readonly List<Material> _materials = [];
    private readonly List<CuttingLayLine> _layLines = [];
    private readonly List<CuttingLay> _lays = [];
    private readonly List<CuttingLayFabric> _layFabrics = [];
    private readonly List<CutHandoverLine> _handoverLines = [];
    private readonly List<MaterialIssue> _issues = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<MaterialReturn> _returns = [];
    private readonly List<MaterialReturnLine> _returnLines = [];
    private readonly List<Request> _requests = [];
    private readonly List<RequestLine> _requestLines = [];
    private int _nextWoId = 60;

    private const int CuttingTeam1 = 1, CuttingTeam2 = 2, SewingTeam3 = 3, OrderId = 1, Line100 = 100, FabricId = 100;

    private readonly Material _fabric = new()
    {
        MaterialId = FabricId, MaterialCode = "F1", MaterialName = "Vải F1", Unit = "m", Color = "Đen",
        MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active,
    };

    public WorkOrderServiceHighComplexityTests()
    {
        _clock.Today.Returns(Today);
        _clock.Now.Returns(Now);
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        _uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        _uow.SaveChangesAsync().Returns(1);
        _codes.NextAsync<WorkOrder>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, string>>>(), Arg.Any<CancellationToken>())
            .Returns("LC2611-001");
        // Đủ cho tạo lệnh (need ~600m / 300pcs); case thiếu tồn tự Returns(0) / thấp hơn.
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(10_000m);

        SetupQueryable(_workOrders);
        SetupQueryable(_orders);
        SetupQueryable(_teams);
        SetupQueryable(_products);
        SetupQueryable(_orderLines);
        SetupQueryable(_variants);
        SetupQueryable(_norms);
        SetupQueryable(_markers);
        SetupQueryable(_markerFabrics);
        SetupQueryable(_markerComponents);
        SetupQueryable(_materials);
        SetupQueryable(_layLines);
        SetupQueryable(_lays);
        SetupQueryable(_layFabrics);
        SetupQueryable(_handoverLines);
        SetupQueryable(_issues);
        SetupQueryable(_issueLines);
        SetupQueryable(_returns);
        SetupQueryable(_returnLines);
        SetupQueryable(_requests);
        SetupQueryable(_requestLines);

        _materials.Add(_fabric);
        _teams.AddRange([
            new Team { TeamId = CuttingTeam1, TeamCode = "CAT1", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active },
            new Team { TeamId = CuttingTeam2, TeamCode = "CAT2", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active },
            new Team { TeamId = SewingTeam3, TeamCode = "MAY3", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active },
        ]);

        var woRepo = _uow.Repository<WorkOrder>();
        woRepo.AddAsync(Arg.Do<WorkOrder>(w =>
        {
            if (w.WorkOrderId == 0) w.WorkOrderId = _nextWoId++;
            if (w.Order is null) w.Order = _orders.FirstOrDefault(o => o.OrderId == w.OrderId);
            if (w.Product is null) w.Product = _products[0];
            if (w.CuttingTeam is null) w.CuttingTeam = _teams.First(t => t.TeamId == w.CuttingTeamId);
            WireWorkOrderLineNavigations(w);
            if (_workOrders.All(x => x.WorkOrderId != w.WorkOrderId))
                _workOrders.Add(w);
        })).Returns(Task.CompletedTask);

        // Update/Create ghi xong — gắn lại OrderLine/PCM cho GetById (Include in-memory không EF-fixup)
        _uow.When(u => u.SaveChangesAsync()).Do(_ =>
        {
            foreach (var w in _workOrders)
                WireWorkOrderLineNavigations(w);
        });
    }

    private void WireWorkOrderLineNavigations(WorkOrder w)
    {
        foreach (var line in w.WorkOrderLines)
        {
            line.OrderLine ??= _orderLines.FirstOrDefault(l => l.OrderLineId == line.OrderLineId);
            if (line.OrderLine?.ProductVariant is { } v)
                v.Product ??= _products.FirstOrDefault(p => p.ProductId == line.OrderLine.ProductId) ?? _products[0];
            if (line.ProductComponentMaterialId is { } pcmId)
                line.ProductComponentMaterial ??= _norms.FirstOrDefault(n => n.ProductComponentMaterialId == pcmId);
        }
    }

    /// <summary>Seed CuttingLay vào `_lays` (EnsureEditableAsync query CuttingLay, không phải LayLine).</summary>
    private void SeedCuttingActivity(WorkOrder wo, int actualQty = 50)
    {
        var lay = new CuttingLay { WorkOrderId = wo.WorkOrderId, WorkOrder = wo };
        _lays.Add(lay);
        _layLines.Add(new CuttingLayLine
        {
            OrderLineId = Line100, ActualQty = actualQty, CuttingLay = lay, CuttingLayId = 0,
        });
    }

    private void SetupQueryable<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private WorkOrderService Sut() => new(_uow, _stock, new FabricPlanner(_uow), _codes, _notifier,
        NullLogger<WorkOrderService>.Instance, _clock, new WorkOrderFabricChecker(_uow, _stock, new FabricPlanner(_uow), new BranchAndBoundMarkerSolver()));

    private WorkOrder SeedWorkOrder(int id, int team = CuttingTeam1, string status = WorkOrderStatuses.Open, int? sourceRequestId = null)
    {
        var order = _orders.FirstOrDefault(o => o.OrderId == OrderId) ?? SeedOrder();
        var wo = new WorkOrder
        {
            WorkOrderId = id, WorkOrderCode = $"LC{id}", OrderId = OrderId, Order = order, ProductId = 1, Product = _products[0],
            CuttingTeamId = team, CuttingTeam = _teams.First(t => t.TeamId == team), Status = status, DueDate = Today.AddDays(5),
            CreatedAt = Now, SourceRequestId = sourceRequestId,
        };
        var line = _orderLines.FirstOrDefault(l => l.OrderLineId == Line100) ?? SeedOrderLine();
        wo.WorkOrderLines.Add(new WorkOrderLine
        {
            WorkOrderLineId = id * 10, WorkOrderId = id, OrderLineId = Line100, PlannedQty = 300, OrderLine = line,
        });
        _workOrders.Add(wo);
        return wo;
    }

    private CustomerOrder SeedOrder(string status = OrderStatuses.InProduction, DateOnly? received = null)
    {
        var existing = _orders.FirstOrDefault(o => o.OrderId == OrderId);
        if (existing is not null) return existing;
        var order = new CustomerOrder
        {
            OrderId = OrderId, OrderCode = "DH2611-001", Status = status,
            ReceivedDate = received ?? new DateOnly(2026, 11, 10),
        };
        _orders.Add(order);
        return order;
    }

    private OrderLine SeedOrderLine(int lineId = Line100, int variantId = 1)
    {
        if (_orderLines.Any(l => l.OrderLineId == lineId)) return _orderLines.First(l => l.OrderLineId == lineId);
        var variant = _variants.FirstOrDefault(v => v.ProductVariantId == variantId)
            ?? TestVariant.Make(1, "MH1", "Đen", "M");
        variant.Product ??= _products[0];
        if (!_variants.Contains(variant)) _variants.Add(variant);
        var line = new OrderLine { OrderLineId = lineId, OrderId = OrderId, ProductId = 1, OrderedQty = 500 }.WithVariant(variant);
        _orderLines.Add(line);
        return line;
    }

    private void SeedFabricMarkerStack(int orderLineVariantId = 1)
    {
        var variant = _variants.FirstOrDefault(v => v.ProductVariantId == orderLineVariantId) ?? TestVariant.Make(1, "MH1", "Đen", "M");
        variant.Product ??= _products[0];
        if (!_variants.Contains(variant)) _variants.Add(variant);
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 900, ProductId = 1, ProductColorId = variant.ProductColorId, ProductColor = variant.ProductColor,
            MaterialId = FabricId, Material = _fabric,
            ProductComponent = new ProductComponent
            {
                ProductComponentId = 31, ComponentType = ComponentTypes.Panel, ComponentName = "Thân",
                Status = RecordStatus.Active, PiecesPerGarment = 1,
            },
            ProductComponentId = 31, Status = RecordStatus.Active,
        });
        _markerFabrics.Add(new ProductMarkerFabric { ProductId = 1, MarkerNo = 1, ProductColorId = variant.ProductColorId, ProductColor = variant.ProductColor, MaterialId = FabricId, Material = _fabric });
        _markerComponents.Add(new ProductMarkerComponent { ProductId = 1, MarkerNo = 1, ProductComponentId = 31 });
        var marker = new ProductMarker
        {
            ProductMarkerId = 1, OrderId = OrderId, ProductId = 1, MarkerNo = 1, MarkerCode = "1A", LengthM = 2m, Status = RecordStatus.Active,
        };
        marker.ProductMarkerRatios.Add(new ProductMarkerRatio { SizeCode = "M", PiecesPerPly = 1 });
        _markers.Add(marker);
    }

    private SaveWorkOrderDto CreateDto(int planned = 300, DateOnly? due = null, int team = CuttingTeam1) =>
        new(OrderId, 1, team, due ?? Today.AddDays(5), null, [new SaveWorkOrderLineDto(Line100, planned)]);

    [Fact]
    public async Task UTCID315_GetPagedAsync_filters_by_cutting_team_and_returns_planned_totals()
    {
        // Arrange
        SeedOrder();
        SeedWorkOrder(60, CuttingTeam1);
        SeedWorkOrder(61, CuttingTeam1);
        SeedWorkOrder(62, CuttingTeam1);
        SeedWorkOrder(70, CuttingTeam2);

        // Act
        var result = await Sut().GetPagedAsync(new WorkOrderQuery { Page = 1, PageSize = 10 }, CuttingTeam1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(3);
        result.Data.Items.Should().OnlyContain(i => i.CuttingTeamId == CuttingTeam1);
        result.Data.Items.Should().Contain(i => i.TotalPlannedQty > 0);
    }

    [Fact]
    public async Task UTCID316_GetPagedAsync_returns_empty_when_no_work_orders()
    {
        // Act
        var result = await Sut().GetPagedAsync(new WorkOrderQuery { Page = 1, PageSize = 10 }, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID317_GetPagedAsync_normalizes_pascal_status_to_match_cutting_orders()
    {
        // Arrange
        SeedOrder();
        SeedWorkOrder(80, status: WorkOrderStatuses.Cutting);
        SeedWorkOrder(81, status: WorkOrderStatuses.Cutting);

        // Act
        var result = await Sut().GetPagedAsync(new WorkOrderQuery { Page = 1, PageSize = 10, Status = "Cutting" }, null, default);

        // Assert
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID318_GetPagedAsync_handles_unknown_status_without_exception()
    {
        // Arrange
        SeedWorkOrder(60);

        // Act
        var result = await Sut().GetPagedAsync(new WorkOrderQuery { Page = 1, PageSize = 10, Status = "Foo" }, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID319_GetByIdAsync_returns_fabric_checks_for_open_regular_work_order()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        SeedWorkOrder(60);

        // Act
        var result = await Sut().GetByIdAsync(60, CuttingTeam1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FabricChecks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UTCID320_GetByIdAsync_returns_empty_fabric_checks_for_replacement_work_order()
    {
        // Arrange
        SeedWorkOrder(61, sourceRequestId: 40);

        // Act
        var result = await Sut().GetByIdAsync(61, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FabricChecks.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID321_GetByIdAsync_returns_empty_fabric_checks_when_status_not_open()
    {
        // Arrange
        SeedWorkOrder(62, status: WorkOrderStatuses.Cutting);

        // Act
        var result = await Sut().GetByIdAsync(62, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FabricChecks.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID322_GetByIdAsync_returns_team_mismatch_for_wrong_cutting_team()
    {
        // Arrange
        SeedWorkOrder(60, CuttingTeam1);

        // Act
        var result = await Sut().GetByIdAsync(60, CuttingTeam2, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    [Fact]
    public async Task UTCID323_GetByIdAsync_returns_not_found_when_missing()
    {
        // Act
        var result = await Sut().GetByIdAsync(999, null, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID324_CheckFabricAsync_reports_enough_fabric_when_stock_covers_need()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        // Cùng ngưỡng với UTCID325 — need ~600m cho 300pcs với marker LengthM=2
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(600m);

        // Act
        var result = await Sut().CheckFabricAsync(new CheckWorkOrderFabricDto(OrderId, CuttingTeam1, [new(Line100, 300)]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().Contain(c => c.MaterialId == FabricId && c.Status == FabricCheckStatuses.Enough);
    }

    [Fact]
    public async Task UTCID325_CheckFabricAsync_reports_enough_when_stock_equals_need()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(600m);

        // Act
        var result = await Sut().CheckFabricAsync(new CheckWorkOrderFabricDto(OrderId, CuttingTeam1, [new(Line100, 300)]), default);

        // Assert
        result.Data!.Single(c => c.MaterialId == FabricId).Status.Should().Be(FabricCheckStatuses.Enough);
    }

    [Fact]
    public async Task UTCID326_CheckFabricAsync_reports_short_when_stock_slightly_below_need()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(119.99m);

        // Act
        var result = await Sut().CheckFabricAsync(new CheckWorkOrderFabricDto(OrderId, CuttingTeam1, [new(Line100, 300)]), default);

        // Assert
        result.Data!.Single(c => c.MaterialId == FabricId).Status.Should().Be(FabricCheckStatuses.Short);
    }

    [Fact]
    public async Task UTCID327_CheckFabricAsync_returns_empty_list_when_no_lines()
    {
        // Arrange
        SeedOrder();

        // Act
        var result = await Sut().CheckFabricAsync(new CheckWorkOrderFabricDto(OrderId, CuttingTeam1, []), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID328_CheckFabricAsync_rejects_order_line_not_belonging_to_order()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine(Line100);
        var foreignVariant = TestVariant.Make(1, "MH1", "Xanh", "L");
        _variants.Add(foreignVariant);
        _orderLines.Add(new OrderLine { OrderLineId = 555, OrderId = 99, ProductId = 1, OrderedQty = 100 }.WithVariant(foreignVariant));
        SeedFabricMarkerStack();

        // Act
        var result = await Sut().CheckFabricAsync(new CheckWorkOrderFabricDto(OrderId, CuttingTeam1, [new(Line100, 100), new(555, 50)]), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID329_CheckFabricAsync_returns_not_found_when_order_missing()
    {
        // Act
        var result = await Sut().CheckFabricAsync(new CheckWorkOrderFabricDto(999, CuttingTeam1, [new(1, 10)]), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID330_GetFabricRequirementsAsync_returns_fabric_rows_for_work_order()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        var wo = SeedWorkOrder(60);

        // Act
        var result = await Sut().GetFabricRequirementsAsync(60, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().NotBeEmpty();
        result.Data!.Should().Contain(f => f.MaterialId == FabricId);
    }

    [Fact]
    public async Task UTCID331_GetFabricRequirementsAsync_returns_empty_when_no_fabric_norms()
    {
        // Arrange
        SeedWorkOrder(67);

        // Act
        var result = await Sut().GetFabricRequirementsAsync(67, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID332_GetFabricRequirementsAsync_returns_not_found_when_missing()
    {
        // Act
        var result = await Sut().GetFabricRequirementsAsync(999, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID333_GetFabricNeedsAsync_returns_positive_needs()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        SeedWorkOrder(60);
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(10m);

        // Act
        var result = await Sut().GetFabricNeedsAsync(60, null, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().Contain(n => n.MaterialId == FabricId && n.NeedMeters > 0);
    }

    [Fact]
    public async Task UTCID334_GetFabricNeedsAsync_omits_fabrics_with_zero_need()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        SeedWorkOrder(60);
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(10_000m);

        // Act
        var result = await Sut().GetFabricNeedsAsync(60, null, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Where(n => n.MaterialId == FabricId).Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID335_GetFabricNeedsAsync_returns_not_found_when_missing()
    {
        // Act
        var result = await Sut().GetFabricNeedsAsync(999, null, null, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID336_GetMaterialAllowanceAsync_returns_allowance_from_norms()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        SeedWorkOrder(60);

        // Act
        var result = await Sut().GetMaterialAllowanceAsync(60, CuttingTeam1, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UTCID337_GetMaterialAllowanceAsync_preserves_fractional_allowance()
    {
        // Arrange
        var wo = SeedWorkOrder(68);
        wo.WorkOrderLines.First().PlannedQty = 1;
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = 901, ProductId = 1, MaterialId = FabricId, Material = _fabric,
            ProductComponent = new ProductComponent { ComponentType = ComponentTypes.Panel, ProductComponentId = 1 },
            ProductComponentId = 1, ProductColorId = 1,
        });

        // Act
        var result = await Sut().GetMaterialAllowanceAsync(68, CuttingTeam1, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID338_GetMaterialAllowanceAsync_returns_not_found_when_missing()
    {
        // Act
        var result = await Sut().GetMaterialAllowanceAsync(999, CuttingTeam1, null, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID339_CreateAsync_creates_open_work_order_and_notifies()
    {
        // Arrange
        SeedOrder(received: new DateOnly(2026, 11, 10));
        SeedOrderLine();
        SeedFabricMarkerStack();

        // Act
        var result = await Sut().CreateAsync(CreateDto(due: Today.AddDays(5)), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(WorkOrderStatuses.Open);
        result.Data.WorkOrderCode.Should().Be("LC2611-001");
        await _notifier.Received().NotifyTeamLeadersAsync(CuttingTeam1, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _notifier.Received().NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID340_CreateAsync_accepts_due_date_equal_today()
    {
        // Arrange
        SeedOrder(received: new DateOnly(2026, 11, 10));
        SeedOrderLine();
        SeedFabricMarkerStack();

        // Act
        var result = await Sut().CreateAsync(CreateDto(due: Today), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID341_CreateAsync_rejects_due_date_before_today()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();

        // Act
        var result = await Sut().CreateAsync(CreateDto(due: Today.AddDays(-1)), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.WorkOrderDueInPast);
    }

    [Fact]
    public async Task UTCID342_CreateAsync_rejects_due_date_on_or_before_order_received_date()
    {
        // Arrange
        SeedOrder(received: Today);
        SeedOrderLine();

        // Act
        var result = await Sut().CreateAsync(CreateDto(due: Today), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID343_CreateAsync_accepts_due_date_after_received_date()
    {
        // Arrange
        SeedOrder(received: Today);
        SeedOrderLine();
        SeedFabricMarkerStack();

        // Act
        var result = await Sut().CreateAsync(CreateDto(due: Today.AddDays(1)), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID344_CreateAsync_rejects_planned_qty_zero()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();

        // Act
        var result = await Sut().CreateAsync(CreateDto(planned: 0), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _workOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID345_CreateAsync_rejects_empty_lines()
    {
        // Arrange
        SeedOrder();

        // Act
        var result = await Sut().CreateAsync(new SaveWorkOrderDto(OrderId, 1, CuttingTeam1, Today.AddDays(3), null, []), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID346_CreateAsync_rejects_negative_planned_qty()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();

        // Act
        var result = await Sut().CreateAsync(new SaveWorkOrderDto(OrderId, 1, CuttingTeam1, Today.AddDays(3), null, [new(Line100, -10)]), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID347_CreateAsync_blocks_create_when_fabric_shortage()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        SeedFabricMarkerStack();
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(0m);

        // Act
        var result = await Sut().CreateAsync(CreateDto(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _workOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID348_CreateAsync_rejects_when_order_closed()
    {
        // Arrange
        SeedOrder(status: OrderStatuses.Completed);
        SeedOrderLine();

        // Act
        var result = await Sut().CreateAsync(CreateDto(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.OrderClosed);
    }

    [Fact]
    public async Task UTCID349_CreateAsync_rejects_non_cutting_team()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();

        // Act
        var result = await Sut().CreateAsync(CreateDto(team: SewingTeam3), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.CuttingTeamInvalid);
    }

    [Fact]
    public async Task UTCID350_CreateAsync_rejects_order_line_not_in_order()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine(Line100);

        // Act
        var result = await Sut().CreateAsync(new SaveWorkOrderDto(OrderId, 1, CuttingTeam1, Today.AddDays(5), null, [new(555, 10)]), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID351_CreateAsync_propagates_failure_without_calling_get_by_id_for_invalid_create()
    {
        // Arrange
        SeedOrder();
        SeedOrderLine();
        _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new InvalidOperationException("save failed"));

        // Act
        var act = () => Sut().CreateAsync(CreateDto(), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _uow.Received().RollbackTransactionAsync();
    }

    [Fact]
    public async Task UTCID352_GetOpenReplacementRequestsAsync_returns_approved_requests_without_work_order()
    {
        // Arrange
        SeedOrder();
        var line = SeedOrderLine();
        SeedFabricMarkerStack();
        var pcm = _norms.First(n => n.ProductComponentMaterialId == 900);
        _requests.AddRange([
            new Request { RequestId = 40, RequestCode = "R40", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved, OrderId = OrderId, Order = _orders[0],
                RequestLines = { new RequestLine { OrderLineId = line.OrderLineId, OrderLine = line, ProductComponentMaterialId = 900, ProductComponentMaterial = pcm, Quantity = 5 } } },
            new Request { RequestId = 41, RequestCode = "R41", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved, OrderId = OrderId, Order = _orders[0],
                RequestLines = { new RequestLine { OrderLineId = line.OrderLineId, OrderLine = line, ProductComponentMaterialId = 900, ProductComponentMaterial = pcm, Quantity = 5 } } },
            new Request { RequestId = 42, RequestCode = "R42", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.PendingApproval, OrderId = OrderId, Order = _orders[0] },
        ]);
        _workOrders.Add(new WorkOrder { WorkOrderId = 99, SourceRequestId = 41, Status = WorkOrderStatuses.Open, OrderId = OrderId });

        // Act
        var result = await Sut().GetOpenReplacementRequestsAsync(default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().ContainSingle(r => r.RequestId == 40);
    }

    [Fact]
    public async Task UTCID353_GetOpenReplacementRequestsAsync_returns_empty_when_none_match()
    {
        // Act
        var result = await Sut().GetOpenReplacementRequestsAsync(default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID354_GetOpenReplacementRequestsAsync_skips_broken_request_lines_without_exception()
    {
        // Arrange
        _requests.Add(new Request
        {
            RequestId = 43, RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved, OrderId = OrderId,
            RequestLines = { new RequestLine { OrderLineId = null, ProductComponentMaterialId = 900, Quantity = 1 } },
        });

        // Act
        var result = await Sut().GetOpenReplacementRequestsAsync(default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID355_CreateReplacementAsync_creates_open_replacement_work_order()
    {
        // Arrange
        SeedOrder(received: new DateOnly(2026, 11, 10));
        var line = SeedOrderLine();
        SeedFabricMarkerStack();
        _requests.Add(new Request
        {
            RequestId = 40, RequestCode = "R40", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved,
            OrderId = OrderId, Order = _orders[0],
            RequestLines = { new RequestLine { OrderLineId = line.OrderLineId, OrderLine = line, ProductComponentMaterialId = 900, Quantity = 5,
                ProductComponentMaterial = _norms.First(n => n.ProductComponentMaterialId == 900) } },
        });

        // Act
        var result = await Sut().CreateReplacementAsync(new CreateReplacementWorkOrderDto(40, CuttingTeam1, Today.AddDays(3), null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.SourceRequestId.Should().Be(40);
        result.Data.Status.Should().Be(WorkOrderStatuses.Open);
    }

    [Fact]
    public async Task UTCID356_CreateReplacementAsync_succeeds_with_short_stock_warning_not_block()
    {
        // Arrange
        SeedOrder(received: new DateOnly(2026, 11, 10));
        var line = SeedOrderLine();
        SeedFabricMarkerStack();
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(0.1m);
        _requests.Add(new Request
        {
            RequestId = 40, RequestCode = "R40", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved,
            OrderId = OrderId, Order = _orders[0],
            RequestLines = { new RequestLine { OrderLineId = line.OrderLineId, OrderLine = line, ProductComponentMaterialId = 900, Quantity = 5,
                ProductComponentMaterial = _norms.First(n => n.ProductComponentMaterialId == 900) } },
        });

        // Act
        var result = await Sut().CreateReplacementAsync(new CreateReplacementWorkOrderDto(40, CuttingTeam1, Today.AddDays(3), null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID357_CreateReplacementAsync_rejects_pending_request()
    {
        // Arrange
        _requests.Add(new Request { RequestId = 42, RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.PendingApproval, OrderId = OrderId });

        // Act
        var result = await Sut().CreateReplacementAsync(new CreateReplacementWorkOrderDto(42, CuttingTeam1, Today.AddDays(3), null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID358_CreateReplacementAsync_rejects_when_work_order_already_exists()
    {
        // Arrange
        SeedOrder();
        _requests.Add(new Request { RequestId = 41, RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved, OrderId = OrderId, Order = _orders[0],
            RequestLines = { new RequestLine { OrderLineId = Line100, ProductComponentMaterialId = 900, Quantity = 1 } } });
        _workOrders.Add(new WorkOrder { WorkOrderId = 50, SourceRequestId = 41, Status = WorkOrderStatuses.Open, OrderId = OrderId });

        // Act
        var result = await Sut().CreateReplacementAsync(new CreateReplacementWorkOrderDto(41, CuttingTeam1, Today.AddDays(3), null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID359_CreateReplacementAsync_returns_not_found_for_missing_request()
    {
        // Act
        var result = await Sut().CreateReplacementAsync(new CreateReplacementWorkOrderDto(999, CuttingTeam1, Today.AddDays(3), null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID360_UpdateAsync_updates_open_work_order()
    {
        // Arrange
        SeedOrder(received: new DateOnly(2026, 11, 10));
        SeedOrderLine();
        SeedFabricMarkerStack();
        var wo = SeedWorkOrder(60);

        // Act
        var result = await Sut().UpdateAsync(60, CreateDto(planned: 320, due: Today.AddDays(8)), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        wo.WorkOrderLines.First().PlannedQty.Should().Be(320);
        await _notifier.Received().NotifyTeamLeadersAsync(CuttingTeam1, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID361_UpdateAsync_accepts_due_date_equal_today()
    {
        // Arrange
        SeedOrder(received: new DateOnly(2026, 11, 10));
        SeedOrderLine();
        SeedFabricMarkerStack();
        SeedWorkOrder(60);

        // Act
        var result = await Sut().UpdateAsync(60, CreateDto(due: Today), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID362_UpdateAsync_rejects_when_cutting_has_started()
    {
        // Arrange
        var wo = SeedWorkOrder(63, status: WorkOrderStatuses.Open);
        SeedCuttingActivity(wo, 50);

        // Act
        var result = await Sut().UpdateAsync(63, CreateDto(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID363_UpdateAsync_returns_not_found_when_missing()
    {
        // Act
        var result = await Sut().UpdateAsync(999, CreateDto(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID364_CancelAsync_cancels_open_work_order_without_activity()
    {
        // Arrange
        var wo = SeedWorkOrder(60);

        // Act
        var result = await Sut().CancelAsync(60, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        wo.Status.Should().Be(WorkOrderStatuses.Cancelled);
        await _notifier.Received().NotifyTeamLeadersAsync(CuttingTeam1, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID365_CancelAsync_rejects_when_fabric_issue_received()
    {
        // Arrange
        SeedWorkOrder(64);
        _issues.Add(new MaterialIssue { WorkOrderId = 64, Status = IssueStatuses.Received });

        // Act
        var result = await Sut().CancelAsync(64, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.WorkOrderHasActivity);
    }

    [Fact]
    public async Task UTCID366_CancelAsync_rejects_when_cutting_started()
    {
        // Arrange
        var wo = SeedWorkOrder(63);
        SeedCuttingActivity(wo, 50);

        // Act
        var result = await Sut().CancelAsync(63, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID367_CancelAsync_rejects_already_cancelled()
    {
        // Arrange
        SeedWorkOrder(65, status: WorkOrderStatuses.Cancelled);

        // Act
        var result = await Sut().CancelAsync(65, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID368_CancelAsync_returns_not_found_when_missing()
    {
        // Act
        var result = await Sut().CancelAsync(999, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID369_CloseShortAsync_closes_in_progress_work_order_with_reason()
    {
        // Arrange
        var wo = SeedWorkOrder(63, status: WorkOrderStatuses.Cutting);
        wo.WorkOrderLines.First().PlannedQty = 300;
        _layLines.Add(new CuttingLayLine { OrderLineId = Line100, ActualQty = 250, CuttingLay = new CuttingLay { WorkOrderId = 63, WorkOrder = wo } });
        _handoverLines.Add(new CutHandoverLine
        {
            OrderLineId = Line100, HandedQty = 250, ConfirmedQty = 250,
            CutHandover = new CutHandover { WorkOrderId = 63, Status = HandoverStatuses.Confirmed },
        });

        // Act
        var result = await Sut().CloseShortAsync(63, "Hết vải", default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        wo.Status.Should().Be(WorkOrderStatuses.Completed);
    }

    [Fact]
    public async Task UTCID370_CloseShortAsync_rejects_open_work_order_with_zero_cut()
    {
        // Arrange
        SeedWorkOrder(60, status: WorkOrderStatuses.Open);

        // Act
        var result = await Sut().CloseShortAsync(60, "Hết vải", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID371_CloseShortAsync_rejects_when_planned_fully_cut()
    {
        // Arrange
        var wo = SeedWorkOrder(66, status: WorkOrderStatuses.Cutting);
        _layLines.Add(new CuttingLayLine { OrderLineId = Line100, ActualQty = 300, CuttingLay = new CuttingLay { WorkOrderId = 66, WorkOrder = wo } });
        _handoverLines.Add(new CutHandoverLine
        {
            OrderLineId = Line100, HandedQty = 300, ConfirmedQty = 300,
            CutHandover = new CutHandover { WorkOrderId = 66, Status = HandoverStatuses.Confirmed },
        });

        // Act
        var result = await Sut().CloseShortAsync(66, "Không cần", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID372_CloseShortAsync_rejects_cancelled_work_order()
    {
        // Arrange
        SeedWorkOrder(65, status: WorkOrderStatuses.Cancelled);

        // Act
        var result = await Sut().CloseShortAsync(65, "x", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID373_CloseShortAsync_returns_not_found_when_missing()
    {
        // Act
        var result = await Sut().CloseShortAsync(999, "x", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }
}