using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwWorkOrderLineProgress
{
    public int WorkOrderLineId { get; set; }

    public int WorkOrderId { get; set; }

    public string WorkOrderCode { get; set; } = null!;

    public int CuttingTeamId { get; set; }

    public string WorkOrderStatus { get; set; } = null!;

    public int OrderLineId { get; set; }

    public int? ProductComponentId { get; set; }

    public int PlannedQty { get; set; }

    public int CutQty { get; set; }

    public int HandedToWarehouseQty { get; set; }

    public int? RemainingQty { get; set; }
}
