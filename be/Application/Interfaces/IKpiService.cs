using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Quản đốc lập / sửa / hủy chỉ tiêu KPI tổ may theo đơn hàng + mã hàng + tổ may + kỳ
/// (KPI tổ may, theo bảng phân công Minh — khác "UC19" đã dùng cho lệnh cắt ở IWorkOrderService,
/// hai UC19 này đến từ hai nguồn đánh số khác nhau, xem ghi chú trong KpiTargetsController).
/// Không kiểm tra tồn kho BTP trước khi lập (đã chốt bỏ rule chặn theo tồn kho với người dùng, DEV-MI-16-1).
/// </summary>
public interface IKpiService
{
    Task<Result<PagedResult<KpiTargetDto>>> GetPagedAsync(KpiTargetQuery query, CancellationToken ct);

    Task<Result<KpiTargetDto>> GetByIdAsync(int kpiTargetId, CancellationToken ct);

    Task<Result<KpiTargetDto>> CreateAsync(SaveKpiTargetDto dto, CancellationToken ct);

    /// <summary>Chỉ sửa được khi Status = ACTIVE (còn trong kỳ, chưa bị đóng/hủy).</summary>
    Task<Result<KpiTargetDto>> UpdateAsync(int kpiTargetId, SaveKpiTargetDto dto, CancellationToken ct);

    /// <summary>Chỉ hủy được khi Status = ACTIVE.</summary>
    Task<Result> CancelAsync(int kpiTargetId, CancellationToken ct);

    /// <summary>
    /// Job nền (KpiAutoCloseJob): đóng các chỉ tiêu ACTIVE đã qua EndDate — so Σ FinishedQty cả kỳ
    /// (vw_KpiDailyResult) với TargetFinishedQty để suy ra Result, rồi chuyển Status sang CLOSED.
    /// Trả số chỉ tiêu đã đóng.
    /// </summary>
    Task<int> CloseExpiredAsync(DateOnly today, CancellationToken ct);

    /// <summary>
    /// MI-18 "KPI theo ngày" (UC19): danh sách kết quả theo ngày của một chỉ tiêu KPI, chiếu thẳng (thin
    /// pass-through) từ view vw_KpiDailyResult, sắp theo WorkDate tăng dần. 404 nếu KpiTargetId không tồn tại.
    /// View chỉ có dòng cho ngày có QC nhập sản lượng — ngày không có dòng là ngày chưa có hàng hoàn thiện,
    /// không backfill dòng 0 ở đây.
    /// </summary>
    Task<Result<IReadOnlyList<KpiDailyResultDto>>> GetDailyResultsAsync(int kpiTargetId, CancellationToken ct);
}
