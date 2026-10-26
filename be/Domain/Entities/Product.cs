using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Mã hàng
/// </summary>
public partial class Product
{
    public int ProductId { get; set; }

    public string ProductCode { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public string? SampleImageUrl { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<BreakEvenResult> BreakEvenResults { get; set; } = new List<BreakEvenResult>();

    public virtual ICollection<FgHandover> FgHandovers { get; set; } = new List<FgHandover>();

    public virtual ICollection<KpiTarget> KpiTargets { get; set; } = new List<KpiTarget>();

    public virtual ICollection<MonthlyCost> MonthlyCosts { get; set; } = new List<MonthlyCost>();

    public virtual Operation? Operation { get; set; }

    public virtual ICollection<OperationGroup> OperationGroups { get; set; } = new List<OperationGroup>();

    public virtual ICollection<ProductComponent> ProductComponents { get; set; } = new List<ProductComponent>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
