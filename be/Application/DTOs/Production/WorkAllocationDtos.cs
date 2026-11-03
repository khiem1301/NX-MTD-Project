using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>Một lần giao / thu hồi việc với công nhân (WorkAllocation): số lần làm công đoạn × màu × size.</summary>
public sealed record WorkAllocationDto(
    int WorkAllocationId, DateOnly WorkDate, string MovementType, int SewingTeamId, string TeamName, int WorkerId, string WorkerCode,
    string WorkerName, int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode,
    int OperationId, string OperationCode, string OperationName, int Quantity,
    int? ConfirmedQty, string? DiscrepancyReason, string? RetrievalReason, string? RetrievalNote, string ConfirmMode,
    string? OnBehalfReason, string Status, string? RejectReason, DateTime CreatedAt, DateTime? ConfirmedAt);

/// <summary>Tiến độ chia việc của một công đoạn × dòng đơn (vw_OperationAllocationProgress): cần làm = số bộ BTP tổ đã nhận.</summary>
public sealed record OperationProgressDto(
    int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode, int OperationId,
    string OperationCode, string OperationName, string? GroupName, int RequiredQty,
    int AllocatedQty, int PendingQty, int UnallocatedQty, int OutputQty);

/// <summary>Số lần một công nhân đang được giao ở một công đoạn × dòng đơn; còn thu hồi được = đang giữ − chờ thu hồi − đã báo sản lượng.</summary>
public sealed record WorkerAllocationLineDto(
    int WorkerId, string WorkerCode, string WorkerName, int OrderLineId, int OperationId, int HeldQty,
    int PendingRetrievalQty, int OutputQty, int RetrievableQty);

public sealed record WorkAllocationBoardDto(
    IReadOnlyList<OperationProgressDto> Operations, IReadOnlyList<WorkerAllocationLineDto> Workers);

/// <param name="MovementType">HANDOVER (giao việc) hoặc RETRIEVAL (thu hồi).</param>
/// <param name="RetrievalReason">Bắt buộc khi thu hồi: OPERATION_CHANGED, WORKER_ABSENT, STYLE_FINISHED.</param>
public sealed record CreateWorkAllocationDto(string MovementType, int WorkerId, int OrderLineId, int OperationId,
    int Quantity, string? RetrievalReason);

/// <param name="ConfirmedQty">1..Quantity; khác Quantity thì bắt buộc DiscrepancyReason (CK_WA_Confirmed). Không nhận gì thì từ chối.</param>
public sealed record ConfirmWorkAllocationDto(int ConfirmedQty, string? DiscrepancyReason);

/// <summary>Tổ trưởng xác nhận hộ công nhân (UC54): bắt buộc lý do.</summary>
public sealed record ConfirmOnBehalfDto(int ConfirmedQty, string? DiscrepancyReason, string OnBehalfReason);

public sealed record WorkAllocationQuery : PageQuery
{
    public string? Status { get; init; }
    public string? MovementType { get; init; }
    public int? WorkerId { get; init; }
    public int? OrderId { get; init; }
}

// ---------- Giao sản lượng công đoạn theo ngày ----------

/// <summary>
/// Một dòng giao việc của ngày: công nhân × dòng đơn × công đoạn. <c>IsAssigned</c> = công đoạn đã phân công cho công nhân
/// trước khi bắt đầu may; dòng không có cờ này là công đoạn tổ trưởng thêm ngoài phân công.
/// </summary>
/// <param name="AllocatedQty">Số lần giao của ngày (giao − thu hồi), cũng là trần sản lượng cuối ca của ngày.</param>
/// <param name="OutputQty">Sản lượng cuối ca đã báo của ngày.</param>
/// <param name="RemainingQty">Phần giao của ngày chưa làm hết, có thể thu hồi (đã chặn theo số công nhân còn giữ).</param>
/// <param name="UnallocatedQty">Số lần còn giao được của công đoạn × dòng đơn trên toàn tổ.</param>
public sealed record DailyAllocationRowDto(
    int WorkerId, string WorkerCode, string WorkerName, int OrderLineId, string OrderCode, string ProductCode, string Color,
    string SizeCode, int OperationId, string OperationCode, string OperationName, string? GroupName, bool IsAssigned,
    int AllocatedQty, int OutputQty, int RemainingQty, int UnallocatedQty);

public sealed record DailyAllocationBoardDto(DateOnly WorkDate, IReadOnlyList<DailyAllocationRowDto> Rows);

/// <param name="Quantity">Số lần giao của ngày sau khi lưu (không phải phần giao thêm). 0 = bỏ phần giao của ngày.</param>
public sealed record SaveDailyAllocationItem(int WorkerId, int OrderLineId, int OperationId, int Quantity);

/// <param name="Reason">Bắt buộc khi có dòng hạ số giao xuống (ghi vào phiếu thu hồi).</param>
public sealed record SaveDailyAllocationDto(DateOnly WorkDate, IReadOnlyList<SaveDailyAllocationItem> Items, string? Reason);

/// <param name="WorkerIds">Rỗng = mọi công nhân của tổ.</param>
public sealed record RetrieveDailyAllocationDto(DateOnly WorkDate, IReadOnlyList<int>? WorkerIds, string Reason);

public sealed record DailyAllocationResultDto(int Added, int Adjusted, int Retrieved);
