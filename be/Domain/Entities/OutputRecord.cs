using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Sản lượng công đoạn
/// </summary>
public partial class OutputRecord
{
    public long OutputRecordId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string ShiftCode { get; set; } = null!;

    public int SewingTeamId { get; set; }

    public int WorkerId { get; set; }

    public int OperationId { get; set; }

    public int OrderLineId { get; set; }

    public int Quantity { get; set; }

    public int PieceRateId { get; set; }

    public bool IsOutsideAssignment { get; set; }

    public bool NoAttendanceWarning { get; set; }

    public DateTime EditDeadline { get; set; }

    public string Status { get; set; } = null!;

    public DateTime EnteredAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual Operation Operation { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual PieceRate PieceRate { get; set; } = null!;

    public virtual Team SewingTeam { get; set; } = null!;

    public virtual Employee Worker { get; set; } = null!;
}
