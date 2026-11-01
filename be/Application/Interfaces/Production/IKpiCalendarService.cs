using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Lịch KPI theo ngày của một chỉ tiêu: xem theo tháng và đặt KPI từng ngày, có kiểm BTP khả dụng.</summary>
public interface IKpiCalendarService
{
    Task<Result<KpiCalendarDto>> GetAsync(int kpiTargetId, CancellationToken ct);

    /// <summary>Chỉ đặt được khi KPI còn ACTIVE; không đổi ngày đã qua và hôm nay sau giờ khóa; tổng các ngày không vượt chỉ tiêu cả kỳ; KPI chưa làm không vượt BTP khả dụng.</summary>
    Task<Result<KpiCalendarDto>> SaveDailyPlanAsync(int kpiTargetId, SaveKpiDailyPlanDto dto, CancellationToken ct);
}
