using FluentAssertions;
using NX_MTD.Application.Services;

namespace Application.Tests;

/// <summary>Định mức theo biến thể (ProductComponentMaterial): cùng công thức với vw_OrderMaterialNorm / vw_WorkOrderFabricNeed.</summary>
public class FabricRequirementCalculatorTests
{
    private const int Variant = 11;
    private const int Body = 1, Sleeve = 2;

    private static NormLine Norm(int component, decimal perGarment, decimal wastage = 0, int material = 100,
        string materialColor = "Đỏ", short pieces = 1, int variant = Variant) =>
        new(variant, component, pieces, material, materialColor, perGarment, wastage);

    private static PieceDemand Demand(int qty, int? component = null, int variant = Variant) => new(variant, component, qty);

    [Fact]
    public void Calculate_WholeGarment_MultipliesQtyAndAddsWastage()
    {
        var needs = FabricRequirementCalculator.Calculate(new[] { Norm(Body, 1.2m, wastage: 5) }, new[] { Demand(100) });

        needs.Should().ContainSingle().Which.Qty.Should().Be(126m);
    }

    [Fact]
    public void Calculate_DifferentComponentsOfSameMaterial_AreSummed()
    {
        var norms = new[] { Norm(Body, 1m), Norm(Sleeve, 0.5m, pieces: 2) };

        FabricRequirementCalculator.Calculate(norms, new[] { Demand(10) }).Single().Qty.Should().Be(15m);
    }

    [Fact]
    public void Calculate_DifferentMaterialsOrColors_AreKeptApart()
    {
        var norms = new[] { Norm(Body, 1m), Norm(Body, 2m, material: 101), Norm(Sleeve, 3m, materialColor: "Xanh") };

        var needs = FabricRequirementCalculator.Calculate(norms, new[] { Demand(1) });

        needs.Should().HaveCount(3);
        needs.Single(n => n.MaterialId == 101).Qty.Should().Be(2m);
        needs.Single(n => n.MaterialColor == "Xanh").Qty.Should().Be(3m);
    }

    [Fact]
    public void Calculate_ReplacementOfOneComponent_CountsOnlyThatComponentShare()
    {
        // Tay áo: 2 chi tiết / sản phẩm, định mức 0,5 cho cả 2 tay → cắt bù 4 chiếc tay áo cần 4 × 0,5 / 2 = 1.
        var norms = new[] { Norm(Body, 1m), Norm(Sleeve, 0.5m, pieces: 2) };

        FabricRequirementCalculator.Calculate(norms, new[] { Demand(4, component: Sleeve) }).Single().Qty.Should().Be(1m);
    }

    [Fact]
    public void Calculate_NormOfOtherVariant_IsIgnored()
    {
        var norms = new[] { Norm(Body, 1m, variant: 99) };

        FabricRequirementCalculator.Calculate(norms, new[] { Demand(10) }).Should().BeEmpty();
    }

    [Fact]
    public void Calculate_AddsDemandsOfSeveralVariants()
    {
        var norms = new[] { Norm(Body, 1m), Norm(Body, 2m, variant: 12) };

        var needs = FabricRequirementCalculator.Calculate(norms, new[] { Demand(10), Demand(5, variant: 12) });

        needs.Single().Qty.Should().Be(20m);
    }

    [Fact]
    public void Calculate_RoundsToThreeDecimals()
    {
        var needs = FabricRequirementCalculator.Calculate(new[] { Norm(Body, 0.3333m, wastage: 3) }, new[] { Demand(7) });

        needs.Single().Qty.Should().Be(2.403m);
    }
}
