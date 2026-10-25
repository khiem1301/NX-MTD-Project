using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng phiếu cấp kho
/// </summary>
public partial class MaterialIssueLine
{
    public int MaterialIssueLineId { get; set; }

    public int MaterialIssueId { get; set; }

    public int? MaterialId { get; set; }

    public string? MaterialColor { get; set; }

    public int? OrderLineId { get; set; }

    public int? ProductComponentId { get; set; }

    public decimal IssuedQty { get; set; }

    public decimal? ConfirmedQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public virtual Material? Material { get; set; }

    public virtual MaterialIssue MaterialIssue { get; set; } = null!;

    public virtual ICollection<MaterialReturnLine> MaterialReturnLines { get; set; } = new List<MaterialReturnLine>();

    public virtual OrderLine? OrderLine { get; set; }

    public virtual ProductComponent? ProductComponent { get; set; }
}
