using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Bàn giao BTP vào kho
/// </summary>
public partial class CutHandover
{
    public int CutHandoverId { get; set; }

    public string HandoverCode { get; set; } = null!;

    public int WorkOrderId { get; set; }

    public DateTime HandedAt { get; set; }

    public string Status { get; set; } = null!;

    public int? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string? RejectReason { get; set; }

    public virtual Employee? ConfirmedByNavigation { get; set; }

    public virtual ICollection<CutHandoverLine> CutHandoverLines { get; set; } = new List<CutHandoverLine>();

    public virtual WorkOrder WorkOrder { get; set; } = null!;
}
