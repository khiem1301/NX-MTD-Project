using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>KI-01 (UC25): Kế toán lập công đoạn của mã hàng. Đúng một công đoạn may cuối đang dùng cho mỗi mã hàng.</summary>
public interface IOperationService
{
    Task<Result<IReadOnlyList<OperationDto>>> ListAsync(int productId, bool includeDiscontinued, CancellationToken ct);
    Task<Result<int>> CreateAsync(SaveOperationDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int operationId, SaveOperationDto dto, CancellationToken ct);
    Task<Result> DiscontinueAsync(int operationId, CancellationToken ct);
    Task<Result> ReactivateAsync(int operationId, CancellationToken ct);

    /// <summary>Xem trước file Excel quy trình công nghệ (chưa ghi DB): dòng nào thêm mới, cập nhật, không đổi, lỗi.</summary>
    Task<Result<OperationImportPreviewDto>> PreviewImportAsync(int productId, Stream stream, string? sheetName, DateOnly? effectiveDate, CancellationToken ct);

    /// <summary>Nhập công đoạn từ Excel theo mã công đoạn (OP + STT): thêm mới hoặc cập nhật, không xóa công đoạn nào. <paramref name="finalStt"/> đánh dấu công đoạn cuối.</summary>
    Task<Result<OperationImportResultDto>> ImportAsync(int productId, Stream stream, string? sheetName, int? finalStt, DateOnly? effectiveDate, bool submitRates, CancellationToken ct);
}

/// <summary>KI-01 (UC25, UC26): đơn giá công đoạn DRAFT → PENDING_APPROVAL → APPROVED / REJECTED (Admin duyệt).</summary>
public interface IPieceRateService
{
    /// <param name="includeDraft">false = bỏ đơn giá Nháp (Admin chỉ thấy đơn giá đã gửi duyệt trở đi).</param>
    Task<Result<PagedResult<PieceRateDto>>> ListAsync(PieceRateQuery query, CancellationToken ct, bool includeDraft = true);
    Task<Result<int>> CreateAsync(SavePieceRateDto dto, CancellationToken ct);

    /// <summary>Chỉ sửa khi DRAFT hoặc REJECTED; sau khi sửa quay về DRAFT.</summary>
    Task<Result> UpdateAsync(int pieceRateId, SavePieceRateDto dto, CancellationToken ct);

    Task<Result> SubmitAsync(int pieceRateId, CancellationToken ct);
    Task<Result<int>> SubmitManyAsync(SubmitPieceRatesDto dto, CancellationToken ct);
    Task<Result> ApproveAsync(int pieceRateId, ApprovePieceRateDto dto, CancellationToken ct);
    Task<Result> RejectAsync(int pieceRateId, RejectPieceRateDto dto, CancellationToken ct);

    /// <summary>Admin duyệt nhiều đơn giá đang chờ duyệt một lần; dòng không ở trạng thái chờ duyệt bị bỏ qua. Trả số đơn giá đã duyệt.</summary>
    Task<Result<int>> ApproveManyAsync(ApproveManyPieceRatesDto dto, CancellationToken ct);

    /// <summary>Đơn giá APPROVED có EffectiveDate lớn nhất ≤ ngày; null nếu chưa có (MSG18, GBR-11).</summary>
    Task<EffectivePieceRate?> FindEffectiveAsync(int operationId, DateOnly date, CancellationToken ct);

    /// <summary>Như FindEffectiveAsync cho nhiều công đoạn trong một truy vấn; công đoạn chưa có đơn giá duyệt không có trong kết quả.</summary>
    Task<IReadOnlyDictionary<int, EffectivePieceRate>> FindEffectiveManyAsync(IReadOnlyCollection<int> operationIds, DateOnly date,
        CancellationToken ct);
}

/// <summary>KI-03 (UC52): tổ trưởng / tổ phó may phân công công nhân × công đoạn; đổi phân công giữ lịch sử.</summary>
/// <summary>Admin, Kế toán quản lý nhóm công đoạn của mã hàng (DEV-ALL-50).</summary>
public interface IOperationGroupService
{
    Task<Result<IReadOnlyList<OperationGroupDto>>> ListAsync(int productId, CancellationToken ct);
    Task<Result<int>> CreateAsync(CreateOperationGroupDto dto, CancellationToken ct);
    Task<Result> RenameAsync(int groupId, RenameOperationGroupDto dto, CancellationToken ct);
    Task<Result> DeleteAsync(int groupId, CancellationToken ct);
    Task<Result<int>> SetOperationsGroupAsync(SetOperationsGroupDto dto, CancellationToken ct);
}

/// <summary>Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may (DEV-ALL-49).</summary>
public interface IOrderLineTeamService
{
    Task<Result<PagedResult<OrderLineTeamDto>>> GetPagedAsync(OrderLineTeamQuery query, CancellationToken ct);
    Task<Result> SetTeamsAsync(SetOrderLineTeamsDto dto, CancellationToken ct);
    Task<Result<int>> AddTeamsAsync(AddOrderLineTeamsDto dto, CancellationToken ct);

    /// <summary>Tổ may được chia ít nhất một dòng của mã hàng trong đơn, để chọn tổ khi đặt chỉ tiêu KPI.</summary>
    Task<Result<IReadOnlyList<AllottedTeamDto>>> ListTeamsAsync(int orderId, int productId, CancellationToken ct);
}

public interface IOperationAssignmentService
{
    Task<Result<IReadOnlyList<OperationAssignmentDto>>> ListCurrentAsync(int orderId, CancellationToken ct);

    /// <summary>Công đoạn may đang dùng của các mã hàng trong đơn được chia cho tổ mình (chọn khi phân công, ghi hàng hỏng).</summary>
    Task<Result<IReadOnlyList<OperationOptionDto>>> ListOperationsAsync(int orderId, CancellationToken ct);
    Task<Result<int>> AssignAsync(AssignOperationDto dto, CancellationToken ct);

    /// <summary>Phân công nhiều công đoạn cho một công nhân; trả số dòng mới (công đoạn đã phân công đúng người đó thì bỏ qua).</summary>
    Task<Result<int>> AssignManyAsync(AssignOperationsDto dto, CancellationToken ct);
    Task<Result<int>> ReplaceAsync(int assignmentId, ReplaceAssignmentDto dto, CancellationToken ct);
    Task<Result> CloseAsync(int assignmentId, CancellationToken ct);
    Task<Result<int>> CopyAsync(CopyAssignmentsDto dto, CancellationToken ct);
}

/// <summary>KI-04 + KI-11 (UC55, UC22): sản lượng cuối ca của tổ may; sau hạn chỉ Quản đốc sửa có lý do.</summary>
public interface IOutputService
{
    Task<Result<OutputGridDto>> GetGridAsync(DateOnly workDate, string shiftCode, int orderLineId, int? sewingTeamId, CancellationToken ct);
    /// <summary>Lưới xem tổng sản lượng cả ngày (cộng các ca); không dùng để nhập/sửa.</summary>
    Task<Result<OutputGridDto>> GetDayGridAsync(DateOnly workDate, int orderLineId, int? sewingTeamId, CancellationToken ct);
    Task<Result<OutputSaveResultDto>> SaveAsync(SaveOutputDto dto, CancellationToken ct);
}

/// <summary>KI-06 + KI-11 (UC57, UC22): QA/QC nhập số đạt theo đợt; sau hết ca chỉ Quản đốc sửa có lý do.</summary>
public interface IQcBatchService
{
    Task<Result<PagedResult<QcBatchDto>>> ListAsync(QcBatchQuery query, CancellationToken ct);
    Task<Result<OnLineQtyDto>> GetOnLineAsync(int sewingTeamId, int orderLineId, CancellationToken ct);
    Task<Result<int>> CreateAsync(CreateQcBatchDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int qcBatchId, UpdateQcBatchDto dto, CancellationToken ct);
    Task<Result> DeleteAsync(int qcBatchId, DeleteQcBatchDto dto, CancellationToken ct);
}

/// <summary>
/// KI-08 (UC56): tổ trưởng / tổ phó may ghi, sửa, hủy phiếu hàng hỏng (trong 24 giờ); Quản đốc / Admin sửa, hủy khi chưa vướng ràng
/// buộc; công nhân chỉ xem phiếu của mình.
/// </summary>
public interface IDefectService
{
    Task<Result<PagedResult<DefectDto>>> ListForTeamAsync(DefectQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<DefectDto>>> ListMineAsync(int workerId, DefectQuery query, CancellationToken ct);
    Task<Result<int>> CreateAsync(CreateDefectDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int defectRecordId, UpdateDefectDto dto, CancellationToken ct);
    Task<Result> CancelAsync(int defectRecordId, CancelDefectDto dto, CancellationToken ct);
}
