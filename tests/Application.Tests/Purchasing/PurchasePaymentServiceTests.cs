using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-117: thanh toán đơn mua nhiều lần, sửa trong 24 giờ kèm lý do, giá trị thực tế do Kế toán nhập.</summary>
public class PurchasePaymentServiceTests
{
    private static readonly byte[] RowVer = { 7 };
    private static readonly DateTime Now = new(2026, 10, 27, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<PurchaseOrder> _poRepo = Substitute.For<IGenericRepository<PurchaseOrder>>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<PurchaseOrder> _pos = new();

    public PurchasePaymentServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _uow.Repository<PurchaseOrder>().Returns(_poRepo);
        _poRepo.Query().Returns(_ => _pos.AsTestAsyncQueryable());
    }

    private PurchasePaymentService Sut() => new(_uow, _audit, _clock);

    /// <summary>Đơn 1.000.000 (100 × 10.000) với các lần thanh toán cho trước: (số tiền, tuổi tính bằng giờ).</summary>
    private PurchaseOrder Po(string goods = PurchaseGoodsStatuses.PartiallyReceived, params (decimal Amount, int AgeHours)[] payments)
    {
        var po = new PurchaseOrder
        {
            PurchaseOrderId = 5, PoCode = "DM-1", GoodsStatus = goods, PaymentStatus = PurchaseOrderStatuses.Unpaid, RowVer = RowVer,
            PurchaseOrderLines = new List<PurchaseOrderLine> { new() { PurchaseOrderLineId = 11, OrderedQty = 100, UnitPrice = 10_000 } },
        };
        var id = 1;
        foreach (var (amount, age) in payments)
            po.PurchasePayments.Add(new PurchasePayment
            {
                PurchasePaymentId = id++, PurchaseOrderId = 5, Amount = amount, PaymentDate = Today, PaymentMethod = PaymentMethods.Cash,
                CreatedAt = Now.AddHours(-age),
            });
        PurchaseAmounts.Sync(po);
        _pos.Add(po);
        return po;
    }

    private static UpdatePurchasePaymentDto Update(decimal amount = 500_000, string reason = "Nhập nhầm số tiền") =>
        new(Today, amount, "BankTransfer", null, reason, RowVer);

    [Fact]
    public void Sync_PartialThenRest_GoesPartiallyPaidThenPaid()
    {
        var po = Po(PurchaseGoodsStatuses.Ordered, (400_000, 1));
        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.PartiallyPaid);
        po.PaidAmount.Should().Be(400_000);

        po.PurchasePayments.Add(new PurchasePayment { PurchasePaymentId = 2, Amount = 600_000, PaymentMethod = PaymentMethods.Cash, CreatedAt = Now });
        PurchaseAmounts.Sync(po);

        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.Paid);
        po.PaidAmount.Should().Be(1_000_000);
    }

    [Fact]
    public async Task UpdatePayment_Within24Hours_UpdatesAndRecomputesStatus()
    {
        var po = Po(PurchaseGoodsStatuses.Ordered, (1_000_000, 23));
        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.Paid);

        var result = await Sut().UpdatePaymentAsync(5, 1, Update(600_000), default);

        result.IsSuccess.Should().BeTrue();
        var payment = po.PurchasePayments.Single();
        payment.Amount.Should().Be(600_000);
        payment.EditReason.Should().Be("Nhập nhầm số tiền");
        payment.UpdatedAt.Should().Be(Now);
        po.PaidAmount.Should().Be(600_000);
        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.PartiallyPaid);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Reason == "Nhập nhầm số tiền"));
    }

    [Fact]
    public async Task UpdatePayment_After24Hours_IsRefused()
    {
        var po = Po(PurchaseGoodsStatuses.Ordered, (500_000, 25));

        var result = await Sut().UpdatePaymentAsync(5, 1, Update(), default);

        result.ErrorCode.Should().Be(ErrorCodes.PaymentEditExpired);
        po.PurchasePayments.Single().EditReason.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePayment_WithoutReason_IsRefused()
    {
        Po(PurchaseGoodsStatuses.Ordered, (500_000, 1));

        var result = await Sut().UpdatePaymentAsync(5, 1, Update(reason: "  "), default);

        result.ErrorCode.Should().Be(ErrorCodes.PaymentInvalid);
        result.Message.Should().Be(Messages.PaymentEditReasonRequired);
    }

    [Fact]
    public async Task UpdatePayment_TotalOverActualValue_IsRefused()
    {
        Po(PurchaseGoodsStatuses.Ordered, (400_000, 2), (300_000, 1));

        var result = await Sut().UpdatePaymentAsync(5, 2, Update(700_000), default);   // 400.000 + 700.000 > 1.000.000

        result.ErrorCode.Should().Be(ErrorCodes.PaymentInvalid);
        result.Message.Should().Be(Messages.PaymentAmountExceedsTotal);
    }

    [Fact]
    public async Task UpdatePayment_UnknownPayment_ReturnsNotFound()
    {
        Po(PurchaseGoodsStatuses.Ordered, (500_000, 1));

        (await Sut().UpdatePaymentAsync(5, 99, Update(), default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task SetActualAmount_LowerThanOrder_MarksPaidWhenAlreadyCovered()
    {
        var po = Po(PurchaseGoodsStatuses.PartiallyReceived, (600_000, 1));
        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.PartiallyPaid);

        var result = await Sut().SetActualAmountAsync(5, new SetActualAmountDto(600_000, "NCC giảm giá do giao thiếu", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        po.ActualAmountOverride.Should().Be(600_000);
        po.ActualAmountReason.Should().Be("NCC giảm giá do giao thiếu");
        po.PaymentStatus.Should().Be(PurchasePaymentStatuses.Paid);
    }

    [Fact]
    public async Task SetActualAmount_BelowPaid_WithoutReason_OrWrongStatus_IsRefused()
    {
        Po(PurchaseGoodsStatuses.PartiallyReceived, (600_000, 1));
        var belowPaid = await Sut().SetActualAmountAsync(5, new SetActualAmountDto(500_000, "x", RowVer), default);
        var noReason = await Sut().SetActualAmountAsync(5, new SetActualAmountDto(700_000, null, RowVer), default);

        belowPaid.Message.Should().Be(Messages.PaymentActualBelowPaid);
        noReason.Message.Should().Be(Messages.PaymentActualReasonRequired);

        _pos.Clear();
        Po(PurchaseGoodsStatuses.Ordered);
        var wrongStatus = await Sut().SetActualAmountAsync(5, new SetActualAmountDto(700_000, "x", RowVer), default);
        wrongStatus.ErrorCode.Should().Be(ErrorCodes.PurchaseOrderInvalidStatus);
    }

    [Fact]
    public async Task SetActualAmount_Null_ClearsOverride()
    {
        var po = Po(PurchaseGoodsStatuses.ClosedShort);
        po.ActualAmountOverride = 300_000;
        po.ActualAmountReason = "x";

        var result = await Sut().SetActualAmountAsync(5, new SetActualAmountDto(null, null, RowVer), default);

        result.IsSuccess.Should().BeTrue();
        po.ActualAmountOverride.Should().BeNull();
        po.ActualAmountReason.Should().BeNull();
    }
}
