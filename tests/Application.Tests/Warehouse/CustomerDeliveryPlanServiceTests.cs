using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-95: lịch khách giao NVL khai một lần cho cả đơn hàng.</summary>
public class CustomerDeliveryPlanServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<CustomerOrder> _orders = new();
    private readonly List<OrderMaterial> _materials = new();
    private readonly List<MaterialReceipt> _receipts = new();

    public CustomerDeliveryPlanServiceTests()
    {
        _clock.Today.Returns(Today);
        Setup(_orders); Setup(_materials); Setup(_receipts);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private CustomerDeliveryPlanService Sut() => new(_uow, new DeliveryCountGuard(_uow), _notifier, _audit, _clock);

    private CustomerOrder Order(decimal customerQty = 100)
    {
        var order = new CustomerOrder { OrderId = 7, OrderCode = "DH-7", Status = OrderStatuses.InProduction, DueDate = Today.AddDays(30) };
        _orders.Add(order);
        _materials.Add(new OrderMaterial { OrderMaterialId = 1, OrderId = 7, MaterialId = 1, CustomerQty = customerQty });
        return order;
    }

    [Fact]
    public async Task SaveAsync_TwoDeliveriesWithRange_StoresPlanAndNotifiesWarehouseKeeper()
    {
        var order = Order();

        var result = await Sut().SaveAsync(7, new SaveOrderCustomerDeliveryDto(2, Today.AddDays(3), Today.AddDays(10)), default);

        result.IsSuccess.Should().BeTrue();
        order.CustomerDeliveryCount.Should().Be(2);
        order.CustomerDeliveryDate.Should().Be(Today.AddDays(3));
        order.CustomerDeliveryDateTo.Should().Be(Today.AddDays(10));
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_OneDelivery_DropsDateTo()
    {
        var order = Order();

        await Sut().SaveAsync(7, new SaveOrderCustomerDeliveryDto(1, Today.AddDays(3), Today.AddDays(10)), default);

        order.CustomerDeliveryDateTo.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_CountBelowDeliveredReceipts_ReturnsFailure()
    {
        var order = Order();
        _receipts.Add(new MaterialReceipt { MaterialReceiptId = 1, OrderId = 7, Status = ReceiptStatuses.Confirmed });
        _receipts.Add(new MaterialReceipt { MaterialReceiptId = 2, OrderId = 7, Status = ReceiptStatuses.Confirmed });

        var result = await Sut().SaveAsync(7, new SaveOrderCustomerDeliveryDto(1, Today.AddDays(3), null), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("2 lần");
        order.CustomerDeliveryCount.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_DateAfterDueDate_ReturnsFailure()
    {
        Order();

        var result = await Sut().SaveAsync(7, new SaveOrderCustomerDeliveryDto(1, Today.AddDays(31), null), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_IncreaseCountKeepingPastDates_ReturnsFailure()
    {
        var order = Order();
        order.CustomerDeliveryCount = 2;
        order.CustomerDeliveryDate = Today.AddDays(-20);
        order.CustomerDeliveryDateTo = Today.AddDays(-5);   // lịch cũ đã qua

        var result = await Sut().SaveAsync(7, new SaveOrderCustomerDeliveryDto(3, Today.AddDays(-20), Today.AddDays(-5)), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.CustomerDeliveryIncreaseNeedsFutureDate);
        order.CustomerDeliveryCount.Should().Be(2);
    }

    [Fact]
    public async Task SaveAsync_IncreaseCountWithNewDateTo_StoresPlan()
    {
        var order = Order();
        order.CustomerDeliveryCount = 2;
        order.CustomerDeliveryDate = Today.AddDays(-20);
        order.CustomerDeliveryDateTo = Today.AddDays(-5);

        var result = await Sut().SaveAsync(7, new SaveOrderCustomerDeliveryDto(3, Today.AddDays(-20), Today.AddDays(7)), default);

        result.IsSuccess.Should().BeTrue();   // ngày từ cũ giữ nguyên được, ngày đến mới từ hôm nay trở đi
        order.CustomerDeliveryCount.Should().Be(3);
        order.CustomerDeliveryDateTo.Should().Be(Today.AddDays(7));
    }

    [Fact]
    public async Task GetAsync_ReturnsPlanAndDeliveredCount()
    {
        var order = Order();
        order.CustomerDeliveryCount = 3;
        _receipts.Add(new MaterialReceipt { MaterialReceiptId = 1, OrderId = 7, Status = ReceiptStatuses.Confirmed });
        _receipts.Add(new MaterialReceipt { MaterialReceiptId = 2, OrderId = 7, Status = ReceiptStatuses.Confirmed, ShortageRequestId = 9 });

        var result = (await Sut().GetAsync(7, default)).Data!;

        result.DeliveryCount.Should().Be(3);
        result.ReceivedCount.Should().Be(1);   // phiếu bổ sung không tính là một lần giao
        result.HasCustomerMaterials.Should().BeTrue();
    }
}
