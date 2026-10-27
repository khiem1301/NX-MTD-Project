using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Hàng lỗi Rework/Scrap và người gây lỗi
/// </summary>
public partial class DefectRecord
{
    public int DefectRecordId { get; set; }

    public string DefectCode { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public string? ShiftCode { get; set; }

    public int SewingTeamId { get; set; }

    public int OrderLineId { get; set; }

    public int OperationId { get; set; }

    public int ResponsibleWorkerId { get; set; }

    public string DefectType { get; set; } = null!;

    public int Quantity { get; set; }

    public string DefectReasonCode { get; set; } = null!;

    public string? ReasonNote { get; set; }

    public int? PieceRateId { get; set; }

    public DateTime? WorkerNotifiedAt { get; set; }

    public string? WorkerObjection { get; set; }

    public DateTime? ObjectedAt { get; set; }

    public DateTime EditDeadline { get; set; }

    public string Status { get; set; } = null!;

    public int RecordedBy { get; set; }

    public DateTime RecordedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual Operation Operation { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual PieceRate? PieceRate { get; set; }

    public virtual Employee RecordedByNavigation { get; set; } = null!;

    public virtual Request? Request { get; set; }

    public virtual Employee ResponsibleWorker { get; set; } = null!;

    public virtual Team SewingTeam { get; set; } = null!;
}
