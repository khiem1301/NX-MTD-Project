using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// KPI tổ may
/// </summary>
public partial class KpiTarget
{
    public int KpiTargetId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int SewingTeamId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int TargetFinishedQty { get; set; }

    public short WorkingDays { get; set; }

    public string Status { get; set; } = null!;

    public string? Result { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<KpiDailyTarget> DailyTargets { get; set; } = new List<KpiDailyTarget>();

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual Team SewingTeam { get; set; } = null!;
}
