using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Thêm, sửa, xóa NVL của đơn đều bị chặn khi đơn đã đóng (trước đây chỉ chặn thêm mới).</summary>
public class OrderMaterialClosedOrderTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<Material> _materials = [];
    private readonly List<MaterialReceiptLine> _receiptLines = [];
    private readonly List<VwOrderMaterialDelivery> _deliveries = [];

    public OrderMaterialClosedOrderTests()
    {
        Setup(_orders); Setup(_orderMaterials); Setup(_materials); Setup(_receiptLines); Setup(_deliveries);
        _materials.Add(new Material { MaterialId = 1, MaterialCode = "V1", MaterialName = "Vải", Unit = "m", Status = MaterialStatuses.Active, MaterialType = "FABRIC" });
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        repo.When(r => r.Remove(Arg.Any<T>())).Do(c => backing.Remove(c.Arg<T>()));
        _uow.Repository<T>().Returns(repo);
    }

    private OrderMaterialService Sut() => new(_uow, _notifier, Substitute.For<IClock>());

    private OrderMaterial Line(string orderStatus, decimal required = 100)
    {
        var order = new CustomerOrder { OrderId = 1, OrderCode = "DH1", Status = orderStatus, DueDate = new DateOnly(2026, 12, 1) };
        _orders.Add(order);
        var om = new OrderMaterial { OrderMaterialId = 7, OrderId = 1, Order = order, MaterialId = 1, Material = _materials[0], RequiredQty = required };
        _orderMaterials.Add(om);
        return om;
    }

    private static UpdateOrderMaterialDto Edit(decimal required) => new(1, required, 0, null, null);

    public static TheoryData<string> ClosedStatuses => new() { OrderStatuses.Cancelled, OrderStatuses.Stopped, OrderStatuses.Delivered, OrderStatuses.Completed };

    public static TheoryData<string> OpenStatuses => new()
    {
        OrderStatuses.New, OrderStatuses.InProduction, OrderStatuses.InDelivery, OrderStatuses.PartiallyDelivered,
    };

    [Theory, MemberData(nameof(ClosedStatuses))]
    public async Task Update_IsRefused_WhenTheOrderIsClosed(string status)
    {
        var om = Line(status);

        var result = await Sut().UpdateAsync(7, Edit(999), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
        result.Message.Should().Be(WarehouseMessages.OrderMaterialOrderClosed);
        om.RequiredQty.Should().Be(100);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Theory, MemberData(nameof(ClosedStatuses))]
    public async Task Delete_IsRefused_WhenTheOrderIsClosed(string status)
    {
        Line(status);

        var result = await Sut().DeleteAsync(7, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
        _orderMaterials.Should().HaveCount(1);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Theory, MemberData(nameof(ClosedStatuses))]
    public async Task Create_IsRefused_WhenTheOrderIsClosed_WithTheNewMessage(string status)
    {
        Line(status);

        var result = await Sut().CreateAsync(new CreateOrderMaterialDto(1, 1, 5, 0, null, null), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
        result.Message.Should().Be(WarehouseMessages.OrderMaterialOrderClosed);
    }

    [Theory, MemberData(nameof(OpenStatuses))]
    public async Task Update_AndDelete_StillWork_WhenTheOrderIsRunning(string status)
    {
        var om = Line(status);

        (await Sut().UpdateAsync(7, Edit(250), default)).IsSuccess.Should().BeTrue();
        om.RequiredQty.Should().Be(250);

        (await Sut().DeleteAsync(7, default)).IsSuccess.Should().BeTrue();
        _orderMaterials.Should().BeEmpty();
    }
}
