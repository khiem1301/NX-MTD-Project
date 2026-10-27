using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// NVL của đơn: cần bao nhiêu, khách cấp bao nhiêu, phải mua bao nhiêu
/// </summary>
public partial class OrderMaterial
{
    public int OrderMaterialId { get; set; }

    public int OrderId { get; set; }

    public int MaterialId { get; set; }

    public string MaterialColor { get; set; } = null!;

    public decimal RequiredQty { get; set; }

    public decimal CustomerQty { get; set; }

    public short? ExpectedDeliveryCount { get; set; }

    public decimal? PurchaseQty { get; set; }

    public string? Source { get; set; }

    public string? Note { get; set; }

    public virtual Material Material { get; set; } = null!;

    public virtual ICollection<MaterialReceiptLine> MaterialReceiptLines { get; set; } = new List<MaterialReceiptLine>();

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();
}
