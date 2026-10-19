using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phiếu cấp kho: NVL cho tổ cắt/may hoặc BTP cho tổ may
/// </summary>
public partial class MaterialIssue
{
    public int MaterialIssueId { get; set; }

    public string IssueCode { get; set; } = null!;

    public string WarehouseType { get; set; } = null!;

    public int? RequestId { get; set; }

    public int OrderId { get; set; }

    public int? WorkOrderId { get; set; }

    public int ReceivingTeamId { get; set; }

    public DateOnly IssueDate { get; set; }

    public int IssuedBy { get; set; }

    public string Status { get; set; } = null!;

    public int? ReceivedBy { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public string? RejectReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual Employee IssuedByNavigation { get; set; } = null!;

    public virtual ICollection<MaterialIssueLine> MaterialIssueLines { get; set; } = new List<MaterialIssueLine>();

    public virtual ICollection<MaterialReturn> MaterialReturns { get; set; } = new List<MaterialReturn>();

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual Employee? ReceivedByNavigation { get; set; }

    public virtual Team ReceivingTeam { get; set; } = null!;

    public virtual Request? Request { get; set; }

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual WorkOrder? WorkOrder { get; set; }
}
