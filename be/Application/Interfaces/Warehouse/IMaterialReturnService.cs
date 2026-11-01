using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC42: tổ cắt trả vải thừa theo phiếu cấp gốc; Thủ kho xác nhận (tăng tồn NVL) hoặc từ chối.</summary>
public interface IMaterialReturnService
{
    /// <param name="cuttingTeamId">Có giá trị = chỉ phiếu của tổ cắt đó; null = mọi phiếu (Thủ kho).</param>
    Task<Result<PagedResult<MaterialReturnListItemDto>>> GetPagedAsync(MaterialReturnQuery query, int? cuttingTeamId, CancellationToken ct);
    Task<Result<MaterialReturnDto>> GetByIdAsync(int returnId, int? cuttingTeamId, CancellationToken ct);
    Task<Result<IReadOnlyList<ReturnableLineDto>>> GetReturnableAsync(int cuttingTeamId, CancellationToken ct);
    Task<Result<MaterialReturnDto>> CreateAsync(CreateMaterialReturnDto dto, int cuttingTeamId, CancellationToken ct);
    Task<Result> ConfirmAsync(int returnId, ConfirmMaterialReturnDto dto, int employeeId, CancellationToken ct);
    Task<Result> RejectAsync(int returnId, ReasonDto dto, int employeeId, CancellationToken ct);
    Task<Result> CancelAsync(int returnId, int cuttingTeamId, CancellationToken ct);
}
