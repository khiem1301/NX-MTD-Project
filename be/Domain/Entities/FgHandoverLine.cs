using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng bàn giao thành phẩm
/// </summary>
public partial class FgHandoverLine
{
    public int FgHandoverLineId { get; set; }

    public int FgHandoverId { get; set; }

    public int OrderLineId { get; set; }

    public int HandedQty { get; set; }

    public int? ConfirmedQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public virtual FgHandover FgHandover { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;
}
