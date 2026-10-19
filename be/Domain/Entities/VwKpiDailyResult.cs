using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwKpiDailyResult
{
    public int KpiTargetId { get; set; }

    public int SewingTeamId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public DateOnly WorkDate { get; set; }

    public decimal? DailyTarget { get; set; }

    public int? FinishedQty { get; set; }

    public int DefectQty { get; set; }

    public decimal? DefectRatePct { get; set; }

    public decimal? PercentAchieved { get; set; }
}
