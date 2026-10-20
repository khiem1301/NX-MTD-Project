using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng đơn mua NVL
/// </summary>
public partial class PurchaseOrderLine
{
    public int PurchaseOrderLineId { get; set; }

    public int PurchaseOrderId { get; set; }

    /// <summary>NULL khi dòng thuộc đơn mua dự trữ; khi đó dùng MaterialId + MaterialColor.</summary>
    public int? OrderMaterialId { get; set; }

    public int? MaterialId { get; set; }

    public string? MaterialColor { get; set; }

    public decimal OrderedQty { get; set; }

    public decimal NotReplenishedQty { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? Amount { get; set; }

    public virtual ICollection<MaterialReceiptLine> MaterialReceiptLines { get; set; } = new List<MaterialReceiptLine>();

    public virtual Material? Material { get; set; }

    public virtual OrderMaterial? OrderMaterial { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
}
