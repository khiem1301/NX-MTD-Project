using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-186: Quản đốc sửa đơn mua trong 24 giờ kể từ lúc tạo, khi chưa nhận hàng và chưa thanh toán.</summary>
public class PurchaseOrderEditTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 26);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<PurchaseOrder> _pos = new();
    private readonly List<Supplier> _suppliers = new();
    private readonly List<MaterialReceiptLine> _receiptLines = new();
    private readonly List<DeliveryScheduleDate> _schedule = new();
    private readonly PurchaseOrder _po;
    private static readonly Material Mat = new() { MaterialId = 1, MaterialCode = "V1", MaterialName = "Vải", Unit = "m", Color = "Đen" };

    public PurchaseOrderEditTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        Setup(_pos); Setup(_suppliers); Setup(_receiptLines); Setup(_schedule); Setup(new List<VwPurchaseOrderDelivery>());
        _uow.SaveChangesAsync().Returns(1);
        _suppliers.Add(new Supplier { SupplierId = 3, SupplierName = "NCC A", Status = RecordStatus.Active });
        _suppliers.Add(new Supplier { SupplierId = 4, SupplierName = "NCC cũ", Status = RecordStatus.Discontinued });
        _po = new PurchaseOrder
        {
            PurchaseOrderId = 5, PoCode = "DM-1", GoodsStatus = PurchaseGoodsStatuses.Ordered, PaymentStatus = PurchaseOrderStatuses.Unpaid,
            SupplierId = 3, CreatedAt = Now.AddHours(-2),
            PurchaseOrderLines = new List<PurchaseOrderLine>
            {
                new() { PurchaseOrderLineId = 11, OrderedQty = 100, UnitPrice = 10_000, MaterialId = 1, Material = Mat },
                new() { PurchaseOrderLineId = 12, OrderedQty = 50, UnitPrice = 10_000, MaterialId = 1, Material = Mat },
            },
        };
        _pos.Add(_po);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private PurchaseOrderService Sut() => new(_uow, Substitute.For<IDocumentCodeGenerator>(), _clock,
        Substitute.For<IMaterialService>(), Substitute.For<INotificationService>(), Substitute.For<IAuditLogService>());

    private static UpdatePurchaseOrderDto Update(string reason = "Nhập nhầm giá", decimal price = 11_000) =>
        new(Today, 1, "ghi chú",
            new[] { new UpdatePurchaseOrderLineDto(11, 120, price), new UpdatePurchaseOrderLineDto(12, 50, price) }, reason);

    [Fact]
    public async Task Update_within_24_hours_changes_lines_and_keeps_the_supplier()
    {
        var result = await Sut().UpdateAsync(5, Update(), default);

        result.IsSuccess.Should().BeTrue();
        _po.PurchaseOrderLines.Select(l => (l.OrderedQty, l.UnitPrice)).Should().Equal((120m, 11_000m), (50m, 11_000m));
        _po.SupplierId.Should().Be(3);                 // nhà cung cấp không đổi được sau khi tạo (DEV-ALL-189)
        _po.CreatedAt.Should().Be(Now.AddHours(-2));   // 24 giờ tính từ lúc tạo, sửa không gia hạn
    }

    [Fact]
    public async Task Update_without_reason_is_rejected()
    {
        var result = await Sut().UpdateAsync(5, Update(reason: "  "), default);

        result.IsSuccess.Should().BeFalse();
        _po.PurchaseOrderLines.First().OrderedQty.Should().Be(100);
    }

    [Fact]
    public async Task Update_after_24_hours_is_locked()
    {
        _po.CreatedAt = Now.AddHours(-25);

        (await Sut().UpdateAsync(5, Update(), default)).ErrorCode.Should().Be(ErrorCodes.PaymentEditExpired);
    }

    [Fact]
    public async Task Update_is_locked_once_paid_or_a_receipt_exists()
    {
        _po.PaymentStatus = PurchasePaymentStatuses.PartiallyPaid;
        (await Sut().UpdateAsync(5, Update(), default)).Message.Should().Be(Messages.PurchaseOrderEditLocked);

        _po.PaymentStatus = PurchaseOrderStatuses.Unpaid;
        _receiptLines.Add(new MaterialReceiptLine { PurchaseOrderLineId = 11 });
        (await Sut().UpdateAsync(5, Update(), default)).Message.Should().Be(Messages.PurchaseOrderEditLocked);
    }

    [Fact]
    public async Task Update_rejects_mismatched_lines()
    {
        var oneLine = Update() with { Lines = new[] { new UpdatePurchaseOrderLineDto(11, 1, 1) } };
        (await Sut().UpdateAsync(5, oneLine, default)).ErrorCode.Should().Be(ErrorCodes.PurchaseOrderLinesMismatch);
    }

    [Theory]
    [InlineData(PurchaseGoodsStatuses.Ordered, PurchaseOrderStatuses.Unpaid, -23, false, true)]
    [InlineData(PurchaseGoodsStatuses.Ordered, PurchaseOrderStatuses.Unpaid, -25, false, false)]
    [InlineData(PurchaseGoodsStatuses.PartiallyReceived, PurchaseOrderStatuses.Unpaid, -1, false, false)]
    [InlineData(PurchaseGoodsStatuses.Ordered, PurchaseOrderStatuses.Unpaid, -1, true, false)]
    public void IsEditable_follows_status_receipts_and_24_hour_window(string goods, string pay, int hours, bool receipt, bool expected) =>
        PurchaseOrderService.IsEditable(goods, pay, Now.AddHours(hours), receipt, Now).Should().Be(expected);
}
