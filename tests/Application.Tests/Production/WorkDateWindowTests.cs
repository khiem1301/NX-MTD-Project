using FluentAssertions;
using NX_MTD.Application.Services;

namespace Application.Tests;

/// <summary>Ngày ghi hàng hỏng, sản lượng, QC, giao việc phải nằm trong thời gian gia công của đơn: từ ngày nhận đơn (DEV-ALL-77).</summary>
public class WorkDateWindowTests
{
    private static readonly DateOnly Received = new(2026, 10, 1);

    [Theory]
    [InlineData("2026-10-01", true)]
    [InlineData("2026-10-02", true)]
    [InlineData("2026-11-05", true)]
    [InlineData("2026-09-30", false)]
    [InlineData("2022-01-15", false)]
    [InlineData("0001-01-01", false)]
    public void BeforeOrderStart_OnlyRejectsDaysBeforeTheReceivedDate(string day, bool allowed)
    {
        var message = ProductionGuard.BeforeOrderStart(DateOnly.Parse(day), Received);

        (message is null).Should().Be(allowed);
    }

    [Fact]
    public void BeforeOrderStart_MessageNamesBothDates()
    {
        var message = ProductionGuard.BeforeOrderStart(new DateOnly(2022, 1, 15), Received);

        message.Should().Contain("15/01/2022").And.Contain("01/10/2026");
    }
}
