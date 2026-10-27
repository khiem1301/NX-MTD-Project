using FluentAssertions;
using NX_MTD.Application.Services;

namespace Application.Tests;

public class FabricRequirementCalculatorTests
{
    private const int Product = 1;

    private static NormLine Norm(string component, decimal perPiece, string? color = null, string? size = null,
        decimal wastage = 0, int orderMaterialId = 10, int productId = Product) =>
        new(orderMaterialId, 100 + orderMaterialId, "Đỏ", productId, component, color, size, perPiece, wastage);

    private static PieceDemand Demand(int qty, string color = "Đỏ", string size = "M", string panel = "") =>
        new(Product, color, size, panel, qty);

    [Fact]
    public void Calculate_GenericNorm_MultipliesQtyAndAddsWastage()
    {
        var needs = FabricRequirementCalculator.Calculate(new[] { Norm("Thân", 1.2m, wastage: 5) }, new[] { Demand(100) });

        needs.Should().ContainSingle().Which.Qty.Should().Be(126m);
    }

    [Fact]
    public void Calculate_SpecificSizeNorm_OverridesGenericForSameComponent()
    {
        var norms = new[] { Norm("Thân", 1.0m), Norm("Thân", 1.5m, size: "XL") };

        var needs = FabricRequirementCalculator.Calculate(norms, new[] { Demand(10, size: "XL"), Demand(10, size: "M") });

        needs.Single().Qty.Should().Be(25m);
    }

    [Fact]
    public void Calculate_ColorAndSizeNorm_IsMostSpecific()
    {
        var norms = new[]
        {
            Norm("Thân", 1m), Norm("Thân", 2m, color: "Đỏ"), Norm("Thân", 3m, size: "M"), Norm("Thân", 4m, "đỏ", "m"),
        };

        FabricRequirementCalculator.Calculate(norms, new[] { Demand(1) }).Single().Qty.Should().Be(4m);
    }

    [Fact]
    public void Calculate_DifferentComponents_AreSummed()
    {
        var norms = new[] { Norm("Thân", 1m), Norm("Tay", 0.5m) };

        FabricRequirementCalculator.Calculate(norms, new[] { Demand(10) }).Single().Qty.Should().Be(15m);
    }

    [Fact]
    public void Calculate_PanelDemand_OnlyCountsThatComponent()
    {
        var norms = new[] { Norm("Thân", 1m), Norm("Tay", 0.5m) };

        FabricRequirementCalculator.Calculate(norms, new[] { Demand(4, panel: "Tay") }).Single().Qty.Should().Be(2m);
    }

    [Fact]
    public void Calculate_NormOfOtherColorOrProduct_IsIgnored()
    {
        var norms = new[] { Norm("Thân", 1m, color: "Xanh"), Norm("Thân", 9m, productId: 2) };

        FabricRequirementCalculator.Calculate(norms, new[] { Demand(10) }).Should().BeEmpty();
    }

    [Fact]
    public void FindUncovered_LineWithoutMatchingNorm_IsReturned()
    {
        var norms = new[] { Norm("Thân", 1m, color: "Đỏ") };

        var uncovered = FabricRequirementCalculator.FindUncovered(norms,
            new[] { Demand(10, color: "Đỏ"), Demand(5, color: "Xanh") });

        uncovered.Should().ContainSingle().Which.Color.Should().Be("Xanh");
    }

    [Fact]
    public void FindUncovered_GenericNorm_CoversEveryLine_AndZeroQtyIsIgnored()
    {
        var norms = new[] { Norm("Thân", 1m) };

        FabricRequirementCalculator.FindUncovered(norms,
            new[] { Demand(10, color: "Đỏ"), Demand(5, color: "Xanh", size: "L"), Demand(0, color: "Tím") }).Should().BeEmpty();
    }

    [Fact]
    public void Calculate_RoundsToThreeDecimals()
    {
        var needs = FabricRequirementCalculator.Calculate(new[] { Norm("Thân", 0.3333m, wastage: 3) }, new[] { Demand(7) });

        needs.Single().Qty.Should().Be(2.403m);
    }
}
