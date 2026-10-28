namespace NX_MTD.Domain.Constants;

/// <summary>
/// Trạng thái chỉ tiêu KPI tổ may (KpiTarget.Status) — Quản đốc lập/sửa/hủy; EndOfOrderService
/// chốt và đóng khi kết thúc đơn của tổ (Minh).
/// </summary>
public static class KpiStatuses
{
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>
/// Kết quả KPI khi đóng kỳ (KpiTarget.Result) — so Σ hoàn thiện cả kỳ (vw_KpiDailyResult)
/// với TargetFinishedQty.
/// </summary>
public static class KpiResults
{
    public const string Achieved = "ACHIEVED";
    public const string NotAchieved = "NOT_ACHIEVED";
}
