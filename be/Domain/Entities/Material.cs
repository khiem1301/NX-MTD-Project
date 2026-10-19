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

    public string MaterialType { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CuttingLay> CuttingLays { get; set; } = new List<CuttingLay>();

    public virtual ICollection<MaterialIssueLine> MaterialIssueLines { get; set; } = new List<MaterialIssueLine>();

    public virtual ICollection<OrderMaterial> OrderMaterials { get; set; } = new List<OrderMaterial>();

    public virtual ICollection<RequestLine> RequestLines { get; set; } = new List<RequestLine>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<StocktakeLine> StocktakeLines { get; set; } = new List<StocktakeLine>();
}
