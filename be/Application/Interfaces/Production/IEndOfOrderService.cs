using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Kết thúc đơn của một tổ may (Quản đốc): kiểm cuối đơn → phiếu hàng hỏng cho công nhân lệch → chốt KPI → tăng ca.</summary>
public interface IEndOfOrderService
{
    /// <summary>Xem trước, không ghi gì: KPI đạt / không, lệch theo từng công nhân, quyết định tăng ca, điều kiện chặn.</summary>
    Task<Result<EndOfOrderPreviewDto>> PreviewAsync(int kpiTargetId, CancellationToken ct);

    /// <summary>Ghi phiếu hàng hỏng cho phần lệch, chốt Result của KPI, đóng chỉ tiêu và tạo đơn tăng ca nếu không đạt.</summary>
    Task<Result<EndOfOrderPreviewDto>> FinishAsync(int kpiTargetId, CancellationToken ct);
}
