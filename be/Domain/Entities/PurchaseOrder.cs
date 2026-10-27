using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Đơn mua NVL kèm thanh toán (OrderId NULL = đơn mua dùng chung)
/// </summary>
public partial class PurchaseOrder
{
    public int PurchaseOrderId { get; set; }

    public string PoCode { get; set; } = null!;

    public int? OrderId { get; set; }

    public int? SupplierId { get; set; }

    public int? SourceRequestId { get; set; }

    public DateOnly? OrderDate { get; set; }

    public short? ExpectedDeliveryCount { get; set; }

    public string GoodsStatus { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public string? RejectReason { get; set; }

    public string? PaymentReturnReason { get; set; }

    public DateOnly? PaymentDate { get; set; }

    public decimal? PaidAmount { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentNote { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual ICollection<MaterialReceipt> MaterialReceipts { get; set; } = new List<MaterialReceipt>();

    public virtual CustomerOrder? Order { get; set; }

    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();

    public virtual Request? SourceRequest { get; set; }

    public virtual Supplier? Supplier { get; set; }
}
