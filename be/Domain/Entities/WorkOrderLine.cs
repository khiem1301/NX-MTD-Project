using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng lệnh cắt
/// </summary>
public partial class WorkOrderLine
{
    public int WorkOrderLineId { get; set; }

    public int WorkOrderId { get; set; }

    public int OrderLineId { get; set; }

    public int? ProductComponentId { get; set; }

    public int PlannedQty { get; set; }

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual ProductComponent? ProductComponent { get; set; }

    public virtual WorkOrder WorkOrder { get; set; } = null!;
}
