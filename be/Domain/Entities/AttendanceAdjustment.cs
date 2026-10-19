using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Chấm hộ của Kế toán
/// </summary>
public partial class AttendanceAdjustment
{
    public int AttendanceAdjustmentId { get; set; }

    public int EmployeeId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string AdjustmentType { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public int? RequestId { get; set; }

    public TimeOnly? CheckIn { get; set; }

    public TimeOnly? CheckOut { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual ICollection<DailyTimesheet> DailyTimesheets { get; set; } = new List<DailyTimesheet>();

    public virtual Employee Employee { get; set; } = null!;

    public virtual Request? Request { get; set; }
}
