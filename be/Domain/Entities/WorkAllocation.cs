using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Giao / thu hồi việc với công nhân
/// </summary>
public partial class WorkAllocation
{
    public int WorkAllocationId { get; set; }

    public string MovementType { get; set; } = null!;

    public int SewingTeamId { get; set; }

    public int WorkerId { get; set; }

    public int OrderLineId { get; set; }

    public int OperationId { get; set; }

    public int Quantity { get; set; }

    public int PieceQty { get; set; }

    public int? ConfirmedQty { get; set; }

    public int? ConfirmedPieceQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public string? RetrievalReason { get; set; }

    public string ConfirmMode { get; set; } = null!;

    public string? OnBehalfReason { get; set; }

    public string Status { get; set; } = null!;

    public string? RejectReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public virtual Operation Operation { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual Team SewingTeam { get; set; } = null!;

    public virtual Employee Worker { get; set; } = null!;
}
