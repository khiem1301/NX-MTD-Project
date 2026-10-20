using FluentAssertions;
using NX_MTD.Application.Services;

namespace Application.Tests;

public class CuttingLayCalculatorTests
{
    [Fact]
    public void CalculatedQty_PliesTimesRatio() => CuttingLayCalculator.CalculatedQty(40, 3).Should().Be(120);

    [Theory]
    [InlineData(150, 3, 154)]
    [InlineData(100, 3, 103)]
    [InlineData(10, 3, 10)]
    [InlineData(100, 0, 100)]
    public void Cap_RoundsDown(int planned, decimal surplus, int expected) =>
        CuttingLayCalculator.Cap(planned, surplus).Should().Be(expected);

    [Fact]
    public void Check_TotalWithinPlan_ReturnsWithinPlan() =>
        CuttingLayCalculator.Check(150, 100, 50, 3).Should().Be(CutCapCheck.WithinPlan);

    [Fact]
    public void Check_TotalOverPlanButWithinSurplus_ReturnsWarning() =>
        CuttingLayCalculator.Check(150, 100, 54, 3).Should().Be(CutCapCheck.OverPlanWithinSurplus);

    [Fact]
    public void Check_TotalOverCap_ReturnsOverCap() =>
        CuttingLayCalculator.Check(150, 100, 55, 3).Should().Be(CutCapCheck.OverCap);
}
