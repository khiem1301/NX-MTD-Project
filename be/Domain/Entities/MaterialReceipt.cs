using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phiếu nhập NVL (mới / bổ sung)
/// </summary>
public partial class MaterialReceipt
{
    public int MaterialReceiptId { get; set; }

    public string ReceiptCode { get; set; } = null!;

    public string ReceiptType { get; set; } = null!;

    public int? OriginalReceiptId { get; set; }

    public int? ShortageRequestId { get; set; }

    /// <summary>NULL = nhập hàng của đơn mua dự trữ.</summary>
    public int? OrderId { get; set; }

    public string Source { get; set; } = null!;

    public int? PurchaseOrderId { get; set; }

    public DateOnly ReceiptDate { get; set; }

    public string Status { get; set; } = null!;

    public string? ReturnReason { get; set; }

    public string? Note { get; set; }

    public DateTime SubmittedAt { get; set; }

    public int? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual Employee? ConfirmedByNavigation { get; set; }

    public virtual ICollection<MaterialReceipt> InverseOriginalReceipt { get; set; } = new List<MaterialReceipt>();

    public virtual ICollection<MaterialReceiptLine> MaterialReceiptLines { get; set; } = new List<MaterialReceiptLine>();

    public virtual CustomerOrder? Order { get; set; }

    public virtual MaterialReceipt? OriginalReceipt { get; set; }

    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    public virtual Request? Request { get; set; }

    public virtual Request? ShortageRequest { get; set; }
}
