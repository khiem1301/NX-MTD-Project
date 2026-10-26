using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Thẻ kho (ghi qua usp_Stock_Post)
/// </summary>
public partial class StockTransaction
{
    public long StockTransactionId { get; set; }

    public int StockBalanceId { get; set; }

    public string SourceDocType { get; set; } = null!;

    public int SourceDocId { get; set; }

    public int? SourceLineId { get; set; }

    public decimal QtyChange { get; set; }

    public decimal BalanceAfter { get; set; }

    public DateOnly TransactionDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual StockBalance StockBalance { get; set; } = null!;
}
