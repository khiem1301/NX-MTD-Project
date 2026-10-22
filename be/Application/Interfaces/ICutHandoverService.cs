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

/// <summary>UC44: QA/QC bàn giao thành phẩm (số QC đạt) vào kho FINISHED; Thủ kho đếm, xác nhận hoặc từ chối.</summary>
public interface IFgHandoverService
{
    /// <param name="inspectorId">Có giá trị = chỉ phiếu của QA/QC này; null = mọi phiếu (Thủ kho, Quản đốc).</param>
    Task<Result<PagedResult<FgHandoverListItemDto>>> GetPagedAsync(FgHandoverQuery query, int? inspectorId, CancellationToken ct);
    Task<Result<FgHandoverDto>> GetByIdAsync(int handoverId, int? inspectorId, CancellationToken ct);
    Task<Result<IReadOnlyList<FgHandoverableLineDto>>> GetHandoverableAsync(int orderId, CancellationToken ct);
    Task<Result<FgHandoverDto>> CreateAsync(CreateFgHandoverDto dto, int inspectorId, CancellationToken ct);
    Task<Result> ConfirmAsync(int handoverId, ConfirmFgHandoverDto dto, int employeeId, CancellationToken ct);
    Task<Result> RejectAsync(int handoverId, ReasonDto dto, int employeeId, CancellationToken ct);

    /// <summary>QA/QC rút lại phiếu của mình khi Thủ kho chưa nhận.</summary>
    Task<Result> CancelAsync(int handoverId, int inspectorId, CancellationToken ct);
}
