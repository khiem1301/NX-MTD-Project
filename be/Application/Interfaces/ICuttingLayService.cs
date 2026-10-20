using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface ICuttingLayService
{
    Task<Result<PagedResult<CuttingLayDto>>> GetPagedAsync(CuttingLayQuery query, int cuttingTeamId, CancellationToken ct);
    Task<Result<CuttingLayDto>> GetByIdAsync(int layId, int cuttingTeamId, CancellationToken ct);

    /// <summary>Thành công có thể kèm Message cảnh báo khi tổng cắt vượt kế hoạch nhưng còn trong % dư.</summary>
    Task<Result<CuttingLayDto>> CreateAsync(SaveCuttingLayDto dto, int cuttingTeamId, CancellationToken ct);

    /// <summary>Chỉ sửa bàn đang RECORDED (chưa bàn giao).</summary>
    Task<Result<CuttingLayDto>> UpdateAsync(int layId, SaveCuttingLayDto dto, int cuttingTeamId, CancellationToken ct);
}
