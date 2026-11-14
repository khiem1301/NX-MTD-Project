using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// UC42: tổ cắt (theo lệnh cắt) và tổ may (NVL không theo đơn, BTP theo đơn – mã – màu – size) trả NVL / phế phẩm / BTP về kho;
/// Thủ kho xác nhận (tăng tồn) hoặc từ chối.
/// </summary>
public interface IMaterialReturnService
{
    /// <param name="teamId">Có giá trị = chỉ phiếu của tổ đó; null = mọi phiếu (Thủ kho).</param>
    Task<Result<PagedResult<MaterialReturnListItemDto>>> GetPagedAsync(MaterialReturnQuery query, int? teamId, CancellationToken ct);
    Task<Result<MaterialReturnDto>> GetByIdAsync(int returnId, int? teamId, CancellationToken ct);
    Task<Result<IReadOnlyList<ReturnableLineDto>>> GetReturnableAsync(int teamId, ReturnableQuery query, CancellationToken ct);
    Task<Result<MaterialReturnDto>> CreateAsync(CreateMaterialReturnDto dto, int teamId, CancellationToken ct);
    Task<Result> ConfirmAsync(int returnId, ConfirmMaterialReturnDto dto, int employeeId, CancellationToken ct);
    Task<Result> RejectAsync(int returnId, ReasonDto dto, int employeeId, CancellationToken ct);
    Task<Result> CancelAsync(int returnId, int teamId, CancellationToken ct);
}
