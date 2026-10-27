using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng bàn giao BTP
/// </summary>
public partial class CutHandoverLine
{
    public int CutHandoverLineId { get; set; }

    public int CutHandoverId { get; set; }

    public int OrderLineId { get; set; }

    public int? ProductComponentMaterialId { get; set; }

    public int HandedQty { get; set; }

    public int? ConfirmedQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public virtual CutHandover CutHandover { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual ProductComponentMaterial? ProductComponentMaterial { get; set; }
}
