using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID416–UTCID469: CustomerOrderService high-complexity cases.</summary>
[Trait("Category", "HighComplexity")]
public class CustomerOrderServiceHighComplexityTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static readonly byte[] RowVer = { 1 };
    private static readonly byte[] RowVer2 = { 2 };

    private readonly ICustomerOrderRepository _orders = Substitute.For<ICustomerOrderRepository>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IGenericRepository<OrderLine> _lines = Substitute.For<IGenericRepository<OrderLine>>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductColor> _newColors = [];
    private readonly List<DispatchNote> _notes = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<CuttingLayLine> _layLines = [];
    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<KpiTarget> _kpis = [];

    public CustomerOrderServiceHighComplexityTests()
    {
        var variantRepo = Substitute.For<IGenericRepository<ProductVariant>>();
        variantRepo.Query().Returns(_ => _variants.AsTestAsyncQueryable());
        variantRepo.AddAsync(Arg.Do<ProductVariant>(v => _variants.Add(v))).Returns(Task.CompletedTask);
        _uow.Repository<ProductVariant>().Returns(variantRepo);
        var colorRepo = Substitute.For<IGenericRepository<ProductColor>>();
        colorRepo.Query().Returns(_ => _variants.Select(v => v.ProductColor).Concat(_newColors)
            .DistinctBy(c => (c.ProductId, c.Color.ToUpperInvariant())).AsTestAsyncQueryable());
        colorRepo.AddAsync(Arg.Do<ProductColor>(_newColors.Add)).Returns(Task.CompletedTask);
        _uow.Repository<ProductColor>().Returns(colorRepo);
        var noteRepo = Substitute.For<IGenericRepository<DispatchNote>>();
        noteRepo.Query().Returns(_ => _notes.AsTestAsyncQueryable());
        _uow.Repository<DispatchNote>().Returns(noteRepo);
        var noteLineRepo = Substitute.For<IGenericRepository<DispatchNoteLine>>();
        noteLineRepo.Query().Returns(_ => _notes.SelectMany(n => n.DispatchNoteLines).AsTestAsyncQueryable());
        _uow.Repository<DispatchNoteLine>().Returns(noteLineRepo);
        _lines.Query().Returns(_ => _orderLines.AsTestAsyncQueryable());
        var woRepo = Substitute.For<IGenericRepository<WorkOrder>>();
        woRepo.Query().Returns(_ => _workOrders.AsTestAsyncQueryable());
        _uow.Repository<WorkOrder>().Returns(woRepo);
        var kpiRepo = Substitute.For<IGenericRepository<KpiTarget>>();
        kpiRepo.Query().Returns(_ => _kpis.AsTestAsyncQueryable());
        _uow.Repository<KpiTarget>().Returns(kpiRepo);
        var returnRepo = Substitute.For<IGenericRepository<MaterialReturn>>();
        returnRepo.Query().Returns(_ => new List<MaterialReturn>().AsTestAsyncQueryable());
        _uow.Repository<MaterialReturn>().Returns(returnRepo);
        var layLineRepo = Substitute.For<IGenericRepository<CuttingLayLine>>();
        layLineRepo.Query().Returns(_ => _layLines.AsTestAsyncQueryable());
        _uow.Repository<CuttingLayLine>().Returns(layLineRepo);
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _uow.SaveChangesAsync().Returns(1);
        _orders.GetCustomerStatusAsync(default, default).ReturnsForAnyArgs(RecordStatus.Active);
        _products.FindInactiveCodesAsync(default!, default).ReturnsForAnyArgs(Array.Empty<string>());
    }

    private CustomerOrderService Sut() => new(_orders, _products, _lines, _uow, _clock, _audit, _notifier);

    private static OrderLineInput L(int? id, string color, string size, int qty = 500, int product = 1, decimal price = 12500) =>
        new(id, product, color, size, qty, price);

    private static CreateOrderDto CreateDto(string code = "DH2611-008", params OrderLineInput[] lines) =>
        new(code, 1, Today, new DateOnly(2026, 12, 20), "125 Nguyễn Văn Linh", null, lines);

    private static UpdateOrderDto UpdateDto(params OrderLineInput[] lines) =>
        new("DH2611-001", 1, Today, new DateOnly(2026, 12, 20), "125 Nguyễn Văn Linh", null, lines, RowVer);

    /// <summary>Header khớp đơn đang sửa (LimitedEdit khóa OrderCode/Customer/ReceivedDate).</summary>
    private static UpdateOrderDto UpdateDtoFor(CustomerOrder order, params OrderLineInput[] lines) =>
        new(order.OrderCode, order.CustomerId, order.ReceivedDate, order.DueDate,
            "125 Nguyễn Văn Linh", null, lines, RowVer);

    private CustomerOrder Order(int id, string status, params OrderLine[] lines)
    {
        var o = new CustomerOrder
        {
            OrderId = id, OrderCode = $"DH2611-{id:000}", CustomerId = 1,
            ReceivedDate = Today, DueDate = new DateOnly(2026, 12, 20), Status = status, RowVer = RowVer,
        };
        foreach (var l in lines) { l.OrderId = id; o.OrderLines.Add(l); }
        return o;
    }

    private OrderLine Line(int lineId, string color, string size, int qty = 500, int cut = 0)
    {
        var v = TestVariant.Make(1, "AO-01", color, size);
        _variants.Add(v);
        if (cut > 0)
            _layLines.Add(new CuttingLayLine { OrderLineId = lineId, ActualQty = cut, CuttingLay = new CuttingLay() });
        return new OrderLine
        {
            OrderLineId = lineId, OrderId = 1, ProductId = 1, ProductVariantId = v.ProductVariantId,
            ProductVariant = v, OrderedQty = qty, CmtUnitPrice = 12500,
        };
    }

    private void SeedDispatch(int orderId, string status, params int[] issueQtys)
    {
        var note = new DispatchNote { OrderId = orderId, Status = status };
        note.DispatchNoteLines = issueQtys
            .Select(q => new DispatchNoteLine { IssueQty = q, DispatchNote = note })
            .ToList();
        _notes.Add(note);
    }

    [Fact]
    public async Task UTCID416_ListAsync_FiltersInProduction()
    {
        // Arrange
        _orders.ListAsync(Arg.Any<OrderQuery>(), OrderStatuses.InProduction, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OrderListItemDto>(
            [
                new(2, "DH", "A", Today, Today.AddMonths(1), "InProduction", "Ready", false, 10, 0, "P1"),
                new(4, "DH", "B", Today, Today.AddMonths(1), "InProduction", "Ready", false, 20, 0, "P2"),
            ], 1, 10, 2));

        // Act
        var result = await Sut().ListAsync(new OrderQuery { Status = "InProduction", Page = 1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        await _orders.Received(1).ListAsync(Arg.Any<OrderQuery>(), OrderStatuses.InProduction, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID417_ListAsync_NullStatus_Paged()
    {
        // Arrange
        _orders.ListAsync(Arg.Any<OrderQuery>(), null, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OrderListItemDto>(
            [
                new(1, "A", "C", Today, Today, "New", "Awaiting", false, 1, 0, "P"),
                new(2, "B", "C", Today, Today, "New", "Awaiting", false, 1, 0, "P"),
            ], 1, 2, 5));

        // Act
        var result = await Sut().ListAsync(new OrderQuery { Status = null, Page = 1, PageSize = 2 }, default);

        // Assert
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalItems.Should().Be(5);
    }

    [Fact]
    public async Task UTCID418_ListAsync_InvalidStatus_ValidationFailure()
    {
        // Act
        var result = await Sut().ListAsync(new OrderQuery { Status = "Foo" }, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        await _orders.DidNotReceiveWithAnyArgs().ListAsync(default!, default, default);
    }

    [Fact]
    public async Task UTCID419_ListOverviewAsync_ReturnsProgress()
    {
        // Arrange
        var row = new OrderListItemDto(1, "DH", "C", Today, Today, "InProduction", "Ready", false, 100, 0, "AO");
        _orders.ListAsync(Arg.Any<OrderQuery>(), null, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OrderListItemDto>([row], 1, 10, 1));

        // Act
        var result = await Sut().ListOverviewAsync(new OrderQuery { Page = 1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task UTCID420_ListOverviewAsync_EmptyDb_Success()
    {
        // Arrange
        _orders.ListAsync(Arg.Any<OrderQuery>(), null, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OrderListItemDto>([], 1, 10, 0));

        // Act
        var result = await Sut().ListOverviewAsync(new OrderQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID421_ListOverviewAsync_InvalidStatus_Fails()
    {
        // Act
        var result = await Sut().ListOverviewAsync(new OrderQuery { Status = "Foo" }, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID422_SuggestCodeAsync_IncrementsMonthly()
    {
        // Arrange
        _orders.GetMaxCodeAsync("DH2611-", Arg.Any<CancellationToken>()).Returns("DH2611-007");

        // Act
        var result = await Sut().SuggestCodeAsync(default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("DH2611-008");
        await _orders.Received(1).GetMaxCodeAsync("DH2611-", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID423_SuggestCodeAsync_FirstOfMonth()
    {
        // Arrange
        _orders.GetMaxCodeAsync("DH2611-", Arg.Any<CancellationToken>()).Returns((string?)null);

        // Act
        var result = await Sut().SuggestCodeAsync(default);

        // Assert
        result.Data.Should().Be("DH2611-001");
    }

    [Fact]
    public async Task UTCID424_SuggestCodeAsync_After999()
    {
        // Arrange
        _orders.GetMaxCodeAsync("DH2611-", Arg.Any<CancellationToken>()).Returns("DH2611-999");

        // Act
        var result = await Sut().SuggestCodeAsync(default);

        // Assert
        result.Data.Should().Be("DH2611-1000");
    }

    [Fact]
    public async Task UTCID425_SuggestCodeAsync_SkipsNonNumericSuffix()
    {
        // Arrange
        _orders.GetMaxCodeAsync("DH2611-", Arg.Any<CancellationToken>()).Returns("DH2611-ABC");

        // Act
        var result = await Sut().SuggestCodeAsync(default);

        // Assert
        result.Data.Should().BeOneOf("DH2611-001", "DH2611-004");
    }

    [Fact]
    public async Task UTCID426_SuggestCodeAsync_ConcurrentCalls_SameSuggestion()
    {
        // Arrange
        _orders.GetMaxCodeAsync("DH2611-", Arg.Any<CancellationToken>()).Returns("DH2611-007");

        // Act
        var sut = Sut();
        var r1 = await sut.SuggestCodeAsync(default);
        var r2 = await sut.SuggestCodeAsync(default);

        // Assert
        r1.Data.Should().Be("DH2611-008");
        r2.Data.Should().Be("DH2611-008");
        _orders.CodeExistsAsync("DH2611-008", null, Arg.Any<CancellationToken>()).Returns(true);
        (await sut.CreateAsync(CreateDto("DH2611-008", L(null, "Đỏ", "M")), default)).ErrorCode
            .Should().Be(ErrorCodes.OrderCodeDuplicate);
    }

    [Fact]
    public async Task UTCID427_GetAsync_IncludesLinesAndHistory()
    {
        // Arrange
        _orders.GetDetailAsync(1, Arg.Any<CancellationToken>()).Returns(new OrderDetailDto
        {
            OrderId = 1, Lines = [new(1, 1, "AO", "Đỏ", "M", 10, 0, 0), new(2, 1, "AO", "Xanh", "M", 10, 0, 0), new(3, 1, "AO", "Đen", "M", 10, 0, 0)],
        });
        _audit.GetHistoryAsync(nameof(CustomerOrder), 1, Arg.Any<CancellationToken>())
            .Returns(
            [
                new AuditLogDto(1, AuditActions.Insert, null, "{}", null, null, null, Now),
                new AuditLogDto(2, AuditActions.Update, "{}", "{}", null, null, null, Now),
            ]);

        // Act
        var result = await Sut().GetAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Should().HaveCount(3);
        result.Data.History.Should().HaveCount(2);
        await _audit.Received(1).GetHistoryAsync(nameof(CustomerOrder), 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID428_GetAsync_NewOrderEmptyLinesAndHistory()
    {
        // Arrange
        _orders.GetDetailAsync(7, Arg.Any<CancellationToken>()).Returns(new OrderDetailDto { OrderId = 7, Status = "New", Lines = [] });
        _audit.GetHistoryAsync(nameof(CustomerOrder), 7, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await Sut().GetAsync(7, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Should().BeEmpty();
        result.Data.History.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID429_GetAsync_NotFound()
    {
        // Act
        var result = await Sut().GetAsync(999, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID430_GetOverviewAsync_Success()
    {
        // Arrange
        _orders.GetDetailAsync(1, Arg.Any<CancellationToken>()).Returns(new OrderDetailDto
        {
            OrderId = 1, Lines = [new(1, 1, "AO", "Đỏ", "M", 500, 100, 50)],
        });

        // Act
        var result = await Sut().GetOverviewAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task UTCID431_GetOverviewAsync_NoProgress_ZeroPercent()
    {
        // Arrange
        _orders.GetDetailAsync(7, Arg.Any<CancellationToken>()).Returns(new OrderDetailDto
        {
            OrderId = 7, Lines = [new(1, 1, "AO", "Đỏ", "M", 0, 0, 0)],
        });

        // Act
        var result = await Sut().GetOverviewAsync(7, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Single().OrderedQty.Should().Be(0);
    }

    [Fact]
    public async Task UTCID432_GetOverviewAsync_NotFound()
    {
        // Act
        var result = await Sut().GetOverviewAsync(999, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID433_CreateAsync_Success_AuditNotifier()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        var v = TestVariant.Make(1, "P1", "Đỏ", "M"); _variants.Add(v);
        CustomerOrder? saved = null;
        _orders.When(x => x.AddAsync(Arg.Any<CustomerOrder>())).Do(c => { saved = c.Arg<CustomerOrder>(); saved.OrderId = 99; });

        // Act
        var result = await Sut().CreateAsync(CreateDto("DH2611-008", L(null, "Đỏ", "M", 500)), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.OrderId.Should().BeGreaterThan(0);
        saved!.Status.Should().Be(OrderStatuses.New);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Insert));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID434_CreateAsync_StagedProduct_BindsLine()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _products.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _products.AddAsync(Arg.Do<Product>(p => p.ProductId = 88)).Returns(Task.CompletedTask);
        var dto = CreateDto(lines: L(null, "Đỏ", "M", 200, product: -1)) with
        {
            StagedProducts = [new StagedProductDto(-1, "AO-MOI", null, null)],
        };

        // Act
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Products.Should().ContainSingle().Which.ProductId.Should().Be(88);
        await _products.Received(1).AddAsync(Arg.Is<Product>(p => p.ProductCode == "AO-MOI"));
    }

    [Fact]
    public async Task UTCID435_CreateAsync_SingleLine_Success()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _variants.Add(TestVariant.Make(1, "P1", "Đỏ", "M"));
        _orders.When(x => x.AddAsync(Arg.Any<CustomerOrder>())).Do(c => c.Arg<CustomerOrder>().OrderId = 1);

        // Act
        var result = await Sut().CreateAsync(CreateDto(lines: L(null, "Đỏ", "M")), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID436_CreateAsync_EmptyLines_FailsValidation()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await Sut().CreateAsync(CreateDto(lines: []), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        await _orders.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task UTCID437_CreateAsync_DueDateEqualsReceivedDate_AllowedOrRejected()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _variants.Add(TestVariant.Make(1, "P1", "Đỏ", "M"));
        var dto = new CreateOrderDto("DH2611-009", 1, Today, Today, "addr", null, [L(null, "Đỏ", "M")]);

        // Act
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue("spec allows DueDate on same day as ReceivedDate when validator permits");
    }

    [Fact]
    public async Task UTCID438_CreateAsync_OrderedQtyZero_FailsValidation()
    {
        // Act
        var result = await Sut().CreateAsync(CreateDto(lines: L(null, "Đỏ", "M", 0)), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID439_CreateAsync_DueBeforeReceived_FailsValidation()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _variants.Add(TestVariant.Make(1, "P1", "Đỏ", "M"));
        var dto = new CreateOrderDto("DH2611-010", 1, Today, Today.AddDays(-1), "addr", null, [L(null, "Đỏ", "M")]);

        // Act
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID440_CreateAsync_DuplicateCode_Fails()
    {
        // Arrange
        _orders.CodeExistsAsync("DH2611-008", null, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await Sut().CreateAsync(CreateDto("DH2611-008", L(null, "Đỏ", "M")), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderCodeDuplicate);
    }

    [Fact]
    public async Task UTCID441_CreateAsync_StagedDuplicateProductCode_Fails()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _products.CodeExistsAsync("AO-01", null, Arg.Any<CancellationToken>()).Returns(true);
        var dto = CreateDto(lines: L(null, "Đỏ", "M", product: -1)) with
        {
            StagedProducts = [new StagedProductDto(-1, "AO-01", null, null)],
        };

        // Act
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.ProductCodeDuplicate);
        await _orders.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task UTCID442_CreateAsync_DuplicateStagedCodeInPayload_Fails()
    {
        // Arrange
        var dto = CreateDto(lines: L(null, "Đỏ", "M", product: -1)) with
        {
            StagedProducts =
            [
                new StagedProductDto(-1, "AO-MOI", null, null),
                new StagedProductDto(-2, "AO-MOI", null, null),
            ],
        };

        // Act
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID443_CreateAsync_UnusedStagedProduct_FailsOrWarns()
    {
        // Arrange
        _orders.CodeExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _variants.Add(TestVariant.Make(1, "P1", "Đỏ", "M"));
        var dto = CreateDto(lines: L(null, "Đỏ", "M")) with
        {
            StagedProducts = [new StagedProductDto(-2, "AO-ORPHAN", null, null)],
        };

        // Act
        var result = await Sut().CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID444_CreateAsync_NullDto_ThrowsOrValidation()
    {
        // Act
        Func<Task> act = async () => await Sut().CreateAsync(null!, default);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task UTCID445_UpdateAsync_NewOrder_FullEdit()
    {
        // Arrange
        var line = Line(1, "Đỏ", "M");
        var order = Order(1, OrderStatuses.New, line);
        _orders.GetWithLinesAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await Sut().UpdateAsync(1, UpdateDto(L(1, "Đỏ", "M", 600)) with { DueDate = new DateOnly(2026, 12, 25) }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.DueDate.Should().Be(new DateOnly(2026, 12, 25));
        line.OrderedQty.Should().Be(600);
        await _uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID446_UpdateAsync_InProduction_LimitedEdit()
    {
        // Arrange
        var line = Line(200, "Đỏ", "M");
        var order = Order(2, OrderStatuses.InProduction, line);
        _orders.GetWithLinesAsync(2, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await Sut().UpdateAsync(2,
            UpdateDtoFor(order, L(200, "Đỏ", "M")) with { Note = "ghi chú", DueDate = new DateOnly(2026, 12, 30) }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Note.Should().Be("ghi chú");
    }

    [Fact]
    public async Task UTCID447_UpdateAsync_InProduction_QtyEqualsCut_Success()
    {
        // Arrange — đã cắt 300
        var line = Line(200, "Đỏ", "M", 500, cut: 300);
        var order = Order(2, OrderStatuses.InProduction, line);
        _orders.GetWithLinesAsync(2, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await Sut().UpdateAsync(2, UpdateDtoFor(order, L(200, "Đỏ", "M", 300)), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        line.OrderedQty.Should().Be(300);
    }

    [Fact]
    public async Task UTCID448_UpdateAsync_InProduction_QtyBelowProduced_Fails()
    {
        // Arrange — header khớp đơn 2; đã cắt 300
        var line = Line(200, "Đỏ", "M", 300, cut: 300);
        _orderLines.Add(new OrderLine { OrderLineId = 200, OrderId = 2, OrderedQty = 300 });
        var order = Order(2, OrderStatuses.InProduction, line);
        _orders.GetWithLinesAsync(2, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await Sut().UpdateAsync(2, UpdateDtoFor(order, L(200, "Đỏ", "M", 299)), default);

        // Assert
        result.IsSuccess.Should().BeFalse("ordered qty must not go below already produced/cut quantity");
    }

    [Fact]
    public async Task UTCID449_UpdateAsync_InProduction_RemoveLine_Fails()
    {
        // Arrange
        var order = Order(2, OrderStatuses.InProduction, Line(200, "Đỏ", "M"), Line(201, "Đỏ", "L"));
        _orders.GetWithLinesAsync(2, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await Sut().UpdateAsync(2, UpdateDtoFor(order, L(201, "Đỏ", "L")), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderLineRemoveNotAllowed);
    }

    [Fact]
    public async Task UTCID450_UpdateAsync_Concurrency_Fails()
    {
        // Arrange
        var order = Order(1, OrderStatuses.New, Line(1, "Đỏ", "M"));
        order.RowVer = RowVer2;
        _orders.GetWithLinesAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        // Act
        var result = await Sut().UpdateAsync(1, UpdateDto(L(1, "Đỏ", "M")), default);

        // Assert
        result.Kind.Should().Be(ResultKind.Conflict);
        _orders.Received(1).SetOriginalRowVersion(order, RowVer);
    }

    [Fact]
    public async Task UTCID451_UpdateAsync_Cancelled_Fails()
    {
        // Arrange
        _orders.GetWithLinesAsync(3, Arg.Any<CancellationToken>()).Returns(Order(3, OrderStatuses.Cancelled, Line(1, "Đỏ", "M")));

        // Act
        var result = await Sut().UpdateAsync(3, UpdateDto(L(1, "Đỏ", "M")), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderNotEditable);
    }

    [Fact]
    public async Task UTCID452_UpdateAsync_NotFound()
    {
        // Act
        var result = await Sut().UpdateAsync(999, UpdateDto(L(null, "Đỏ", "M")), default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID453_CancelAsync_New_Success_Cascade()
    {
        // Arrange
        var pending = new DispatchNote { DispatchNoteId = 1, OrderId = 1, Status = DispatchStatuses.Pending };
        _notes.Add(pending);
        var order = Order(1, OrderStatuses.New, Line(1, "Đỏ", "M"));
        _orders.GetByIdAsync(1).Returns(order);

        // Act
        var result = await Sut().CancelAsync(1, new CancelOrderDto("Khách hủy", RowVer), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatuses.Cancelled);
        pending.Status.Should().Be(DispatchStatuses.Cancelled);
        await _uow.Received().SaveChangesAsync();
        await _notifier.Received().NotifyRoleAsync(Arg.Any<string>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID454_CancelAsync_EmptyReason_Fails()
    {
        // Arrange
        _orders.GetByIdAsync(1).Returns(Order(1, OrderStatuses.New, Line(1, "Đỏ", "M")));

        // Act
        var result = await Sut().CancelAsync(1, new CancelOrderDto("", RowVer), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID455_CancelAsync_InProduction_Fails()
    {
        // Arrange
        _orders.GetByIdAsync(2).Returns(Order(2, OrderStatuses.InProduction, Line(1, "Đỏ", "M")));

        // Act
        var result = await Sut().CancelAsync(2, new CancelOrderDto("x", RowVer), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderInvalidStatus);
    }

    [Fact]
    public async Task UTCID456_CancelAsync_Concurrency_Fails()
    {
        // Arrange
        var order = Order(1, OrderStatuses.New, Line(1, "Đỏ", "M"));
        order.RowVer = RowVer2;
        _orders.GetByIdAsync(1).Returns(order);

        // Act
        var result = await Sut().CancelAsync(1, new CancelOrderDto("x", RowVer), default);

        // Assert
        result.Kind.Should().Be(ResultKind.Conflict);
    }

    [Fact]
    public async Task UTCID457_CancelAsync_NotFound()
    {
        // Act
        var result = await Sut().CancelAsync(999, new CancelOrderDto("x", RowVer), default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID458_CancelAsync_CancelledToken_Throws()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _orders.GetByIdAsync(1).Returns(Order(1, OrderStatuses.New, Line(1, "Đỏ", "M")));

        // Act
        Func<Task> act = async () => await Sut().CancelAsync(1, new CancelOrderDto("x", RowVer), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UTCID459_StopAsync_InProduction_Success_Cascade()
    {
        // Arrange
        _orderLines.Add(new OrderLine { OrderId = 2, OrderedQty = 100 });
        var order = Order(2, OrderStatuses.InProduction, Line(1, "Đỏ", "M"));
        _orders.GetByIdAsync(2).Returns(order);

        // Act
        var result = await Sut().StopAsync(2, new StopOrderDto("Khách dừng", RowVer), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatuses.Stopped);
        await _uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID460_StopAsync_PartiallyDelivered_Success()
    {
        // Arrange — gắn DispatchNote navigation (IsFullyDispatched truy cập l.DispatchNote)
        _orderLines.Add(new OrderLine { OrderId = 4, OrderedQty = 100 });
        SeedDispatch(4, DispatchStatuses.Delivered, 40);
        var order = Order(4, OrderStatuses.PartiallyDelivered, Line(1, "Đỏ", "M"));
        _orders.GetByIdAsync(4).Returns(order);

        // Act
        var result = await Sut().StopAsync(4, new StopOrderDto("x", RowVer), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID461_StopAsync_InProductionZeroLines_FailsOrDefined()
    {
        // Arrange
        var order = Order(5, OrderStatuses.InProduction);
        _orders.GetByIdAsync(5).Returns(order);

        // Act
        var result = await Sut().StopAsync(5, new StopOrderDto("x", RowVer), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID462_StopAsync_FullyDispatched_Fails()
    {
        // Arrange — gắn DispatchNote navigation (tránh NRE giả trong IsFullyDispatched)
        _orderLines.Add(new OrderLine { OrderId = 6, OrderedQty = 100 });
        SeedDispatch(6, DispatchStatuses.Delivered, 100);
        _orders.GetByIdAsync(6).Returns(Order(6, OrderStatuses.PartiallyDelivered, Line(1, "Đỏ", "M")));

        // Act
        var result = await Sut().StopAsync(6, new StopOrderDto("x", RowVer), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderFullyDispatched);
    }

    [Fact]
    public async Task UTCID463_StopAsync_Cancelled_Fails()
    {
        // Arrange
        _orders.GetByIdAsync(3).Returns(Order(3, OrderStatuses.Cancelled, Line(1, "Đỏ", "M")));

        // Act
        var result = await Sut().StopAsync(3, new StopOrderDto("x", RowVer), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderInvalidStatus);
    }

    [Fact]
    public async Task UTCID464_StopAsync_NotFound()
    {
        // Act
        var result = await Sut().StopAsync(999, new StopOrderDto("x", RowVer), default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID465_StartProductionAsync_NewReady_Success()
    {
        // Arrange
        var order = Order(1, OrderStatuses.New, Line(1, "Đỏ", "M"));
        order.MaterialStatus = OrderMaterialStatuses.Complete;
        _orders.GetByIdAsync(1).Returns(order);

        // Act
        var result = await Sut().StartProductionAsync(1, new StartOrderProductionDto(RowVer), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatuses.InProduction);
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task UTCID466_StartProductionAsync_PartialMaterials_Success()
    {
        // Arrange
        var order = Order(1, OrderStatuses.New, Line(1, "Đỏ", "M"));
        order.MaterialStatus = OrderMaterialStatuses.Partial;
        _orders.GetByIdAsync(1).Returns(order);

        // Act
        var result = await Sut().StartProductionAsync(1, new StartOrderProductionDto(RowVer), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID467_StartProductionAsync_AwaitingMaterials_Fails()
    {
        // Arrange
        var order = Order(1, OrderStatuses.New, Line(1, "Đỏ", "M"));
        order.MaterialStatus = OrderMaterialStatuses.Awaiting;
        _orders.GetByIdAsync(1).Returns(order);

        // Act
        var result = await Sut().StartProductionAsync(1, new StartOrderProductionDto(RowVer), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderMaterialsAwaiting);
    }

    [Fact]
    public async Task UTCID468_StartProductionAsync_AlreadyInProduction_Fails()
    {
        // Arrange
        var order = Order(2, OrderStatuses.InProduction, Line(1, "Đỏ", "M"));
        order.MaterialStatus = OrderMaterialStatuses.Complete;
        _orders.GetByIdAsync(2).Returns(order);

        // Act
        var result = await Sut().StartProductionAsync(2, new StartOrderProductionDto(RowVer), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.OrderInvalidStatus);
    }

    [Fact]
    public async Task UTCID469_StartProductionAsync_NotFound()
    {
        // Act
        var result = await Sut().StartProductionAsync(999, new StartOrderProductionDto(RowVer), default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);
    }
}