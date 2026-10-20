using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface ICutHandoverService
{
    /// <summary>cuttingTeamId khác null: chỉ phiếu của tổ cắt đó.</summary>
    Task<Result<PagedResult<CutHandoverListItemDto>>> GetPagedAsync(CutHandoverQuery query, int? cuttingTeamId,
        CancellationToken ct);

    Task<Result<CutHandoverDto>> GetByIdAsync(int handoverId, int? cuttingTeamId, CancellationToken ct);

    /// <summary>Số BTP còn được bàn giao của lệnh cắt — dùng điền sẵn phiếu bàn giao.</summary>
    Task<Result<IReadOnlyList<HandoverableLineDto>>> GetHandoverableAsync(int workOrderId, int cuttingTeamId,
        CancellationToken ct);

    /// <summary>Tổ trưởng cắt lập phiếu; các bàn cắt đang RECORDED của lệnh chuyển LOCKED.</summary>
    Task<Result<CutHandoverDto>> CreateAsync(CreateCutHandoverDto dto, int cuttingTeamId, CancellationToken ct);

    /// <summary>Thủ kho xác nhận số đếm: tăng tồn BTP theo dòng đơn.</summary>
    Task<Result> ConfirmAsync(int handoverId, ConfirmCutHandoverDto dto, int employeeId, CancellationToken ct);

    Task<Result> RejectAsync(int handoverId, ReasonDto dto, int employeeId, CancellationToken ct);
}
