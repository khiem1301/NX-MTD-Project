using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwOrderLineProgress
{
    public int OrderLineId { get; set; }

    public int OrderId { get; set; }

    public string OrderCode { get; set; } = null!;

    public int ProductId { get; set; }

    public string ProductCode { get; set; } = null!;

    public int ProductVariantId { get; set; }

    public string Color { get; set; } = null!;

    public string SizeCode { get; set; } = null!;

    public int OrderedQty { get; set; }

    public int CutQty { get; set; }

    public int CutHandedToWarehouseQty { get; set; }

    public int IssuedToSewingQty { get; set; }

    public int FinishedQty { get; set; }

    public int FinishedReceivedQty { get; set; }

    public int DeliveredQty { get; set; }

    public int ReworkQty { get; set; }

    public int ScrapQty { get; set; }

    public decimal? DefectRatePct { get; set; }
}
