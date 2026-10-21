using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// KI-10: nguồn đọc dùng chung về sản lượng, Scrap và số QC đạt cho các luồng khác (UC19 KPI, UC21 tiến độ,
/// UC59 lương tạm tính, UC44 bàn giao thành phẩm). Chỉ đọc (view), không ghi — luồng khác không tự truy vấn
/// <c>OutputRecord</c> / <c>DefectRecord</c> / <c>QcBatch</c>.
/// </summary>
public interface IOutputQueryService
{
    /// <summary>Lương tạm tính từng ngày của một công nhân khoán sản phẩm (vw_WorkerDailyEstimatedPay), tăng dần theo ngày.</summary>
    Task<IReadOnlyList<MyEstimatedPayDayDto>> GetWorkerDailyPayAsync(int workerId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Σ hàng hoàn thiện cả kỳ của từng chỉ tiêu KPI (vw_KpiDailyResult); chỉ tiêu chưa có QC không có trong kết quả.</summary>
    Task<IReadOnlyDictionary<int, int>> GetKpiFinishedTotalsAsync(IReadOnlyCollection<int> kpiTargetIds, CancellationToken ct);

    /// <summary>Kết quả KPI theo ngày của một chỉ tiêu (chỉ ngày có QC nhập), tăng dần theo ngày.</summary>
    Task<IReadOnlyList<KpiDailyResultDto>> GetKpiDailyResultsAsync(int kpiTargetId, CancellationToken ct);

    /// <summary>Tiến độ theo dòng đơn của một đơn hàng (vw_OrderLineProgress).</summary>
    Task<IReadOnlyList<OrderLineProgressDto>> GetOrderLineProgressAsync(int orderId, CancellationToken ct);

    /// <summary>Σ số QC đạt của từng dòng đơn (cho bàn giao thành phẩm: tổng giao ≤ số QC đạt).</summary>
    Task<IReadOnlyDictionary<int, int>> GetQcFinishedByOrderLineAsync(IReadOnlyCollection<int> orderLineIds, CancellationToken ct);
}
