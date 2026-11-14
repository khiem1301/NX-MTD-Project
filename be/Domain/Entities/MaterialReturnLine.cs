using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng phiếu trả: NVL / phế phẩm (MaterialId) hoặc BTP cả bộ của một dòng đơn (OrderLineId), đúng một trong hai
/// </summary>
public partial class MaterialReturnLine
{
    public int MaterialReturnLineId { get; set; }

    public int MaterialReturnId { get; set; }

    /// <summary>Chỉ có ở dòng của phiếu trả cũ.</summary>
    public int? MaterialIssueLineId { get; set; }

    public int? MaterialId { get; set; }

    public int? OrderLineId { get; set; }

    public decimal ReturnedQty { get; set; }

    public decimal? ConfirmedQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public virtual MaterialIssueLine? MaterialIssueLine { get; set; }

    public virtual Material? Material { get; set; }

    public virtual OrderLine? OrderLine { get; set; }

    public virtual MaterialReturn MaterialReturn { get; set; } = null!;
}
