using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwOperationAllocationProgress
{
    public int SewingTeamId { get; set; }

    public int? OrderLineId { get; set; }

    public int OperationId { get; set; }

    public bool UsesSemiFinished { get; set; }

    public int? RequiredQty { get; set; }

    public int AllocatedQty { get; set; }

    public int PendingQty { get; set; }

    public int? UnallocatedQty { get; set; }

    public int OutputQty { get; set; }
}
