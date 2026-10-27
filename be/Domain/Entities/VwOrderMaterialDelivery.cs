using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwOrderMaterialDelivery
{
    public int OrderMaterialId { get; set; }

    public int OrderId { get; set; }

    public int MaterialId { get; set; }

    public string MaterialColor { get; set; } = null!;

    public decimal CustomerQty { get; set; }

    public short? ExpectedDeliveryCount { get; set; }

    public int ReceivedCount { get; set; }

    public decimal ReceivedQty { get; set; }

    public decimal? RemainingQty { get; set; }
}
