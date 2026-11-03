namespace NX_MTD.Domain.Entities;

/// <summary>Chỉ tiêu KPI theo một dòng đơn (màu × size); KpiTarget.TargetFinishedQty = Σ TargetQty (DEV-ALL-62).</summary>
public partial class KpiTargetLine
{
    public int KpiTargetLineId { get; set; }

    public int KpiTargetId { get; set; }

    public int OrderLineId { get; set; }

    public int TargetQty { get; set; }

    public virtual KpiTarget KpiTarget { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;
}
