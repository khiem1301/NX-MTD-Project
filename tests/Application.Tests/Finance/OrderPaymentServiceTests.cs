using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID660–UTCID749: OrderPaymentService per UNIT_TEST_CASES_REBASELINE_v3.1 sheet OrderPaymentService (P0-2).</summary>
public class OrderPaymentServiceTests
{
    private static readonly byte[] V1 = { 1 };
    private static readonly byte[] V2 = { 2 };
    private static readonly DateTime Now = new(2026, 11, 24, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 24);

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
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        _uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        _uow.SaveChangesAsync().Returns(1);
        _audit.GetHistoryAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AuditLogDto>());
    }

    private OrderPaymentService Sut() => new(_uow, _audit, _clock);

    private CustomerOrder Order(
        int id,
        string code,
        decimal? final = null,
        IEnumerable<OrderPayment>? payments = null,
        IEnumerable<OrderLine>? lines = null,
        string? customer = "Khách A",
        string status = OrderStatuses.InProduction,
        byte[]? rowVer = null)
    {
        var order = new CustomerOrder
        {
            OrderId = id,
            OrderCode = code,
            Status = status,
            RowVer = rowVer ?? V1,
            FinalAmount = final,
            FinalAmountReason = final is null ? null : "Chốt",
            Customer = customer is null ? null! : new Customer { CustomerId = id, CustomerName = customer, Status = "ACTIVE" },
            MaterialStatus = "PENDING",
        };
        if (payments is not null)
        {
            foreach (var payment in payments)
            {
                payment.OrderId = id;
                order.OrderPayments.Add(payment);
            }
        }

        if (lines is not null)
        {
            foreach (var line in lines)
            {
                line.OrderId = id;
                order.OrderLines.Add(line);
            }
        }

        _orders.Add(order);
        return order;
    }

    private static OrderLine Line(int qty, decimal cmtPrice) => new() { OrderedQty = qty, CmtUnitPrice = cmtPrice };

    private static OrderPayment Payment(int id, decimal amount, int ageHours, string method = PaymentMethods.Cash) =>
        new()
        {
            OrderPaymentId = id,
            Amount = amount,
            PaymentDate = Today,
            PaymentMethod = method,
            CreatedAt = Now.AddHours(-ageHours),
        };

    /// <summary>O1 chưa chốt; O2 đã tất toán 100tr; O3 chưa trả; O4 trả một phần 40tr; O5 chốt 0.</summary>
    private void SeedFive()
    {
        Order(1, "DH2611-001", final: null, lines: new[] { Line(10, 1_000m) }, customer: "Khách A");
        Order(2, "DH2611-002", 100_000_000m, new[] { Payment(21, 100_000_000m, 5) }, new[] { Line(100, 1_000_000m) }, "May Việt");
        Order(3, "DH2611-003", 100_000_000m, lines: new[] { Line(50, 2_000_000m) }, customer: "Khách C");
        Order(4, "DH2611-004", 100_000_000m, new[] { Payment(41, 40_000_000m, 5) }, new[] { Line(40, 2_500_000m) }, "Khách D");
        Order(5, "DH2611-005", 0m, lines: new[] { Line(1, 0m) }, customer: "Khách E");
    }

    private void ConflictOnSave() =>
        _uow.When(u => u.SaveChangesAsync()).Do(_ => throw new DbUpdateConcurrencyException("conflict"));

    private async Task AssertCommitted()
    {
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).SaveChangesAsync();
        await _uow.Received(1).CommitTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
    }

    private async Task AssertNotWritten()
    {
        await _uow.DidNotReceive().BeginTransactionAsync();
        await _uow.DidNotReceive().SaveChangesAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
        _audit.DidNotReceive().Add(Arg.Any<AuditEntry>());
        _repo.DidNotReceive().SetOriginalRowVersion(Arg.Any<CustomerOrder>(), Arg.Any<byte[]>());
    }

    private void AssertAudited(string action, int orderId, string? reason) =>
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == "OrderPayment" && e.RecordId == orderId && e.Action == action && e.Reason == reason));

    private static void AssertValidation(Result result, string message)
    {
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.PaymentInvalid);
        result.Message.Should().Be(message);
    }

    private static void AssertNotFound(Result result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    private void AssertRowTouched(CustomerOrder order, byte[] rowVer)
    {
        order.UpdatedAt.Should().Be(Now);
        _repo.Received(1).SetOriginalRowVersion(order, Arg.Is<byte[]>(b => b.SequenceEqual(rowVer)));
    }

    [Fact]
    public void UTCID660_IsEditable_CreatedTwoHoursAgo_ReturnsTrue()
    {
        // Arrange
        var payment = new OrderPayment { CreatedAt = new DateTime(2026, 11, 24, 8, 0, 0) };

        // Act
        var editable = OrderPaymentService.IsEditable(payment, Now);

        // Assert
        editable.Should().BeTrue();
    }

    [Fact]
    public void UTCID661_IsEditable_Exactly24Hours_ReturnsTrue()
    {
        // Arrange
        var payment = new OrderPayment { CreatedAt = new DateTime(2026, 11, 23, 10, 0, 0) };

        // Act
        var editable = OrderPaymentService.IsEditable(payment, Now);

        // Assert
        editable.Should().BeTrue();
    }

    [Fact]
    public void UTCID662_IsEditable_TwentyFourHoursOneSecond_ReturnsFalse()
    {
        // Arrange
        var payment = new OrderPayment { CreatedAt = new DateTime(2026, 11, 23, 9, 59, 59) };

        // Act
        var editable = OrderPaymentService.IsEditable(payment, Now);

        // Assert
        editable.Should().BeFalse();
    }

    [Fact]
    public void UTCID663_IsEditable_CreatedInTheFuture_ReturnsTrue()
    {
        // Arrange
        var payment = new OrderPayment { CreatedAt = new DateTime(2026, 11, 24, 11, 0, 0) };

        // Act
        var editable = OrderPaymentService.IsEditable(payment, Now);

        // Assert
        editable.Should().BeTrue();
    }

    [Fact]
    public void UTCID664_InRange_PaidInsidePositiveFinal_ReturnsTrue()
    {
        // Arrange
        const decimal paid = 50m;
        const decimal finalAmount = 100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeTrue();
    }

    [Fact]
    public void UTCID665_InRange_PaidZeroAgainstPositiveFinal_ReturnsTrue()
    {
        // Arrange
        const decimal paid = 0m;
        const decimal finalAmount = 100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeTrue();
    }

    [Fact]
    public void UTCID666_InRange_PaidEqualsPositiveFinal_ReturnsTrue()
    {
        // Arrange
        const decimal paid = 100m;
        const decimal finalAmount = 100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeTrue();
    }

    [Fact]
    public void UTCID667_InRange_PaidAbovePositiveFinal_ReturnsFalse()
    {
        // Arrange
        const decimal paid = 101m;
        const decimal finalAmount = 100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeFalse();
    }

    [Fact]
    public void UTCID668_InRange_PaidNegativeAgainstPositiveFinal_ReturnsFalse()
    {
        // Arrange
        const decimal paid = -1m;
        const decimal finalAmount = 100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeFalse();
    }

    [Fact]
    public void UTCID669_InRange_PaidEqualsNegativeFinal_ReturnsTrue()
    {
        // Arrange
        const decimal paid = -100m;
        const decimal finalAmount = -100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeTrue();
    }

    [Fact]
    public void UTCID670_InRange_PaidBelowNegativeFinal_ReturnsFalse()
    {
        // Arrange
        const decimal paid = -101m;
        const decimal finalAmount = -100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeFalse();
    }

    [Fact]
    public void UTCID671_InRange_PaidZeroFinalZero_ReturnsTrue()
    {
        // Arrange
        const decimal paid = 0m;
        const decimal finalAmount = 0m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeTrue();
    }

    [Fact]
    public void UTCID672_InRange_PositivePaidAgainstNegativeFinal_ReturnsFalse()
    {
        // Arrange
        const decimal paid = 10m;
        const decimal finalAmount = -100m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeFalse();
    }

    [Fact]
    public void UTCID673_InRange_PositivePaidAgainstZeroFinal_ReturnsFalse()
    {
        // Arrange
        const decimal paid = 1m;
        const decimal finalAmount = 0m;

        // Act
        var inRange = OrderPaymentService.InRange(paid, finalAmount);

        // Assert
        inRange.Should().BeFalse();
    }

    [Fact]
    public void UTCID674_Remaining_PartialPositive_ReturnsDifference()
    {
        // Arrange
        const decimal finalAmount = 100_000_000m;
        const decimal paid = 30_000_000m;

        // Act
        var remaining = OrderPaymentService.Remaining(finalAmount, paid);

        // Assert
        remaining.Should().Be(70_000_000m);
    }

    [Fact]
    public void UTCID675_Remaining_BothZero_ReturnsZero()
    {
        // Arrange
        const decimal finalAmount = 0m;
        const decimal paid = 0m;

        // Act
        var remaining = OrderPaymentService.Remaining(finalAmount, paid);

        // Assert
        remaining.Should().Be(0m);
    }

    [Fact]
    public void UTCID676_Remaining_FinalNotSet_ReturnsNull()
    {
        // Arrange
        const decimal paid = 0m;

        // Act
        var remaining = OrderPaymentService.Remaining(null, paid);

        // Assert
        remaining.Should().BeNull();
    }

    [Fact]
    public void UTCID677_StateOf_PaidBetweenZeroAndFinal_ReturnsPartial()
    {
        // Arrange
        const decimal finalAmount = 100m;
        const decimal paid = 30m;

        // Act
        var state = OrderPaymentService.StateOf(finalAmount, paid);

        // Assert
        state.Should().Be(OrderPaymentStates.Partial);
    }

    [Fact]
    public void UTCID678_StateOf_PaidZero_ReturnsUnpaid()
    {
        // Arrange
        const decimal finalAmount = 100m;
        const decimal paid = 0m;

        // Act
        var state = OrderPaymentService.StateOf(finalAmount, paid);

        // Assert
        state.Should().Be(OrderPaymentStates.Unpaid);
    }

    [Fact]
    public void UTCID679_StateOf_PaidEqualsFinal_ReturnsSettled()
    {
        // Arrange
        const decimal finalAmount = 100m;
        const decimal paid = 100m;

        // Act
        var state = OrderPaymentService.StateOf(finalAmount, paid);

        // Assert
        state.Should().Be(OrderPaymentStates.Settled);
    }

    [Fact]
    public void UTCID680_StateOf_FinalZeroPaidZero_ReturnsSettledBeforeUnpaid()
    {
        // Arrange
        const decimal finalAmount = 0m;
        const decimal paid = 0m;

        // Act
        var state = OrderPaymentService.StateOf(finalAmount, paid);

        // Assert
        state.Should().Be(OrderPaymentStates.Settled);
    }

    [Fact]
    public void UTCID681_StateOf_FinalNull_ReturnsNotSet()
    {
        // Arrange
        const decimal paid = 0m;

        // Act
        var state = OrderPaymentService.StateOf(null, paid);

        // Assert
        state.Should().Be(OrderPaymentStates.NotSet);
    }

    [Fact]
    public async Task UTCID682_ListAsync_FiveOrders_ReturnsAmountRemainingAndState()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = null, Page = 1 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var page = result.Data!;
        page.Items.Should().HaveCount(5);
        page.TotalItems.Should().Be(5);
        page.Items.Select(i => i.OrderId).Should().Equal(5, 4, 3, 2, 1);

        var byId = page.Items.ToDictionary(i => i.OrderId);
        byId[1].OrderAmount.Should().Be(10_000m);
        byId[1].FinalAmount.Should().BeNull();
        byId[1].PaidAmount.Should().Be(0m);
        byId[1].Remaining.Should().BeNull();
        byId[1].State.Should().Be(OrderPaymentStates.NotSet);

        byId[2].OrderCode.Should().Be("DH2611-002");
        byId[2].CustomerName.Should().Be("May Việt");
        byId[2].OrderAmount.Should().Be(100_000_000m);
        byId[2].FinalAmount.Should().Be(100_000_000m);
        byId[2].PaidAmount.Should().Be(100_000_000m);
        byId[2].Remaining.Should().Be(0m);
        byId[2].State.Should().Be(OrderPaymentStates.Settled);

        byId[3].OrderAmount.Should().Be(100_000_000m);
        byId[3].PaidAmount.Should().Be(0m);
        byId[3].Remaining.Should().Be(100_000_000m);
        byId[3].State.Should().Be(OrderPaymentStates.Unpaid);

        byId[4].OrderAmount.Should().Be(100_000_000m);
        byId[4].PaidAmount.Should().Be(40_000_000m);
        byId[4].Remaining.Should().Be(60_000_000m);
        byId[4].State.Should().Be(OrderPaymentStates.Partial);

        byId[5].OrderAmount.Should().Be(0m);
        byId[5].FinalAmount.Should().Be(0m);
        byId[5].PaidAmount.Should().Be(0m);
        byId[5].Remaining.Should().Be(0m);
        byId[5].State.Should().Be(OrderPaymentStates.Settled);
        byId[5].OrderStatus.Should().Be("InProduction");
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID683_ListAsync_StateNotSet_ReturnsOnlyUnsetOrder()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = "NotSet" }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle().Which.OrderId.Should().Be(1);
        result.Data.Items.Single().State.Should().Be(OrderPaymentStates.NotSet);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID684_ListAsync_StateSettled_ReturnsFullyPaidAndZeroFinal()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = "Settled" }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Select(i => i.OrderId).Should().BeEquivalentTo(new[] { 2, 5 });
        result.Data.Items.Should().OnlyContain(i => i.State == OrderPaymentStates.Settled);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID685_ListAsync_StateUnpaid_ExcludesZeroFinal()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = "Unpaid" }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle().Which.OrderId.Should().Be(3);
        result.Data.Items.Single().State.Should().Be(OrderPaymentStates.Unpaid);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID686_ListAsync_StatePartial_ReturnsOnlyPartialOrder()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = "Partial" }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle().Which.OrderId.Should().Be(4);
        result.Data.Items.Single().PaidAmount.Should().Be(40_000_000m);
        result.Data.Items.Single().State.Should().Be(OrderPaymentStates.Partial);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID687_ListAsync_KeywordCustomerName_ReturnsMatchingOrder()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { Keyword = "May Việt" }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Should().ContainSingle().Which;
        item.OrderId.Should().Be(2);
        item.OrderCode.Should().Be("DH2611-002");
        item.CustomerName.Should().Be("May Việt");
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID688_ListAsync_TwoLines_AmountIsQtyTimesCmt()
    {
        // Arrange
        Order(6, "DH2611-006", lines: new[] { Line(1_000, 12_000m), Line(500, 20_000m) });

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = null }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle().Which.OrderAmount.Should().Be(22_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID689_ListAsync_NoOrderLines_AmountIsZero()
    {
        // Arrange
        Order(7, "DH2611-007");

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = null }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle().Which.OrderAmount.Should().Be(0m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID690_ListAsync_EmptyStore_ReturnsEmptyPage()
    {
        // Arrange

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(0);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID691_ListAsync_PageThreeSizeTwo_ReturnsLastItemAndTotalFive()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { Page = 3, PageSize = 2 }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
        result.Data.TotalItems.Should().Be(5);
        result.Data.Page.Should().Be(3);
        result.Data.PageSize.Should().Be(2);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID692_ListAsync_BlankKeyword_DoesNotFilter()
    {
        // Arrange
        SeedFive();

        // Act
        var all = await Sut().ListAsync(new OrderPaymentQuery(), default);
        var blank = await Sut().ListAsync(new OrderPaymentQuery { Keyword = "" }, default);

        // Assert
        blank.IsSuccess.Should().BeTrue();
        blank.Data!.TotalItems.Should().Be(5);
        blank.Data.Items.Select(i => i.OrderId).Should().Equal(all.Data!.Items.Select(i => i.OrderId));
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID693_ListAsync_StateSettledWrongCase_MatchesIgnoringCaseOrValidation()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = "settled" }, default);

        // Assert — ideal: không phân biệt hoa thường, hoặc Failure Validation (list rỗng là lệch spec)
        if (result.IsSuccess)
            result.Data!.Items.Select(i => i.OrderId).Should().BeEquivalentTo(new[] { 2, 5 });
        else
            result.Kind.Should().Be(ResultKind.Validation);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID694_ListAsync_UnknownState_ReturnsEmptySuccess()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { State = "Foo" }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Kind.Should().Be(ResultKind.Ok);
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(0);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID695_ListAsync_PercentKeyword_MatchesLiteralNotEveryOrder()
    {
        // Arrange
        Order(11, "DH%01", customer: "Khách A");
        Order(12, "DH2611-012", customer: "Khách B");

        // Act
        var result = await Sut().ListAsync(new OrderPaymentQuery { Keyword = "%" }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalItems.Should().Be(1);
        result.Data.Items.Should().ContainSingle().Which.OrderCode.Should().Be("DH%01");
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID696_GetAsync_TwoPayments_ReturnsPaidRemainingEditWindowAndHistory()
    {
        // Arrange
        var order = Order(
            4,
            "DH2611-004",
            100_000_000m,
            new[]
            {
                Payment(1, 20_000_000m, 2, PaymentMethods.Cash),
                Payment(2, 20_000_000m, 30, PaymentMethods.BankTransfer),
            },
            new[] { Line(1_000, 12_000m), Line(500, 20_000m) },
            customer: "May Việt");
        var history = new AuditLogDto[]
        {
            new(1, AuditActions.Update, null, null, "a", 1, "Kế toán", Now),
            new(2, AuditActions.Confirm, null, null, "b", 1, "Kế toán", Now),
            new(3, AuditActions.Update, null, null, "c", 1, "Kế toán", Now),
        };
        _audit.GetHistoryAsync("OrderPayment", order.OrderId, Arg.Any<CancellationToken>()).Returns(history);

        // Act
        var result = await Sut().GetAsync(order.OrderId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.CustomerName.Should().Be("May Việt");
        dto.OrderStatus.Should().Be("InProduction");
        dto.OrderAmount.Should().Be(22_000_000m);
        dto.FinalAmount.Should().Be(100_000_000m);
        dto.PaidAmount.Should().Be(40_000_000m);
        dto.Remaining.Should().Be(60_000_000m);
        dto.State.Should().Be(OrderPaymentStates.Partial);
        dto.RowVer.Should().Equal(V1);
        dto.History.Should().HaveCount(3);
        dto.Payments.Select(p => p.CreatedAt).Should().BeInAscendingOrder();
        var recent = dto.Payments.Single(p => p.OrderPaymentId == 1);
        var expired = dto.Payments.Single(p => p.OrderPaymentId == 2);
        recent.Amount.Should().Be(20_000_000m);
        recent.CanEdit.Should().BeTrue();
        recent.PaymentMethod.Should().Be("Cash");
        expired.Amount.Should().Be(20_000_000m);
        expired.CanEdit.Should().BeFalse();
        expired.PaymentMethod.Should().Be("BankTransfer");
        await _audit.Received(1).GetHistoryAsync("OrderPayment", order.OrderId, Arg.Any<CancellationToken>());
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID697_GetAsync_PaymentAgedExactly24Hours_CanEdit()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 24) });

        // Act
        var result = await Sut().GetAsync(order.OrderId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Payments.Should().ContainSingle().Which.CanEdit.Should().BeTrue();
        result.Data.Payments.Single().CreatedAt.Should().Be(Now.AddHours(-24));
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID698_GetAsync_PaymentAged24HoursOneSecond_CannotEdit()
    {
        // Arrange
        var payment = Payment(1, 20_000_000m, 24);
        payment.CreatedAt = Now.AddHours(-24).AddSeconds(-1);
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { payment });

        // Act
        var result = await Sut().GetAsync(order.OrderId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Payments.Should().ContainSingle().Which.CanEdit.Should().BeFalse();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID699_GetAsync_NoPayments_PaidZeroAndUnpaid()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m, lines: new[] { Line(1, 1m) });

        // Act
        var result = await Sut().GetAsync(order.OrderId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.PaidAmount.Should().Be(0m);
        result.Data.Payments.Should().BeEmpty();
        result.Data.State.Should().Be(OrderPaymentStates.Unpaid);
        result.Data.Remaining.Should().Be(100_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID700_GetAsync_FinalNotSet_RemainingNullAndNotSet()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var result = await Sut().GetAsync(order.OrderId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FinalAmount.Should().BeNull();
        result.Data.Remaining.Should().BeNull();
        result.Data.State.Should().Be(OrderPaymentStates.NotSet);
        result.Data.PaidAmount.Should().Be(0m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID701_GetAsync_UnknownOrder_ReturnsNotFound()
    {
        // Arrange
        SeedFive();

        // Act
        var result = await Sut().GetAsync(999, default);

        // Assert
        AssertNotFound(result);
        await _audit.DidNotReceive().GetHistoryAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID702_GetAsync_NullCustomer_ReturnsNullCustomerName()
    {
        // Arrange
        var order = Order(8, "DH2611-008", customer: null);

        // Act
        var act = async () => await Sut().GetAsync(order.OrderId, default);

        // Assert — ideal: không ném, CustomerName null
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        result.Data!.CustomerName.Should().BeNull();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID703_GetAsync_OrderAmountMatchesListAmount()
    {
        // Arrange
        var order = Order(6, "DH2611-006", lines: new[] { Line(1_000, 12_000m), Line(500, 20_000m) });

        // Act
        var listed = await Sut().ListAsync(new OrderPaymentQuery(), default);
        var got = await Sut().GetAsync(order.OrderId, default);

        // Assert
        listed.IsSuccess.Should().BeTrue();
        got.IsSuccess.Should().BeTrue();
        listed.Data!.Items.Should().ContainSingle().Which.OrderAmount.Should().Be(22_000_000m);
        got.Data!.OrderAmount.Should().Be(22_000_000m);
        got.Data.OrderAmount.Should().Be(listed.Data.Items.Single().OrderAmount);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID704_SetFinalAmountAsync_FirstFinal_PersistsReasonAuditAndCommit()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var result = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(100_000_000m, "Chốt theo biên bản", V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.FinalAmountSaved);
        order.FinalAmount.Should().Be(100_000_000m);
        order.FinalAmountReason.Should().Be("Chốt theo biên bản");
        AssertRowTouched(order, V1);
        AssertAudited(AuditActions.Update, order.OrderId, "Chốt theo biên bản");
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID705_SetFinalAmountAsync_AbovePaid_LeavesPartialWithRemaining()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().SetFinalAmountAsync(4, new SetFinalAmountDto(50_000_000m, "Điều chỉnh", V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.FinalAmount.Should().Be(50_000_000m);
        order.FinalAmountReason.Should().Be("Điều chỉnh");
        var paid = order.OrderPayments.Sum(p => p.Amount);
        OrderPaymentService.Remaining(order.FinalAmount, paid).Should().Be(10_000_000m);
        OrderPaymentService.StateOf(order.FinalAmount, paid).Should().Be(OrderPaymentStates.Partial);
        AssertRowTouched(order, V1);
        AssertAudited(AuditActions.Update, order.OrderId, "Điều chỉnh");
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID706_SetFinalAmountAsync_EqualPaid_BecomesSettled()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().SetFinalAmountAsync(4, new SetFinalAmountDto(40_000_000m, "Chốt đúng đã trả", V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.FinalAmount.Should().Be(40_000_000m);
        var paid = order.OrderPayments.Sum(p => p.Amount);
        OrderPaymentService.Remaining(order.FinalAmount, paid).Should().Be(0m);
        OrderPaymentService.StateOf(order.FinalAmount, paid).Should().Be(OrderPaymentStates.Settled);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID707_SetFinalAmountAsync_BelowPaid_ValidationAndNoSave()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().SetFinalAmountAsync(4, new SetFinalAmountDto(39_999_999m, "Hạ dưới đã trả", V1), default);

        // Assert
        AssertValidation(result, Messages.FinalAmountBelowPaid);
        order.FinalAmount.Should().Be(100_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID708_SetFinalAmountAsync_Zero_SettledAndLaterNonZeroPaymentsRejected()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var set = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(0m, "Chốt không thu", V1), default);
        var positive = await Sut().AddPaymentAsync(1, new AddOrderPaymentDto(Today, 1_000_000m, "CASH", null, V1), default);
        var negative = await Sut().AddPaymentAsync(1, new AddOrderPaymentDto(Today, -1_000_000m, "CASH", null, V1), default);

        // Assert
        set.IsSuccess.Should().BeTrue();
        order.FinalAmount.Should().Be(0m);
        OrderPaymentService.StateOf(order.FinalAmount, order.OrderPayments.Sum(p => p.Amount)).Should().Be(OrderPaymentStates.Settled);
        AssertValidation(positive, Messages.OrderPaymentOutOfRange);
        AssertValidation(negative, Messages.OrderPaymentOutOfRange);
        order.OrderPayments.Should().BeEmpty();
        await _uow.Received(1).SaveChangesAsync();
        await _uow.Received(1).CommitTransactionAsync();
        _audit.Received(1).Add(Arg.Any<AuditEntry>());
    }

    [Fact]
    public async Task UTCID709_SetFinalAmountAsync_NegativeAmount_IsAllowed()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var result = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(-5_000_000m, "Đền hợp đồng", V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.FinalAmount.Should().Be(-5_000_000m);
        order.FinalAmountReason.Should().Be("Đền hợp đồng");
        AssertRowTouched(order, V1);
        AssertAudited(AuditActions.Update, order.OrderId, "Đền hợp đồng");
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID710_SetFinalAmountAsync_NegativeWhilePaidPositive_BelowPaid()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().SetFinalAmountAsync(4, new SetFinalAmountDto(-5_000_000m, "Đổi dấu", V1), default);

        // Assert
        AssertValidation(result, Messages.FinalAmountBelowPaid);
        order.FinalAmount.Should().Be(100_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID711_SetFinalAmountAsync_EmptyReason_Validation()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var result = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(100_000_000m, "", V1), default);

        // Assert
        AssertValidation(result, Messages.FinalAmountReasonRequired);
        order.FinalAmount.Should().BeNull();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID712_SetFinalAmountAsync_BlankReason_Validation()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var result = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(100_000_000m, "   ", V1), default);

        // Assert
        AssertValidation(result, Messages.FinalAmountReasonRequired);
        order.FinalAmount.Should().BeNull();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID713_SetFinalAmountAsync_Reason500Chars_Succeeds()
    {
        // Arrange
        var order = Order(1, "DH2611-001");
        var reason = new string('R', 500);

        // Act
        var result = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(100_000_000m, reason, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.FinalAmountReason.Should().Be(reason);
        order.FinalAmountReason.Should().HaveLength(500);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID714_SetFinalAmountAsync_AboveCmtSum_Succeeds()
    {
        // Arrange
        var order = Order(1, "DH2611-001", lines: new[] { Line(1_000, 100_000m) });

        // Act
        var result = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(200_000_000m, "Chốt cao hơn CMT", V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.OrderLines.Sum(l => l.OrderedQty * l.CmtUnitPrice).Should().Be(100_000_000m);
        order.FinalAmount.Should().Be(200_000_000m);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID715_SetFinalAmountAsync_UnknownOrder_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await Sut().SetFinalAmountAsync(999, new SetFinalAmountDto(100_000_000m, "Chốt", V1), default);

        // Assert
        AssertNotFound(result);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID716_SetFinalAmountAsync_NullReason_Validation()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var result = await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(100_000_000m, null!, V1), default);

        // Assert
        AssertValidation(result, Messages.FinalAmountReasonRequired);
        order.FinalAmount.Should().BeNull();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID717_SetFinalAmountAsync_StaleRowVersion_ConflictAndRollback()
    {
        // Arrange
        var order = Order(1, "DH2611-001", rowVer: V2);
        ConflictOnSave();

        // Act
        var act = async () => await Sut().SetFinalAmountAsync(1, new SetFinalAmountDto(100_000_000m, "Chốt theo biên bản", V1), default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        _repo.Received(1).SetOriginalRowVersion(order, Arg.Is<byte[]>(b => b.SequenceEqual(V1)));
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID718_SetFinalAmountAsync_NullDto_ArgumentNullOrValidation()
    {
        // Arrange
        Order(1, "DH2611-001");

        // Act
        Exception? thrown = null;
        Result? result = null;
        try
        {
            result = await Sut().SetFinalAmountAsync(1, null!, default);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        // Assert — ArgumentNullException hoặc Failure Validation
        if (thrown is not null)
            thrown.Should().BeOfType<ArgumentNullException>();
        else
        {
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Validation);
        }

        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID719_AddPaymentAsync_BankTransfer_SetsCreatedAtAndConfirmAudit()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(
            3, new AddOrderPaymentDto(new DateOnly(2026, 11, 24), 30_000_000m, "BANK_TRANSFER", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.OrderPaymentAdded);
        var payment = order.OrderPayments.Should().ContainSingle().Which;
        payment.Amount.Should().Be(30_000_000m);
        payment.PaymentMethod.Should().Be(PaymentMethods.BankTransfer);
        payment.PaymentDate.Should().Be(Today);
        payment.CreatedAt.Should().Be(Now);
        order.OrderPayments.Sum(p => p.Amount).Should().Be(30_000_000m);
        AssertRowTouched(order, V1);
        AssertAudited(AuditActions.Confirm, order.OrderId, null);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID720_AddPaymentAsync_Cash_Succeeds()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(Today, 10_000_000m, "CASH", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.OrderPayments.Should().ContainSingle().Which.PaymentMethod.Should().Be(PaymentMethods.Cash);
        order.OrderPayments.Single().Amount.Should().Be(10_000_000m);
        AssertAudited(AuditActions.Confirm, order.OrderId, null);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID721_AddPaymentAsync_ExactRemainder_BecomesSettled()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().AddPaymentAsync(4, new AddOrderPaymentDto(Today, 60_000_000m, "CASH", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var paid = order.OrderPayments.Sum(p => p.Amount);
        paid.Should().Be(100_000_000m);
        OrderPaymentService.StateOf(order.FinalAmount, paid).Should().Be(OrderPaymentStates.Settled);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID722_AddPaymentAsync_OneAboveFinal_ValidationAndNoSave()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().AddPaymentAsync(4, new AddOrderPaymentDto(Today, 60_000_001m, "CASH", null, V1), default);

        // Assert
        AssertValidation(result, Messages.OrderPaymentOutOfRange);
        order.OrderPayments.Should().ContainSingle().Which.Amount.Should().Be(40_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID723_AddPaymentAsync_ZeroAmount_Validation()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(Today, 0m, "CASH", null, V1), default);

        // Assert
        AssertValidation(result, Messages.OrderPaymentAmountInvalid);
        order.OrderPayments.Should().BeEmpty();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID724_AddPaymentAsync_RefundLeavingPositivePaid_Succeeds()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().AddPaymentAsync(4, new AddOrderPaymentDto(Today, -10_000_000m, "CASH", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.OrderPayments.Sum(p => p.Amount).Should().Be(30_000_000m);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID725_AddPaymentAsync_RefundBelowZero_Validation()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 40_000_000m, 5) });

        // Act
        var result = await Sut().AddPaymentAsync(4, new AddOrderPaymentDto(Today, -40_000_001m, "CASH", null, V1), default);

        // Assert
        AssertValidation(result, Messages.OrderPaymentOutOfRange);
        order.OrderPayments.Sum(p => p.Amount).Should().Be(40_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID726_AddPaymentAsync_NegativeFinalExact_BecomesSettled()
    {
        // Arrange
        var order = Order(9, "DH2611-009", -5_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(9, new AddOrderPaymentDto(Today, -5_000_000m, "CASH", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var paid = order.OrderPayments.Sum(p => p.Amount);
        paid.Should().Be(-5_000_000m);
        OrderPaymentService.StateOf(order.FinalAmount, paid).Should().Be(OrderPaymentStates.Settled);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID727_AddPaymentAsync_PositiveAgainstNegativeFinal_Validation()
    {
        // Arrange
        var order = Order(9, "DH2611-009", -5_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(9, new AddOrderPaymentDto(Today, 1_000_000m, "CASH", null, V1), default);

        // Assert
        AssertValidation(result, Messages.OrderPaymentOutOfRange);
        order.OrderPayments.Should().BeEmpty();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID728_AddPaymentAsync_PaymentDateToday_Succeeds()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(new DateOnly(2026, 11, 24), 10_000_000m, "CASH", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.OrderPayments.Should().ContainSingle().Which.PaymentDate.Should().Be(Today);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID729_AddPaymentAsync_PaymentDateTomorrow_Validation()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(new DateOnly(2026, 11, 25), 10_000_000m, "CASH", null, V1), default);

        // Assert
        AssertValidation(result, Messages.PaymentDateFuture);
        order.OrderPayments.Should().BeEmpty();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID730_AddPaymentAsync_UnknownMethod_Validation()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m);

        // Act
        var result = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(Today, 10_000_000m, "MOMO", null, V1), default);

        // Assert
        AssertValidation(result, Messages.PaymentMethodInvalid);
        order.OrderPayments.Should().BeEmpty();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID731_AddPaymentAsync_NullMethod_Validation()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m);

        // Act
        Exception? thrown = null;
        Result? result = null;
        try
        {
            result = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(Today, 10_000_000m, null!, null, V1), default);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        // Assert
        thrown.Should().BeNull();
        AssertValidation(result!, Messages.PaymentMethodInvalid);
        order.OrderPayments.Should().BeEmpty();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID732_AddPaymentAsync_FinalNotSet_ConflictNeedFinalAmount()
    {
        // Arrange
        var order = Order(1, "DH2611-001");

        // Act
        var result = await Sut().AddPaymentAsync(1, new AddOrderPaymentDto(Today, 10_000_000m, "CASH", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.PaymentInvalid);
        result.Message.Should().Be(Messages.OrderPaymentNeedFinalAmount);
        order.OrderPayments.Should().BeEmpty();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID733_AddPaymentAsync_UnknownOrder_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await Sut().AddPaymentAsync(999, new AddOrderPaymentDto(Today, 10_000_000m, "CASH", null, V1), default);

        // Assert
        AssertNotFound(result);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID734_AddPaymentAsync_CancelledOrder_StillSucceeds()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m, status: OrderStatuses.Cancelled);

        // Act
        var result = await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(Today, 10_000_000m, "CASH", null, V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatuses.Cancelled);
        order.OrderPayments.Should().ContainSingle().Which.Amount.Should().Be(10_000_000m);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID735_AddPaymentAsync_StaleRowVersion_ConflictAndRollback()
    {
        // Arrange
        var order = Order(3, "DH2611-003", 100_000_000m, rowVer: V2);
        ConflictOnSave();

        // Act
        var act = async () => await Sut().AddPaymentAsync(3, new AddOrderPaymentDto(Today, 10_000_000m, "CASH", null, V1), default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        _repo.Received(1).SetOriginalRowVersion(order, Arg.Is<byte[]>(b => b.SequenceEqual(V1)));
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID736_UpdatePaymentAsync_WithinWindow_UpdatesAmountReasonAndAudits()
    {
        // Arrange
        var order = Order(
            4,
            "DH2611-004",
            100_000_000m,
            new[] { Payment(1, 20_000_000m, 2), Payment(2, 20_000_000m, 30) },
            customer: "Khách D");

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4,
            1,
            new UpdateOrderPaymentDto(new DateOnly(2026, 11, 24), 25_000_000m, "CASH", null, "Sửa nhập sai", V1),
            default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.PaymentUpdated);
        var payment = order.OrderPayments.Single(p => p.OrderPaymentId == 1);
        payment.Amount.Should().Be(25_000_000m);
        payment.PaymentMethod.Should().Be(PaymentMethods.Cash);
        payment.PaymentDate.Should().Be(Today);
        payment.EditReason.Should().Be("Sửa nhập sai");
        payment.UpdatedAt.Should().Be(Now);
        order.OrderPayments.Single(p => p.OrderPaymentId == 2).Amount.Should().Be(20_000_000m);
        AssertRowTouched(order, V1);
        AssertAudited(AuditActions.Update, order.OrderId, "Sửa nhập sai");
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID737_UpdatePaymentAsync_AgedExactly24Hours_Succeeds()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 24) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 25_000_000m, "CASH", null, "Sửa đúng hạn", V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.OrderPayments.Single().Amount.Should().Be(25_000_000m);
        order.OrderPayments.Single().EditReason.Should().Be("Sửa đúng hạn");
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID738_UpdatePaymentAsync_Aged24HoursOneSecond_EditExpired()
    {
        // Arrange
        var payment = Payment(1, 20_000_000m, 24);
        payment.CreatedAt = Now.AddHours(-24).AddSeconds(-1);
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { payment });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 25_000_000m, "CASH", null, "Sửa quá hạn", V1), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PaymentEditExpired);
        result.Message.Should().Be(Messages.PaymentEditExpired);
        order.OrderPayments.Single().Amount.Should().Be(20_000_000m);
        order.OrderPayments.Single().EditReason.Should().BeNull();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID739_UpdatePaymentAsync_OthersPlusNewEqualsFinal_Succeeds()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2), Payment(2, 20_000_000m, 30) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 80_000_000m, "CASH", null, "Chốt đủ", V1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.OrderPayments.Sum(p => p.Amount).Should().Be(100_000_000m);
        order.OrderPayments.Single(p => p.OrderPaymentId == 1).Amount.Should().Be(80_000_000m);
        OrderPaymentService.StateOf(order.FinalAmount, order.OrderPayments.Sum(p => p.Amount)).Should().Be(OrderPaymentStates.Settled);
        await AssertCommitted();
    }

    [Fact]
    public async Task UTCID740_UpdatePaymentAsync_OneAboveFinal_Validation()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2), Payment(2, 20_000_000m, 30) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 80_000_001m, "CASH", null, "Vượt", V1), default);

        // Assert
        AssertValidation(result, Messages.OrderPaymentOutOfRange);
        order.OrderPayments.Single(p => p.OrderPaymentId == 1).Amount.Should().Be(20_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID741_UpdatePaymentAsync_ZeroAmount_Validation()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2), Payment(2, 20_000_000m, 30) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 0m, "CASH", null, "Xóa bằng 0", V1), default);

        // Assert
        AssertValidation(result, Messages.OrderPaymentAmountInvalid);
        order.OrderPayments.Should().HaveCount(2);
        order.OrderPayments.Sum(p => p.Amount).Should().Be(40_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID742_UpdatePaymentAsync_EmptyReason_Validation()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 25_000_000m, "CASH", null, "", V1), default);

        // Assert
        AssertValidation(result, Messages.PaymentEditReasonRequired);
        order.OrderPayments.Single().Amount.Should().Be(20_000_000m);
        order.OrderPayments.Single().EditReason.Should().BeNull();
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID743_UpdatePaymentAsync_FutureDate_Validation()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(new DateOnly(2026, 11, 25), 25_000_000m, "CASH", null, "Đổi ngày", V1), default);

        // Assert
        AssertValidation(result, Messages.PaymentDateFuture);
        order.OrderPayments.Single().PaymentDate.Should().Be(Today);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID744_UpdatePaymentAsync_UnknownOrder_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await Sut().UpdatePaymentAsync(
            999, 1, new UpdateOrderPaymentDto(Today, 25_000_000m, "CASH", null, "Sửa", V1), default);

        // Assert
        AssertNotFound(result);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID745_UpdatePaymentAsync_UnknownPayment_ReturnsNotFound()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 999, new UpdateOrderPaymentDto(Today, 25_000_000m, "CASH", null, "Sửa", V1), default);

        // Assert
        AssertNotFound(result);
        order.OrderPayments.Single().Amount.Should().Be(20_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID746_UpdatePaymentAsync_PaymentOnAnotherOrder_ReturnsNotFound()
    {
        // Arrange
        var other = Order(3, "DH2611-003", 100_000_000m, new[] { Payment(5, 15_000_000m, 2) });
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 5, new UpdateOrderPaymentDto(Today, 25_000_000m, "CASH", null, "Sửa nhầm đơn", V1), default);

        // Assert
        AssertNotFound(result);
        other.OrderPayments.Single().Amount.Should().Be(15_000_000m);
        order.OrderPayments.Single().Amount.Should().Be(20_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID747_UpdatePaymentAsync_FinalNullWithExistingPayment_ReturnsNotFound()
    {
        // Arrange
        var order = Order(10, "DH2611-010", final: null, payments: new[] { Payment(1, 10_000_000m, 2) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            10, 1, new UpdateOrderPaymentDto(Today, 12_000_000m, "CASH", null, "Sửa khi chưa chốt", V1), default);

        // Assert
        AssertNotFound(result);
        order.FinalAmount.Should().BeNull();
        order.OrderPayments.Single().Amount.Should().Be(10_000_000m);
        await AssertNotWritten();
    }

    [Fact]
    public async Task UTCID748_UpdatePaymentAsync_StaleRowVersion_ConflictAndRollback()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2) }, rowVer: V2);
        ConflictOnSave();

        // Act
        var act = async () => await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 25_000_000m, "CASH", null, "Sửa", V1), default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        _repo.Received(1).SetOriginalRowVersion(order, Arg.Is<byte[]>(b => b.SequenceEqual(V1)));
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID749_UpdatePaymentAsync_UnknownMethod_Validation()
    {
        // Arrange
        var order = Order(4, "DH2611-004", 100_000_000m, new[] { Payment(1, 20_000_000m, 2) });

        // Act
        var result = await Sut().UpdatePaymentAsync(
            4, 1, new UpdateOrderPaymentDto(Today, 25_000_000m, "ATM", null, "Đổi hình thức", V1), default);

        // Assert
        AssertValidation(result, Messages.PaymentMethodInvalid);
        order.OrderPayments.Single().PaymentMethod.Should().Be(PaymentMethods.Cash);
        order.OrderPayments.Single().Amount.Should().Be(20_000_000m);
        await AssertNotWritten();
    }
}
