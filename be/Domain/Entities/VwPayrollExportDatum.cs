using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwPayrollExportDatum
{
    public string PeriodMonth { get; set; } = null!;

    public int EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public string PayType { get; set; } = null!;

    public string? TeamName { get; set; }

    public decimal WorkDays { get; set; }

    public decimal SundayWorkDays { get; set; }

    public decimal OvertimeHours { get; set; }

    public decimal SundayOvertimeHours { get; set; }

    public decimal PieceRateAmount { get; set; }

    public decimal SundayPieceRateBonus { get; set; }

    public decimal ScrapDeduction { get; set; }

    public string? GradeCode { get; set; }

    public decimal? GradeAllowance { get; set; }
}
