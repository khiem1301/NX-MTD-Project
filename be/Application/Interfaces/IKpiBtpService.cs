using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Màn so sánh BTP đã cắt vào kho – KPI ngày đã đặt cho tổ may – số hoàn thiện thực tế của tổ, trừ dần BTP theo lũy kế.
/// Chỉ đọc. teamId có giá trị (tổ trưởng / tổ phó) = chỉ thấy KPI của tổ mình; null = mọi tổ.
/// </summary>
public interface IKpiBtpService
{
    Task<Result<IReadOnlyList<KpiBtpOptionDto>>> GetOptionsAsync(int? teamId, CancellationToken ct);
    Task<Result<KpiBtpComparisonDto>> GetAsync(int kpiTargetId, int? teamId, CancellationToken ct);
}
