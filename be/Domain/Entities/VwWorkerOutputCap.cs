using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwWorkerOutputCap
{
    public int WorkerId { get; set; }

    public int OrderLineId { get; set; }

    public int OperationId { get; set; }

    public int? CapQty { get; set; }

    public int OutputQty { get; set; }

    public int ScrapQty { get; set; }

    public int? RemainingQty { get; set; }
}
