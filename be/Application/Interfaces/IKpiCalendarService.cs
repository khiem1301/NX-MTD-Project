using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Lịch KPI theo ngày: Quản đốc đặt chỉ tiêu từng ngày (mấy ngày đầu vào đơn tổ làm chậm hơn nên không chia đều cả kỳ) và xem
/// Tổng bán thành phẩm, số còn lại để đặt KPI, thực tế của tổ. Admin chỉ xem.
/// </summary>
public interface IKpiCalendarService
{
    Task<Result<KpiCalendarDto>> GetAsync(int kpiTargetId, CancellationToken ct);

    /// <summary>
    /// Thay toàn bộ chỉ tiêu ngày của một KPI đang chạy. Σ các ngày ≤ tổng cả kỳ; KPI chưa ra chuyền không được vượt
    /// BTP còn lại (trừ khi đang giảm). Trả lại lịch mới.
    /// </summary>
    Task<Result<KpiCalendarDto>> SaveDailyPlanAsync(int kpiTargetId, SaveKpiDailyPlanDto dto, CancellationToken ct);
}
