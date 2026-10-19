using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng phiếu trả vải thừa
/// </summary>
public partial class MaterialReturnLine
{
    public int MaterialReturnLineId { get; set; }

    public int MaterialReturnId { get; set; }

    public int MaterialIssueLineId { get; set; }

    public decimal ReturnedQty { get; set; }

    public decimal? ConfirmedQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public virtual MaterialIssueLine MaterialIssueLine { get; set; } = null!;

    public virtual MaterialReturn MaterialReturn { get; set; } = null!;
}
