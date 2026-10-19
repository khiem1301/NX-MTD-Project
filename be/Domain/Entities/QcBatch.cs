using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Đợt kiểm hoàn thiện
/// </summary>
public partial class QcBatch
{
    public int QcBatchId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string ShiftCode { get; set; } = null!;

    public TimeOnly BatchTime { get; set; }

    public int SewingTeamId { get; set; }

    public int OrderLineId { get; set; }

    public int FinishedQty { get; set; }

    public int InspectorId { get; set; }

    public DateTime EditDeadline { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual Employee Inspector { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual Team SewingTeam { get; set; } = null!;
}
