using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Lệnh cắt
/// </summary>
public partial class WorkOrder
{
    public int WorkOrderId { get; set; }

    public string WorkOrderCode { get; set; } = null!;

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int CuttingTeamId { get; set; }

    public int? SourceRequestId { get; set; }

    public DateOnly DueDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<CutHandover> CutHandovers { get; set; } = new List<CutHandover>();

    public virtual ICollection<CuttingLay> CuttingLays { get; set; } = new List<CuttingLay>();

    public virtual Team CuttingTeam { get; set; } = null!;

    public virtual ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual Request? SourceRequest { get; set; }

    public virtual ICollection<WorkOrderLine> WorkOrderLines { get; set; } = new List<WorkOrderLine>();
}
