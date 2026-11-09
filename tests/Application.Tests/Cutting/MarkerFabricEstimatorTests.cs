using FluentAssertions;
using NX_MTD.Application.Services;

namespace Application.Tests;

/// <summary>Ước mét vải theo bìa của đơn (DEV-ALL-85): số lớp, chọn bìa, số dư, tối thiểu một lớp khi cắt lẻ.</summary>
public class MarkerFabricEstimatorTests
{
    private static MarkerOption Marker(string code, decimal length, params (string Size, short Pieces)[] ratio) =>
        new(code, length, ratio.ToDictionary(r => r.Size, r => r.Pieces, StringComparer.OrdinalIgnoreCase));

    private static Dictionary<string, int> Need(params (string Size, int Qty)[] need) =>
        need.ToDictionary(n => n.Size, n => n.Qty, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Whole_MatchingRatio_LayersEqualTheSetsNeeded()
    {
        var choice = MarkerFabricEstimator.ChooseWhole(Need(("S", 40), ("M", 80), ("L", 40)), [Marker("1A", 2.2m, ("S", 1), ("M", 2), ("L", 1))]);

        choice.Should().Be(new MarkerChoice("1A", 40, 88m, 0));
    }

    [Fact]
    public void Whole_OffRatio_TakesTheLargestNumberOfLayersAndCountsTheSurplus()
    {
        var choice = MarkerFabricEstimator.ChooseWhole(Need(("M", 30)), [Marker("2A", 5m, ("S", 1), ("M", 2), ("L", 1))]);

        choice.Should().Be(new MarkerChoice("2A", 15, 75m, 30));   // dư S15, L15
    }

    [Fact]
    public void Whole_PicksFewestMeters_ThenLeastSurplus_ThenCode()
    {
        MarkerFabricEstimator.ChooseWhole(Need(("M", 30)), [Marker("1A", 8m, ("S", 1), ("M", 2), ("L", 1)), Marker("1B", 7.5m, ("M", 4))])!
            .Code.Should().Be("1B");
        MarkerFabricEstimator.ChooseWhole(Need(("M", 4)), [Marker("1B", 2m, ("M", 4)), Marker("1A", 2m, ("M", 4))])!
            .Code.Should().Be("1A");
        MarkerFabricEstimator.ChooseWhole(Need(("M", 4)), [Marker("1A", 2m, ("M", 4), ("L", 1)), Marker("1B", 2m, ("M", 4))])!
            .Code.Should().Be("1B");
    }

    [Fact]
    public void Whole_NoMarkerCoversEverySize_ReturnsNull()
    {
        MarkerFabricEstimator.ChooseWhole(Need(("S", 10), ("M", 10)), [Marker("1B", 2m, ("M", 4))]).Should().BeNull();
        MarkerFabricEstimator.ChooseWhole(Need(("M", 0)), [Marker("1B", 2m, ("M", 4))]).Should().BeNull();
    }

    [Fact]
    public void Whole_SizeCase_DoesNotMatter()
    {
        MarkerFabricEstimator.ChooseWhole(Need(("m", 4)), [Marker("1A", 1m, ("M", 2))])!.Plies.Should().Be(2);
    }

    [Theory]
    [InlineData(3, 1, 5.0)]    // 3 tay ÷ 4 tay / lớp → 1 lớp
    [InlineData(4, 1, 5.0)]
    [InlineData(5, 2, 10.0)]
    [InlineData(1, 1, 5.0)]    // tối thiểu một chiều dài bìa
    public void Extra_PiecesPerPlyIsGarmentsTimesPiecesPerGarment(int pieces, int plies, double meters)
    {
        var choice = MarkerFabricEstimator.ChooseExtra([new PieceNeed("M", pieces, 2)], [Marker("2A", 5m, ("S", 1), ("M", 2), ("L", 1))]);

        (choice!.Plies, choice.Meters).Should().Be((plies, (decimal)meters));
    }

    [Fact]
    public void Extra_SizeNotOnTheMarker_ReturnsNull() =>
        MarkerFabricEstimator.ChooseExtra([new PieceNeed("XL", 3, 1)], [Marker("2A", 5m, ("M", 2))]).Should().BeNull();
}
