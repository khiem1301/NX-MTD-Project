using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-137: giá trị chốt đơn hàng khách (có thể âm) và thanh toán nhiều lần hai chiều.</summary>
public class OrderPaymentServiceTests
{
    private static readonly byte[] RowVer = { 7 };
    private static readonly DateTime Now = new(2026, 11, 18, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 18);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<CustomerOrder> _repo = Substitute.For<IGenericRepository<CustomerOrder>>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<CustomerOrder> _orders = new();

    public OrderPaymentServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _uow.Repository<CustomerOrder>().Returns(_repo);
        _repo.Query().Returns(_ => _orders.AsTestAsyncQueryable());
        _audit.GetHistoryAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AuditLogDto>());
    }

    private OrderPaymentService Sut() => new(_uow, _audit, _clock);

    /// <summary>Đơn với giá trị chốt cho trước và các lần thanh toán (số tiền có dấu, tuổi tính bằng giờ).</summary>
    private CustomerOrder Order(decimal? finalAmount, params (decimal Amount, int AgeHours)[] payments)
    {
        var order = new CustomerOrder
        {
            OrderId = 3, OrderCode = "DH-3", Status = "IN_PRODUCTION", RowVer = RowVer, FinalAmount = finalAmount,
            FinalAmountReason = finalAmount is null ? null : "Chốt",
            Customer = new Customer { CustomerName = "Khách A" },
        };
        var id = 1;
        foreach (var (amount, age) in payments)
            order.OrderPayments.Add(new OrderPayment
            {
                OrderPaymentId = id++, OrderId = 3, Amount = amount, PaymentDate = Today, PaymentMethod = PaymentMethods.Cash,
                CreatedAt = Now.AddHours(-age),
            });
        _orders.Add(order);
        return order;
    }

    private static AddOrderPaymentDto Add(decimal amount) => new(Today, amount, "BankTransfer", "Đợt trả", RowVer);

    private static UpdateOrderPaymentDto Update(decimal amount, string reason = "Nhập nhầm") =>
        new(Today, amount, "Cash", null, reason, RowVer);

    [Theory]
    [InlineData(1_000, 0, true)]
    [InlineData(1_000, 1_000, true)]
    [InlineData(1_000, 1_001, false)]
    [InlineData(1_000, -1, false)]
    [InlineData(-500, -500, true)]
    [InlineData(-500, -501, false)]
    [InlineData(-500, 1, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    public void InRange_PaidMustStayBetweenZeroAndFinalAmount(int finalAmount, int paid, bool expected) =>
        OrderPaymentService.InRange(paid, finalAmount).Should().Be(expected);

    [Fact]
    public void StateOf_FollowsFinalAmountAndPaid()
    {
        OrderPaymentService.StateOf(null, 0).Should().Be(OrderPaymentStates.NotSet);
        OrderPaymentService.StateOf(1_000, 0).Should().Be(OrderPaymentStates.Unpaid);
        OrderPaymentService.StateOf(1_000, 400).Should().Be(OrderPaymentStates.Partial);
        OrderPaymentService.StateOf(1_000, 1_000).Should().Be(OrderPaymentStates.Settled);
        OrderPaymentService.StateOf(-500, -200).Should().Be(OrderPaymentStates.Partial);
        OrderPaymentService.StateOf(-500, -500).Should().Be(OrderPaymentStates.Settled);
    }

    [Fact]
    public async Task SetFinalAmount_Negative_IsAllowedWithReason()
    {
        var order = Order(null);

        var result = await Sut().SetFinalAmountAsync(3, new SetFinalAmountDto(-2_000_000, "Đền hợp đồng do hủy đơn", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        order.FinalAmount.Should().Be(-2_000_000);
        order.FinalAmountReason.Should().Be("Đền hợp đồng do hủy đơn");
        order.UpdatedAt.Should().Be(Now);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == "OrderPayment" && e.Reason == "Đền hợp đồng do hủy đơn"));
    }

    [Fact]
    public async Task SetFinalAmount_WithoutReason_OrBelowPaid_IsRefused()
    {
        Order(1_000_000, (600_000, 1));

        var noReason = await Sut().SetFinalAmountAsync(3, new SetFinalAmountDto(900_000, " ", RowVer), default);
        var belowPaid = await Sut().SetFinalAmountAsync(3, new SetFinalAmountDto(500_000, "Giảm giá", RowVer), default);
        var signFlip = await Sut().SetFinalAmountAsync(3, new SetFinalAmountDto(-100, "Đền", RowVer), default);

        noReason.Message.Should().Be(Messages.FinalAmountReasonRequired);
        belowPaid.Message.Should().Be(Messages.FinalAmountBelowPaid);
        signFlip.Message.Should().Be(Messages.FinalAmountBelowPaid);
    }

    [Fact]
    public async Task AddPayment_BeforeFinalAmountIsSet_IsRefused()
    {
        Order(null);

        var result = await Sut().AddPaymentAsync(3, Add(100_000), default);

        result.ErrorCode.Should().Be(ErrorCodes.PaymentInvalid);
        result.Message.Should().Be(Messages.OrderPaymentNeedFinalAmount);
    }

    [Fact]
    public async Task AddPayment_CustomerPaysInInstalments_UntilSettled()
    {
        var order = Order(1_000_000, (400_000, 5));

        var ok = await Sut().AddPaymentAsync(3, Add(600_000), default);
        var over = await Sut().AddPaymentAsync(3, Add(1), default);

        ok.IsSuccess.Should().BeTrue();
        order.OrderPayments.Should().HaveCount(2);
        over.Message.Should().Be(Messages.OrderPaymentOutOfRange);
        order.OrderPayments.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddPayment_WeCompensateTheCustomerInInstalments_NegativeAmounts()
    {
        var order = Order(-1_000_000, (-300_000, 5));

        var ok = await Sut().AddPaymentAsync(3, Add(-700_000), default);
        var overCompensate = await Sut().AddPaymentAsync(3, Add(-1), default);
        var flipsToCustomerOwing = await Sut().AddPaymentAsync(3, Add(1_000_001), default);   // tổng thành +1: khách nợ, vượt khoảng cho phép

        ok.IsSuccess.Should().BeTrue();
        order.OrderPayments.Should().HaveCount(2);
        overCompensate.Message.Should().Be(Messages.OrderPaymentOutOfRange);
        flipsToCustomerOwing.Message.Should().Be(Messages.OrderPaymentOutOfRange);
    }

    [Fact]
    public async Task AddPayment_ZeroAmount_OrFutureDate_IsRefused()
    {
        Order(1_000_000);

        var zero = await Sut().AddPaymentAsync(3, Add(0), default);
        var future = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(Today.AddDays(1), 100, "Cash", null, RowVer), default);

        zero.Message.Should().Be(Messages.OrderPaymentAmountInvalid);
        future.Message.Should().Be(Messages.PaymentDateFuture);
    }

    [Fact]
    public async Task UpdatePayment_Within24Hours_UpdatesWithReason()
    {
        var order = Order(1_000_000, (400_000, 23));

        var result = await Sut().UpdatePaymentAsync(3, 1, Update(500_000), default);

        result.IsSuccess.Should().BeTrue();
        var payment = order.OrderPayments.Single();
        payment.Amount.Should().Be(500_000);
        payment.EditReason.Should().Be("Nhập nhầm");
        payment.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task UpdatePayment_After24Hours_WithoutReason_OrOutOfRange_IsRefused()
    {
        Order(1_000_000, (400_000, 25));
        var expired = await Sut().UpdatePaymentAsync(3, 1, Update(500_000), default);
        expired.ErrorCode.Should().Be(ErrorCodes.PaymentEditExpired);

        _orders.Clear();
        Order(1_000_000, (400_000, 1));
        var noReason = await Sut().UpdatePaymentAsync(3, 1, Update(500_000, " "), default);
        var outOfRange = await Sut().UpdatePaymentAsync(3, 1, Update(1_000_001), default);

        noReason.Message.Should().Be(Messages.PaymentEditReasonRequired);
        outOfRange.Message.Should().Be(Messages.OrderPaymentOutOfRange);
    }

    [Fact]
    public async Task Get_ReturnsRemainingWithSignAndEditability()
    {
        Order(-1_000_000, (-300_000, 30), (-100_000, 1));

        var dto = (await Sut().GetAsync(3, default)).Data!;

        dto.PaidAmount.Should().Be(-400_000);
        dto.Remaining.Should().Be(-600_000);   // mình còn phải trả khách 600.000
        dto.State.Should().Be(OrderPaymentStates.Partial);
        dto.Payments.Select(p => p.CanEdit).Should().Equal(false, true);
    }

    [Fact]
    public async Task Get_ReturnsOrderAmountAsSumOfQtyTimesPrice()
    {
        var order = Order(null);
        order.OrderLines.Add(new OrderLine { OrderedQty = 100, CmtUnitPrice = 15_000 });
        order.OrderLines.Add(new OrderLine { OrderedQty = 50, CmtUnitPrice = 20_000 });

        var dto = (await Sut().GetAsync(3, default)).Data!;

        dto.OrderAmount.Should().Be(2_500_000);
    }

    [Fact]
    public async Task List_FiltersByState()
    {
        Order(1_000_000, (1_000_000, 1));
        var all = await Sut().ListAsync(new OrderPaymentQuery(), default);
        var settled = await Sut().ListAsync(new OrderPaymentQuery { State = OrderPaymentStates.Settled }, default);
        var notSet = await Sut().ListAsync(new OrderPaymentQuery { State = OrderPaymentStates.NotSet }, default);

        all.Data!.Items.Should().ContainSingle().Which.State.Should().Be(OrderPaymentStates.Settled);
        settled.Data!.Items.Should().HaveCount(1);
        notSet.Data!.Items.Should().BeEmpty();
    }
}
