using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Bảng công ngày
/// </summary>
public partial class DailyTimesheet
{
    public int DailyTimesheetId { get; set; }

    public int EmployeeId { get; set; }

    public DateOnly WorkDate { get; set; }

    public DateTime? CheckIn { get; set; }

    public DateTime? CheckOut { get; set; }

    public decimal WorkedHours { get; set; }

    public decimal WorkdayValue { get; set; }

    public decimal OvertimeHours { get; set; }

    public bool IsMissingPunch { get; set; }

    public bool HasExtraPunch { get; set; }

    public bool? IsSunday { get; set; }

    public int? AttendanceAdjustmentId { get; set; }

    public string? Source { get; set; }

    public string? Note { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual AttendanceAdjustment? AttendanceAdjustment { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
