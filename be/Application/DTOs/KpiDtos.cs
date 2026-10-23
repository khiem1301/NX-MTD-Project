using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>Chỉ tiêu KPI tổ may — dùng chung cho danh sách và chi tiết; DailyTargets chỉ có ở chi tiết (danh sách để null cho nhẹ).
/// ActualFinishedQty = Σ hàng hoàn thiện cả kỳ (vw_KpiDailyResult), PercentAchieved = thực tế / chỉ tiêu cả kỳ × 100 (MI-17).</summary>
public sealed record KpiTargetDto(
    int KpiTargetId, int OrderId, string OrderCode, int ProductId, string ProductCode, string ProductName,
    int SewingTeamId, string SewingTeamName, DateOnly StartDate, DateOnly EndDate, int TargetFinishedQty,
    short WorkingDays, decimal DailyTarget, string Status, string? Result, DateTime CreatedAt,
    int ActualFinishedQty = 0, decimal PercentAchieved = 0m, IReadOnlyList<KpiDailyTargetInputDto>? DailyTargets = null);

/// <summary>Chỉ tiêu một ngày của KPI.</summary>
public sealed record KpiDailyTargetInputDto(DateOnly WorkDate, int TargetQty);

/// <summary>
/// Tạo mới / sửa toàn bộ chỉ tiêu KPI (sửa chỉ cho phép khi Status = ACTIVE). Bắt buộc nhập chỉ tiêu từng ngày vì mấy ngày đầu
/// vào đơn tổ làm chậm hơn; tổng cả kỳ = Σ các ngày, số ngày làm việc = số ngày có chỉ tiêu (dịch vụ tự tính).
/// </summary>
public sealed record SaveKpiTargetDto(
    int OrderId, int ProductId, int SewingTeamId, DateOnly StartDate, DateOnly EndDate,
    IReadOnlyList<KpiDailyTargetInputDto> DailyTargets);

public sealed record KpiTargetQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
    public int? SewingTeamId { get; init; }
}

/// <summary>
/// MI-18 "KPI theo ngày": một dòng kết quả theo ngày của một chỉ tiêu KPI, chiếu thẳng từ view
/// vw_KpiDailyResult (xem VwKpiDailyResult). Có dòng cho mọi ngày có chỉ tiêu (KpiDailyTarget; chưa có hàng thì FinishedQty = 0)
/// và ngày QC có hàng ngoài kế hoạch (DailyTarget, PercentAchieved = null).
/// </summary>
public sealed record KpiDailyResultDto(
    int KpiTargetId, int SewingTeamId, int OrderId, int ProductId, DateOnly WorkDate,
    decimal? DailyTarget, int? FinishedQty, int DefectQty, decimal? DefectRatePct, decimal? PercentAchieved);
