using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// DEV-ALL-95: thêm / tăng NVL khách cấp khi đơn đã nhận đủ số lần khách giao thì bị chặn (NVL đó không còn lượt giao, cũng không
/// phải hàng thiếu); Quản đốc phải tăng số lần giao trước.
/// </summary>
public class OrderMaterialCustomerDeliveryTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<Material> _materials = [];
    private readonly List<MaterialReceipt> _receipts = [];
    private readonly CustomerOrder _order;

    public OrderMaterialCustomerDeliveryTests()
    {
        Setup(_orders); Setup(_orderMaterials); Setup(_materials); Setup(_receipts);
        Setup(new List<MaterialReceiptLine>()); Setup(new List<VwOrderMaterialDelivery>());
        _materials.Add(new Material { MaterialId = 1, MaterialCode = "V1", MaterialName = "Vải", Unit = "m", Status = MaterialStatuses.Active });
        _order = new CustomerOrder { OrderId = 1, OrderCode = "DH1", Status = OrderStatuses.InProduction, CustomerDeliveryCount = 1 };
        _orders.Add(_order);
        _orderMaterials.Add(new OrderMaterial
        {
            OrderMaterialId = 7, OrderId = 1, Order = _order, MaterialId = 1, Material = _materials[0], RequiredQty = 100, CustomerQty = 50,
        });
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private OrderMaterialService Sut() => new(_uow, _notifier, new DeliveryCountGuard(_uow));

    private void Delivered(int count)
    {
        for (var i = 1; i <= count; i++)
            _receipts.Add(new MaterialReceipt { MaterialReceiptId = i, OrderId = 1, Status = ReceiptStatuses.Confirmed });
    }

    [Fact]
    public async Task UpdateAsync_IncreaseCustomerQtyWhenDeliveriesFull_ReturnsCustomerDeliveryFull()
    {
        Delivered(1);

        var result = await Sut().UpdateAsync(7, new UpdateOrderMaterialDto(1, 100, 60, null), default);

        result.ErrorCode.Should().Be(ErrorCodes.CustomerDeliveryFull);
        result.Message.Should().Contain("1 lần");
    }

    [Fact]
    public async Task UpdateAsync_SameCustomerQtyWhenDeliveriesFull_Succeeds()
    {
        Delivered(1);

        var result = await Sut().UpdateAsync(7, new UpdateOrderMaterialDto(1, 120, 50, "sửa số cần"), default);

        result.IsSuccess.Should().BeTrue();   // không thêm hàng khách cấp nên không cần lượt giao mới
    }

    [Fact]
    public async Task UpdateAsync_IncreaseCustomerQtyWithDeliveryLeft_Succeeds()
    {
        _order.CustomerDeliveryCount = 2;
        Delivered(1);

        var result = await Sut().UpdateAsync(7, new UpdateOrderMaterialDto(1, 100, 60, null), default);

        result.IsSuccess.Should().BeTrue();
    }
}
