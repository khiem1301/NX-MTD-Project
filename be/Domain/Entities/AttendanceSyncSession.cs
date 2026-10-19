using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Lần đồng bộ RJ / import Excel chấm công
/// </summary>
public partial class AttendanceSyncSession
{
    public int SyncSessionId { get; set; }

    public string Source { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public int? LastSourceRecordId { get; set; }

    public int RecordsReceived { get; set; }

    public int RecordsInserted { get; set; }

    public string Status { get; set; } = null!;

    public string? ErrorMessage { get; set; }

    public string? FileName { get; set; }

    public virtual ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
}
