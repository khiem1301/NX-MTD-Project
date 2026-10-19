using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng phiếu xuất kho
/// </summary>
public partial class DispatchNoteLine
{
    public int DispatchNoteLineId { get; set; }

    public int DispatchNoteId { get; set; }

    public int OrderLineId { get; set; }

    public int IssueQty { get; set; }

    public int? ReceivedQty { get; set; }

    public virtual DispatchNote DispatchNote { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;
}
