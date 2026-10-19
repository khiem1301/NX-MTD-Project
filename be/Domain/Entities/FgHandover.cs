using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Bàn giao thành phẩm
/// </summary>
public partial class FgHandover
{
    public int FgHandoverId { get; set; }

    public string HandoverCode { get; set; } = null!;

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int InspectorId { get; set; }

    public DateTime HandedAt { get; set; }

    public string Status { get; set; } = null!;

    public int? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string? RejectReason { get; set; }

    public virtual Employee? ConfirmedByNavigation { get; set; }

    public virtual ICollection<FgHandoverLine> FgHandoverLines { get; set; } = new List<FgHandoverLine>();

    public virtual Employee Inspector { get; set; } = null!;

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
