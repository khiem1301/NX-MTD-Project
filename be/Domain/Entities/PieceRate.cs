using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Đơn giá công đoạn
/// </summary>
public partial class PieceRate
{
    public int PieceRateId { get; set; }

    public int OperationId { get; set; }

    public decimal Rate { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? DecisionNote { get; set; }

    public virtual ICollection<DefectRecord> DefectRecords { get; set; } = new List<DefectRecord>();

    public virtual Operation Operation { get; set; } = null!;

    public virtual ICollection<OutputRecord> OutputRecords { get; set; } = new List<OutputRecord>();
}
