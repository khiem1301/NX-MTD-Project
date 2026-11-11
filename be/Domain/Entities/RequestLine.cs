using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng chi tiết đơn từ
/// </summary>
public partial class RequestLine
{
    public int RequestLineId { get; set; }

    public int RequestId { get; set; }

    public int? MaterialId { get; set; }

    public int? OrderLineId { get; set; }

    public int? ProductComponentMaterialId { get; set; }

    public string? ItemDescription { get; set; }

    public decimal Quantity { get; set; }

    public decimal? ReplenishQty { get; set; }

    public int? MaterialReceiptLineId { get; set; }

    /// <summary>SHORT_DELIVERY: dòng đơn mua còn thiếu (kể cả dòng chưa về lần nào — DEV-ALL-95).</summary>
    public int? PurchaseOrderLineId { get; set; }

    /// <summary>SHORT_DELIVERY: NVL khách cấp còn thiếu (kể cả NVL chưa về lần nào — DEV-ALL-95).</summary>
    public int? OrderMaterialId { get; set; }

    public virtual Material? Material { get; set; }

    public virtual MaterialReceiptLine? MaterialReceiptLine { get; set; }

    public virtual OrderLine? OrderLine { get; set; }

    public virtual ProductComponentMaterial? ProductComponentMaterial { get; set; }

    public virtual Request Request { get; set; } = null!;
}
