using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phiếu trả vải thừa
/// </summary>
public partial class MaterialReturn
{
    public int MaterialReturnId { get; set; }

    public string ReturnCode { get; set; } = null!;

    public int MaterialIssueId { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public int? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string? RejectReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee? ConfirmedByNavigation { get; set; }

    public virtual MaterialIssue MaterialIssue { get; set; } = null!;

    public virtual ICollection<MaterialReturnLine> MaterialReturnLines { get; set; } = new List<MaterialReturnLine>();
}
