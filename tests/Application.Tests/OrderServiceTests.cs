using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Orders;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class ProductServiceTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    private ProductService Create() => new(_products, _audit, _uow, _clock, _storage, Options.Create(new AttachmentOptions()));

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

    [Fact]
    public async Task UploadSampleImage_Png_ReturnsStorageKey()
    {
        _storage.SaveAsync(Arg.Any<byte[]>(), "PRODUCT", ".png", Arg.Any<CancellationToken>()).Returns("PRODUCT/2026/10/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png");

        var result = await Create().UploadSampleImageAsync(new FileUpload(Png, "a.png"), default);

        result.IsSuccess.Should().BeTrue();
        SampleImageKey.IsValid(result.Data).Should().BeTrue();
    }

    [Fact]
    public async Task UploadSampleImage_NotAnImage_Fails()
    {
        var result = await Create().UploadSampleImageAsync(new FileUpload(new byte[] { 1, 2, 3, 4 }, "a.png"), default);

        result.IsSuccess.Should().BeFalse();
        await _storage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task OpenSampleImage_InvalidKey_NotFound()
    {
        var result = await Create().OpenSampleImageAsync("../secret.png", default);

        result.IsSuccess.Should().BeFalse();
        await _storage.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default);
    }

    private static SaveProductDto Dto(string code = "AO-01") => new(code, "Áo thun", null, null);

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        _products.CodeExistsAsync("AO-01", null, default).ReturnsForAnyArgs(true);

        var result = await Create().CreateAsync(Dto(), default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductCodeDuplicate);
        await _products.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task Create_NormalizesCodeUpper()
    {
        Product? saved = null;
        _products.When(x => x.AddAsync(Arg.Any<Product>())).Do(c => saved = c.Arg<Product>());

        var result = await Create().CreateAsync(Dto(" ao-01 "), default);

        result.IsSuccess.Should().BeTrue();
        saved!.ProductCode.Should().Be("AO-01");
        saved.Status.Should().Be(RecordStatus.Active);
    }

    [Fact]
    public async Task Update_ChangeCodeWhenUsed_Fails()
    {
        _products.GetByIdAsync(1).Returns(new Product { ProductId = 1, ProductCode = "AO-01", ProductName = "x", Status = RecordStatus.Active });
        _products.IsUsedAsync(1, default).ReturnsForAnyArgs(true);

        var result = await Create().UpdateAsync(1, Dto("AO-02"), default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductCodeLocked);
    }

    [Fact]
    public async Task Update_RenameWhenUsed_SucceedsAndAudits()
    {
        var product = new Product { ProductId = 1, ProductCode = "AO-01", ProductName = "x", Status = RecordStatus.Active };
        _products.GetByIdAsync(1).Returns(product);
        _products.IsUsedAsync(1, default).ReturnsForAnyArgs(true);

        var result = await Create().UpdateAsync(1, Dto("AO-01") with { ProductName = "Tên mới" }, default);

        result.IsSuccess.Should().BeTrue();
        product.ProductName.Should().Be("Tên mới");
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task Discontinue_AlreadyDiscontinued_Fails()
    {
        _products.GetByIdAsync(1).Returns(new Product { ProductId = 1, Status = RecordStatus.Discontinued });

        var result = await Create().DiscontinueAsync(1, default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductInvalidStatus);
    }
}

public class CustomerOrderServiceTests
{
    private static readonly byte[] RowVer = { 1 };

    private readonly ICustomerOrderRepository _orders = Substitute.For<ICustomerOrderRepository>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IGenericRepository<OrderLine> _lines = Substitute.For<IGenericRepository<OrderLine>>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();

    public CustomerOrderServiceTests()
    {
        _clock.Now.Returns(new DateTime(2026, 10, 23, 8, 0, 0));
        _orders.CustomerExistsAsync(default, default).ReturnsForAnyArgs(true);
        _products.FindInactiveCodesAsync(default!, default).ReturnsForAnyArgs(Array.Empty<string>());
    }

    private CustomerOrderService Create() => new(_orders, _products, _lines, _uow, _clock, _audit, _notifier);

    private static OrderLineInput L(int? id, string color, string size, int qty = 10, int product = 1, decimal price = 12500) =>
        new(id, product, color, size, qty, price);

    private static UpdateOrderDto Update(params OrderLineInput[] lines) =>
        new("DH2610-001", 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), null, lines, RowVer);

    private static CustomerOrder Existing(string status, params OrderLine[] lines)
    {
        var order = new CustomerOrder
        {
            OrderId = 5, OrderCode = "DH2610-001", CustomerId = 1, ReceivedDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 11, 1), Status = status,
        };
        foreach (var l in lines) order.OrderLines.Add(l);
        return order;
    }

    private static OrderLine Line(int id, string color, string size, int qty = 10) =>
        new() { OrderLineId = id, OrderId = 5, ProductId = 1, Color = color, SizeCode = size, OrderedQty = qty, CmtUnitPrice = 12500 };

    [Fact]
    public async Task Create_InactiveProduct_FailsWithCode()
    {
        _products.FindInactiveCodesAsync(default!, default).ReturnsForAnyArgs(new[] { "AO-01" });
        var dto = new CreateOrderDto("DH2610-001", 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), null,
            new[] { L(null, "Đỏ", "M") });

        var result = await Create().CreateAsync(dto, default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductNotActive);
        result.Message.Should().Contain("AO-01");
    }

    [Fact]
    public async Task Create_UsesTransactionAuditsAndNotifiesManager()
    {
        var dto = new CreateOrderDto("dh2610-001", 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), null,
            new[] { L(null, "Đỏ", "m") });
        CustomerOrder? saved = null;
        _orders.When(x => x.AddAsync(Arg.Any<CustomerOrder>())).Do(c => saved = c.Arg<CustomerOrder>());

        var result = await Create().CreateAsync(dto, default);

        result.IsSuccess.Should().BeTrue();
        saved!.OrderCode.Should().Be("DH2610-001");
        saved.Status.Should().Be(OrderStatuses.New);
        saved.OrderLines.Single().SizeCode.Should().Be("M");
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(2).SaveChangesAsync();
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Insert));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitTransactionAsync();
    }

    [Fact]
    public async Task SuggestCode_IncrementsMonthly()
    {
        _orders.GetMaxCodeAsync("DH2610-", default).ReturnsForAnyArgs("DH2610-009");
        (await Create().SuggestCodeAsync(default)).Data.Should().Be("DH2610-010");

        _orders.GetMaxCodeAsync("DH2610-", default).ReturnsForAnyArgs((string?)null);
        (await Create().SuggestCodeAsync(default)).Data.Should().Be("DH2610-001");
    }

    [Fact]
    public async Task Update_New_RemovesMissingLine()
    {
        var keep = Line(1, "Đỏ", "M");
        var drop = Line(2, "Đỏ", "L");
        var order = Existing(OrderStatuses.New, keep, drop);
        _orders.GetWithLinesAsync(5, default).ReturnsForAnyArgs(order);

        var result = await Create().UpdateAsync(5, Update(L(1, "Đỏ", "M", 20)), default);

        result.IsSuccess.Should().BeTrue();
        order.OrderLines.Should().ContainSingle().Which.OrderedQty.Should().Be(20);
        _lines.Received(1).Remove(drop);
    }

    [Fact]
    public async Task Update_New_ReAddSameKey_ReusesLineInsteadOfDeleting()
    {
        var old = Line(1, "Đỏ", "M");
        var order = Existing(OrderStatuses.New, old);
        _orders.GetWithLinesAsync(5, default).ReturnsForAnyArgs(order);

        var result = await Create().UpdateAsync(5, Update(L(null, "đỏ", "m", 99)), default);

        result.IsSuccess.Should().BeTrue();
        order.OrderLines.Should().ContainSingle().Which.Should().BeSameAs(old);
        old.OrderedQty.Should().Be(99);
        _lines.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task Update_InProduction_RemoveLine_Fails()
    {
        var order = Existing(OrderStatuses.InProduction, Line(1, "Đỏ", "M"), Line(2, "Đỏ", "L"));
        _orders.GetWithLinesAsync(5, default).ReturnsForAnyArgs(order);

        var result = await Create().UpdateAsync(5, Update(L(1, "Đỏ", "M")), default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderLineRemoveNotAllowed);
    }

    [Fact]
    public async Task Update_InProduction_ChangeColor_Fails()
    {
        var order = Existing(OrderStatuses.InProduction, Line(1, "Đỏ", "M"));
        _orders.GetWithLinesAsync(5, default).ReturnsForAnyArgs(order);

        var result = await Create().UpdateAsync(5, Update(L(1, "Xanh", "M")), default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderLineKeyLocked);
    }

    [Fact]
    public async Task Update_InProduction_ChangeQtyAndAddLine_Succeeds()
    {
        var order = Existing(OrderStatuses.InProduction, Line(1, "Đỏ", "M"));
        _orders.GetWithLinesAsync(5, default).ReturnsForAnyArgs(order);

        var result = await Create().UpdateAsync(5, Update(L(1, "Đỏ", "M", 50), L(null, "Đỏ", "XL")), default);

        result.IsSuccess.Should().BeTrue();
        order.OrderLines.Should().HaveCount(2);
        order.OrderLines.First().OrderedQty.Should().Be(50);
    }

    [Fact]
    public async Task Update_InProduction_ChangeCustomer_Fails()
    {
        var order = Existing(OrderStatuses.InProduction, Line(1, "Đỏ", "M"));
        _orders.GetWithLinesAsync(5, default).ReturnsForAnyArgs(order);

        var result = await Create().UpdateAsync(5, Update(L(1, "Đỏ", "M")) with { CustomerId = 2 }, default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderNotEditable);
        result.Message.Should().Be(Messages.OrderHeaderLocked);
    }

    [Fact]
    public async Task Update_Delivered_Fails()
    {
        _orders.GetWithLinesAsync(5, default).ReturnsForAnyArgs(Existing(OrderStatuses.Delivered, Line(1, "Đỏ", "M")));

        var result = await Create().UpdateAsync(5, Update(L(1, "Đỏ", "M")), default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderNotEditable);
    }

    [Fact]
    public async Task Cancel_InProduction_Fails()
    {
        _orders.GetByIdAsync(5).Returns(Existing(OrderStatuses.InProduction));

        var result = await Create().CancelAsync(5, new CancelOrderDto(null, RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderInvalidStatus);
    }

    [Theory]
    [InlineData(OrderMaterialStatuses.Partial)]
    [InlineData(OrderMaterialStatuses.Complete)]
    public async Task StartProduction_NewWithMaterials_MovesToInProduction(string materialStatus)
    {
        var order = Existing(OrderStatuses.New);
        order.MaterialStatus = materialStatus;
        _orders.GetByIdAsync(5).Returns(order);

        var result = await Create().StartProductionAsync(5, new StartOrderProductionDto(RowVer), default);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatuses.InProduction);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(CustomerOrder) && e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task StartProduction_WhileAwaitingMaterials_IsRejected()
    {
        var order = Existing(OrderStatuses.New);
        order.MaterialStatus = OrderMaterialStatuses.Awaiting;
        _orders.GetByIdAsync(5).Returns(order);

        var result = await Create().StartProductionAsync(5, new StartOrderProductionDto(RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderMaterialsAwaiting);
        order.Status.Should().Be(OrderStatuses.New);
    }

    [Theory]
    [InlineData(OrderStatuses.InProduction)]
    [InlineData(OrderStatuses.Stopped)]
    [InlineData(OrderStatuses.Cancelled)]
    public async Task StartProduction_WhenNotNew_IsRejected(string status)
    {
        var order = Existing(status);
        order.MaterialStatus = OrderMaterialStatuses.Complete;
        _orders.GetByIdAsync(5).Returns(order);

        var result = await Create().StartProductionAsync(5, new StartOrderProductionDto(RowVer), default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderInvalidStatus);
    }

    [Fact]
    public async Task Stop_New_Fails_AndInProduction_SetsReason()
    {
        _orders.GetByIdAsync(5).Returns(Existing(OrderStatuses.New));
        (await Create().StopAsync(5, new StopOrderDto("hết hàng", RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.OrderInvalidStatus);

        var running = Existing(OrderStatuses.InProduction);
        _orders.GetByIdAsync(5).Returns(running);
        var result = await Create().StopAsync(5, new StopOrderDto(" khách hủy ", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        running.Status.Should().Be(OrderStatuses.Stopped);
        running.StopReason.Should().Be("khách hủy");
    }
}

public class OrderValidatorTests
{
    private static CreateOrderDto Dto(DateOnly due, params OrderLineInput[] lines) =>
        new("DH2610-001", 1, new DateOnly(2026, 10, 1), due, null, lines);

    [Fact]
    public void DuplicateLineKey_IgnoringCase_Fails()
    {
        var dto = Dto(new DateOnly(2026, 11, 1), new OrderLineInput(null, 1, "Đỏ", "M", 1, 0), new OrderLineInput(null, 1, "đỏ", "m", 2, 0));

        new CreateOrderDtoValidator().Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DueBeforeReceived_Fails()
    {
        var dto = Dto(new DateOnly(2026, 9, 1), new OrderLineInput(null, 1, "Đỏ", "M", 1, 0));

        new CreateOrderDtoValidator().Validate(dto).Errors.Should().Contain(e => e.ErrorMessage.Contains("Ngày giao"));
    }

    [Fact]
    public void NoLines_Fails() =>
        new CreateOrderDtoValidator().Validate(Dto(new DateOnly(2026, 11, 1))).IsValid.Should().BeFalse();

    [Fact]
    public void Stop_EmptyReason_HasMsg09() =>
        new StopOrderDtoValidator().Validate(new StopOrderDto("", new byte[] { 1 })).Errors
            .Should().Contain(e => e.ErrorMessage == Messages.ReasonRequired);

    [Fact]
    public void LookupDtos_HaveNoPriceProperties()
    {
        foreach (var type in new[] { typeof(OrderLookupDto), typeof(OrderLineLookupDto), typeof(ProductLookupDto) })
            type.GetProperties().Select(p => p.Name).Should().NotContain(new[] { "CmtUnitPrice", "TotalAmount" });
    }
}
