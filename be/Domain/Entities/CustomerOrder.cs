using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Đơn hàng gia công
/// </summary>
public partial class CustomerOrder
{
    public int OrderId { get; set; }

    public string OrderCode { get; set; } = null!;

    public int CustomerId { get; set; }

    public DateOnly ReceivedDate { get; set; }

    public DateOnly DueDate { get; set; }

    public string MaterialStatus { get; set; } = null!;

    public string Status { get; set; } = null!;

    public bool IsDelayRisk { get; set; }

    public DateOnly? DelayRiskSince { get; set; }

    public string? StopReason { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<DispatchNote> DispatchNotes { get; set; } = new List<DispatchNote>();

    public virtual ICollection<FgHandover> FgHandovers { get; set; } = new List<FgHandover>();

    public virtual ICollection<KpiTarget> KpiTargets { get; set; } = new List<KpiTarget>();

    public virtual ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();

    public virtual ICollection<MaterialReceipt> MaterialReceipts { get; set; } = new List<MaterialReceipt>();

    public virtual ICollection<OperationAssignment> OperationAssignments { get; set; } = new List<OperationAssignment>();

    public virtual ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

    public virtual ICollection<OrderMaterial> OrderMaterials { get; set; } = new List<OrderMaterial>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
