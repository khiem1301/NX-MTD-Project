using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IWorkOrderService
{
    /// <summary>cuttingTeamId khác null: chỉ lệnh của tổ cắt đó (tổ trưởng cắt xem lệnh của tổ mình).</summary>
    Task<Result<PagedResult<WorkOrderListItemDto>>> GetPagedAsync(WorkOrderQuery query, int? cuttingTeamId,
        CancellationToken ct);

    Task<Result<WorkOrderDto>> GetByIdAsync(int workOrderId, int? cuttingTeamId, CancellationToken ct);

    /// <summary>Vải cần / đã cấp / còn phải cấp của lệnh cắt — dùng điền sẵn phiếu cấp vải (UC40).</summary>
    Task<Result<IReadOnlyList<FabricRequirementDto>>> GetFabricRequirementsAsync(int workOrderId, CancellationToken ct);

    Task<Result<WorkOrderDto>> CreateAsync(SaveWorkOrderDto dto, CancellationToken ct);

    /// <summary>Đơn cắt bù đã duyệt và chưa có lệnh cắt bù (Quản đốc chọn khi lập lệnh cắt bù).</summary>
    Task<Result<IReadOnlyList<ReplacementRequestDto>>> GetOpenReplacementRequestsAsync(CancellationToken ct);

    /// <summary>Lệnh cắt bù (SourceRequestId = đơn cắt bù): không tính vào trần kế hoạch của dòng đơn.</summary>
    Task<Result<WorkOrderDto>> CreateReplacementAsync(CreateReplacementWorkOrderDto dto, CancellationToken ct);
    Task<Result<WorkOrderDto>> UpdateAsync(int workOrderId, SaveWorkOrderDto dto, CancellationToken ct);
    Task<Result> CancelAsync(int workOrderId, CancellationToken ct);
}
