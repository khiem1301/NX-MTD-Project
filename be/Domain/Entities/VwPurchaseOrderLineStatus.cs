using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwPurchaseOrderLineStatus
{
    public int PurchaseOrderLineId { get; set; }

    public int PurchaseOrderId { get; set; }

    public int OrderMaterialId { get; set; }

    public decimal OrderedQty { get; set; }

    public decimal ReceivedQty { get; set; }

    public decimal NotReplenishedQty { get; set; }

    public decimal? OutstandingQty { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? Amount { get; set; }
}
