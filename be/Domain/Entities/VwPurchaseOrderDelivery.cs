using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwPurchaseOrderDelivery
{
    public int PurchaseOrderId { get; set; }

    public string PoCode { get; set; } = null!;

    public int? OrderId { get; set; }

    public int? SupplierId { get; set; }

    public string GoodsStatus { get; set; } = null!;

    public short? ExpectedDeliveryCount { get; set; }

    public int ReceivedCount { get; set; }

    public DateOnly? LastReceiptDate { get; set; }
}
