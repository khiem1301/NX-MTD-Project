using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwWorkOrderFabricNeed
{
    public int WorkOrderId { get; set; }

    public int MaterialId { get; set; }

    public string MaterialColor { get; set; } = null!;

    public decimal? NeedQty { get; set; }
}
