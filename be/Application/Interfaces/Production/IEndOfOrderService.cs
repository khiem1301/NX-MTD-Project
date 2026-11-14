using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Kết thúc đơn của một tổ may (Quản đốc): kiểm cuối đơn (lệch chỉ cảnh báo và thông báo, không tự trừ lương — DEV-ALL-107) → chốt KPI → thông báo tăng ca.
/// </summary>
public interface IEndOfOrderService
{
    /// <summary>
    /// Xem trước, không ghi gì: KPI đạt / không, dòng đơn lệch ở công đoạn cuối và những ai đã báo, quyết định tăng ca, điều kiện chặn.
    /// <paramref name="teamId"/> khác null (tổ trưởng / tổ phó) thì chỉ xem được chỉ tiêu của tổ mình; Quản đốc truyền null.
    /// </summary>
    Task<Result<EndOfOrderPreviewDto>> PreviewAsync(int kpiTargetId, int? teamId, CancellationToken ct);

    /// <summary>Chốt Result của KPI, đóng chỉ tiêu, thông báo lệch kiểm cuối đơn và thiếu KPI (tăng ca) cho Quản đốc và tổ trưởng.</summary>
    Task<Result<EndOfOrderPreviewDto>> FinishAsync(int kpiTargetId, CancellationToken ct);
}
