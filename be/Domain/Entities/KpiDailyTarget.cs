using System;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Chỉ tiêu KPI từng ngày (Quản đốc nhập riêng từng ngày, không chia đều cả kỳ)
/// </summary>
public partial class KpiDailyTarget
{
    public int KpiDailyTargetId { get; set; }

    public int KpiTargetId { get; set; }

    public DateOnly WorkDate { get; set; }

    public int TargetQty { get; set; }

    public virtual KpiTarget KpiTarget { get; set; } = null!;
}
