using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng phiếu nhập NVL
/// </summary>
public partial class MaterialReceiptLine
{
    public int MaterialReceiptLineId { get; set; }

    public int MaterialReceiptId { get; set; }

    public int OrderMaterialId { get; set; }

    public int? PurchaseOrderLineId { get; set; }

    public int? OriginalReceiptLineId { get; set; }

    public decimal? ToReplenishQty { get; set; }

    public decimal AcceptedQty { get; set; }

    public decimal ShortQty { get; set; }

    public decimal DefectiveQty { get; set; }

    public virtual ICollection<MaterialReceiptLine> InverseOriginalReceiptLine { get; set; } = new List<MaterialReceiptLine>();

    public virtual MaterialReceipt MaterialReceipt { get; set; } = null!;

    public virtual OrderMaterial OrderMaterial { get; set; } = null!;

    public virtual MaterialReceiptLine? OriginalReceiptLine { get; set; }

    public virtual PurchaseOrderLine? PurchaseOrderLine { get; set; }

    public virtual ICollection<RequestLine> RequestLines { get; set; } = new List<RequestLine>();
}
