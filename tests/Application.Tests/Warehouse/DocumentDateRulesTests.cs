using FluentAssertions;
using NX_MTD.Application.Common;

namespace Application.Tests;

public class DocumentDateRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);
    private static readonly DateOnly Received = new(2026, 11, 3);

    [Theory]
    [InlineData(0, true)]
    [InlineData(-4, true)]    // = ngày nhận đơn
    [InlineData(-7, false)]   // quá 7 ngày và trước ngày nhận đơn (đơn nhận 4 ngày trước)
    [InlineData(1, false)]    // tương lai
    public void DocumentDate_WithOrder_MustBeBetweenReceivedDateAndToday(int offset, bool valid) =>
        (DocumentDateRules.CheckDocumentDate(Today.AddDays(offset), Today, Received, "Ngày cấp") is null).Should().Be(valid);

    [Theory]
    [InlineData(0, true)]
    [InlineData(-7, true)]
    [InlineData(-8, false)]
    [InlineData(1, false)]
    public void DocumentDate_WithoutOrder_MaxSevenDaysBackAndNotInTheFuture(int offset, bool valid) =>
        (DocumentDateRules.CheckDocumentDate(Today.AddDays(offset), Today, null, "Ngày kiểm kê") is null).Should().Be(valid);

    [Fact]
    public void DocumentDate_ErrorsNameTheField()
    {
        DocumentDateRules.CheckDocumentDate(Today.AddDays(1), Today, null, "Ngày cấp").Should().Be("Ngày cấp không được sau hôm nay.");
        DocumentDateRules.CheckDocumentDate(Today.AddDays(-8), Today, null, "Ngày nhập")
            .Should().Be("Ngày nhập không được trước hôm nay quá 7 ngày.");
        DocumentDateRules.CheckDocumentDate(Today.AddDays(-5), Today, Received, "Ngày cấp")
            .Should().Be("Ngày cấp không được trước ngày nhận đơn (03/11/2026).");
    }

    [Theory]
    [InlineData(-1, null, null, false)]    // trước hôm nay
    [InlineData(0, null, null, true)]
    [InlineData(5, 10, null, false)]       // trước ngày đặt hàng (đặt sau)
    [InlineData(10, 10, null, true)]
    [InlineData(20, 0, 15, false)]         // sau hạn giao của đơn hàng
    [InlineData(15, 0, 15, true)]
    public void ExpectedDelivery_NotBeforeTodayOrOrderDate_NotAfterOrderDue(int offset, int? notBeforeOffset, int? dueOffset, bool valid) =>
        (DocumentDateRules.CheckExpectedDelivery(Today.AddDays(offset), Today,
            notBeforeOffset is { } n ? Today.AddDays(n) : null, dueOffset is { } d ? Today.AddDays(d) : null) is null).Should().Be(valid);
}
