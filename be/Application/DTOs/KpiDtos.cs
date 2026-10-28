using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>Chỉ tiêu KPI tổ may — dùng chung cho danh sách và chi tiết (không có dòng con nên cùng một shape).
/// ActualFinishedQty = Σ hàng hoàn thiện cả kỳ (vw_KpiDailyResult), PercentAchieved = thực tế / chỉ tiêu cả kỳ × 100 (MI-17).</summary>
public sealed record KpiTargetDto(
    int KpiTargetId, int OrderId, string OrderCode, int ProductId, string ProductCode, string ProductName,
    int SewingTeamId, string SewingTeamName, DateOnly StartDate, DateOnly EndDate, int TargetFinishedQty,
    short WorkingDays, decimal DailyTarget, string Status, string? Result, DateTime CreatedAt,
    int ActualFinishedQty = 0, decimal PercentAchieved = 0m);

/// <summary>Tạo mới / sửa toàn bộ chỉ tiêu KPI (sửa chỉ cho phép khi Status = ACTIVE).</summary>
public sealed record SaveKpiTargetDto(
    int OrderId, int ProductId, int SewingTeamId, DateOnly StartDate, DateOnly EndDate,
    int TargetFinishedQty, short WorkingDays);

/// <summary>
/// Chỉ tiêu tối đa lập được cho (đơn, mã hàng, tổ may): BTP bộ đủ còn trong kho + BTP tổ đã nhận mà chưa làm xong
/// − phần còn phải làm của các chỉ tiêu KPI khác cùng đơn + mã hàng.
/// </summary>
public sealed record KpiCapacityDto(int WarehouseBtp, int TeamHeld, int CommittedByOthers, int MaxTarget);

public sealed record KpiTargetQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
    public int? SewingTeamId { get; init; }
}

/// <summary>
/// MI-18 "KPI theo ngày": một dòng kết quả theo ngày của một chỉ tiêu KPI, chiếu thẳng từ view
/// vw_KpiDailyResult (xem VwKpiDailyResult). View chỉ có dòng cho ngày có QC nhập sản lượng (CROSS APPLY
/// trong SQL) — ngày không có dòng nghĩa là ngày đó chưa có hàng hoàn thiện, không phải lỗi dữ liệu.
/// </summary>
public sealed record KpiDailyResultDto(
    int KpiTargetId, int SewingTeamId, int OrderId, int ProductId, DateOnly WorkDate,
    decimal? DailyTarget, int? FinishedQty, int DefectQty, decimal? DefectRatePct, decimal? PercentAchieved);
