using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Nguyên vật liệu
/// </summary>
public partial class Material
{
    public int MaterialId { get; set; }

    public string MaterialCode { get; set; } = null!;

    public string MaterialName { get; set; } = null!;

    public string Color { get; set; } = null!;

    public string? MaterialType { get; set; }

    public string Category { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CuttingLay> CuttingLays { get; set; } = new List<CuttingLay>();

    public virtual ICollection<MaterialIssueLine> MaterialIssueLines { get; set; } = new List<MaterialIssueLine>();

    public virtual ICollection<MaterialReceiptLine> MaterialReceiptLines { get; set; } = new List<MaterialReceiptLine>();

    public virtual ICollection<OrderMaterial> OrderMaterials { get; set; } = new List<OrderMaterial>();

    public virtual ICollection<ProductComponentMaterial> ProductComponentMaterials { get; set; } = new List<ProductComponentMaterial>();

    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();

    public virtual ICollection<RequestLine> RequestLines { get; set; } = new List<RequestLine>();

    public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();
}
