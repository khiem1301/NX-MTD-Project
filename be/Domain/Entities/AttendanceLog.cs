using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Lượt quẹt vân tay (3 trường lấy từ RJ)
/// </summary>
public partial class AttendanceLog
{
    public long AttendanceLogId { get; set; }

    public int AttendanceCode { get; set; }

    public DateTime PunchTime { get; set; }

    public DateOnly? WorkDate { get; set; }

    public int? SourceRecordId { get; set; }

    public int? EmployeeId { get; set; }

    public int SyncSessionId { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual AttendanceSyncSession SyncSession { get; set; } = null!;
}
