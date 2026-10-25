using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Thẻ kho (tồn tính ở vw_StockBalance)
/// </summary>
public partial class StockTransaction
{
    public long StockTransactionId { get; set; }

    public string WarehouseType { get; set; } = null!;

    public int? MaterialId { get; set; }

    public string? MaterialColor { get; set; }

    public int? OrderLineId { get; set; }

    public int? ProductComponentId { get; set; }

    public string SourceDocType { get; set; } = null!;

    public int SourceDocId { get; set; }

    public int? SourceLineId { get; set; }

    public decimal QtyChange { get; set; }

    public decimal BalanceAfter { get; set; }

    public DateOnly TransactionDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Material? Material { get; set; }

    public virtual OrderLine? OrderLine { get; set; }

    public virtual ProductComponent? ProductComponent { get; set; }
}
