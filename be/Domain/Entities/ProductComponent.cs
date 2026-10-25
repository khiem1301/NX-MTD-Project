using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Thành phần của mã hàng: chi tiết vải (PANEL) và phụ liệu (TRIM)
/// </summary>
public partial class ProductComponent
{
    public int ProductComponentId { get; set; }

    public int ProductId { get; set; }

    public string ComponentName { get; set; } = null!;

    public string ComponentType { get; set; } = null!;

    public short PiecesPerGarment { get; set; }

    public short SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<CutHandoverLine> CutHandoverLines { get; set; } = new List<CutHandoverLine>();

    public virtual ICollection<CuttingLayLine> CuttingLayLines { get; set; } = new List<CuttingLayLine>();

    public virtual ICollection<MaterialIssueLine> MaterialIssueLines { get; set; } = new List<MaterialIssueLine>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductComponentMaterial> ProductComponentMaterials { get; set; } = new List<ProductComponentMaterial>();

    public virtual ICollection<RequestLine> RequestLines { get; set; } = new List<RequestLine>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<StocktakeLine> StocktakeLines { get; set; } = new List<StocktakeLine>();

    public virtual ICollection<WorkOrderLine> WorkOrderLines { get; set; } = new List<WorkOrderLine>();
}
