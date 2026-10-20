namespace NX_MTD.Application.Services;

public enum CutCapCheck
{
    WithinPlan,
    OverPlanWithinSurplus,
    OverCap,
}

/// <summary>UC51: số cắt = số lớp × tỉ lệ size; tổng cắt ≤ kế hoạch + % dư (CUT_SURPLUS_PERCENT).</summary>
public static class CuttingLayCalculator
{
    public static int CalculatedQty(int plyCount, int ratioValue) => plyCount * ratioValue;

    /// <summary>Trần làm tròn xuống: kế hoạch 150, dư 3% → 154.</summary>
    public static int Cap(int plannedQty, decimal surplusPercent) =>
        (int)Math.Floor(plannedQty * (1 + surplusPercent / 100m));

    public static CutCapCheck Check(int plannedQty, int alreadyCutQty, int newQty, decimal surplusPercent)
    {
        var total = alreadyCutQty + newQty;
        if (total > Cap(plannedQty, surplusPercent)) return CutCapCheck.OverCap;
        return total > plannedQty ? CutCapCheck.OverPlanWithinSurplus : CutCapCheck.WithinPlan;
    }
}
