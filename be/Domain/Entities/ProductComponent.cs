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

    public string Status { get; set; } = null!;

    public virtual ICollection<OperationComponent> OperationComponents { get; set; } = new List<OperationComponent>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductComponentMaterial> ProductComponentMaterials { get; set; } = new List<ProductComponentMaterial>();
}
