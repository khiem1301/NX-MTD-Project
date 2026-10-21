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
    Task<Result<OperationImportPreviewDto>> PreviewImportAsync(int productId, Stream stream, string? sheetName, CancellationToken ct);

    /// <summary>Nhập công đoạn từ Excel theo mã công đoạn (OP + STT): thêm mới hoặc cập nhật, không xóa công đoạn nào. <paramref name="finalStt"/> đánh dấu công đoạn cuối.</summary>
    Task<Result<OperationImportResultDto>> ImportAsync(int productId, Stream stream, string? sheetName, int? finalStt, CancellationToken ct);
}

/// <summary>KI-01 (UC25, UC26): đơn giá công đoạn DRAFT → PENDING_APPROVAL → APPROVED / REJECTED (Admin duyệt).</summary>
public interface IPieceRateService
{
    Task<Result<PagedResult<PieceRateDto>>> ListAsync(PieceRateQuery query, CancellationToken ct);
    Task<Result<int>> CreateAsync(SavePieceRateDto dto, CancellationToken ct);

    /// <summary>Chỉ sửa khi DRAFT hoặc REJECTED; sau khi sửa quay về DRAFT.</summary>
    Task<Result> UpdateAsync(int pieceRateId, SavePieceRateDto dto, CancellationToken ct);

    Task<Result> SubmitAsync(int pieceRateId, CancellationToken ct);
    Task<Result> ApproveAsync(int pieceRateId, ApprovePieceRateDto dto, CancellationToken ct);
    Task<Result> RejectAsync(int pieceRateId, RejectPieceRateDto dto, CancellationToken ct);

    /// <summary>Đơn giá APPROVED có EffectiveDate lớn nhất ≤ ngày; null nếu chưa có (MSG18, GBR-11).</summary>
    Task<EffectivePieceRate?> FindEffectiveAsync(int operationId, DateOnly date, CancellationToken ct);
}

/// <summary>KI-03 (UC52): tổ trưởng / tổ phó may phân công công nhân × công đoạn; đổi phân công giữ lịch sử.</summary>
public interface IOperationAssignmentService
{
    Task<Result<IReadOnlyList<OperationAssignmentDto>>> ListCurrentAsync(int orderId, CancellationToken ct);

    /// <summary>Công đoạn may đang dùng của các mã hàng trong đơn (chọn khi phân công, ghi hàng hỏng).</summary>
    Task<Result<IReadOnlyList<OperationOptionDto>>> ListOperationsAsync(int orderId, CancellationToken ct);
    Task<Result<int>> AssignAsync(AssignOperationDto dto, CancellationToken ct);
    Task<Result<int>> ReplaceAsync(int assignmentId, ReplaceAssignmentDto dto, CancellationToken ct);
    Task<Result> CloseAsync(int assignmentId, CancellationToken ct);
    Task<Result<int>> CopyAsync(CopyAssignmentsDto dto, CancellationToken ct);
}

/// <summary>KI-04 + KI-11 (UC55, UC22): sản lượng cuối ca của tổ may; sau hạn chỉ Quản đốc sửa có lý do.</summary>
public interface IOutputService
{
    Task<Result<OutputGridDto>> GetGridAsync(DateOnly workDate, string shiftCode, int orderLineId, int? sewingTeamId, CancellationToken ct);
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

/// <summary>KI-08 (UC56): tổ trưởng / tổ phó may ghi hàng hỏng Rework / Scrap; công nhân xem và phản hồi nếu ghi sai.</summary>
public interface IDefectService
{
    Task<Result<PagedResult<DefectDto>>> ListForTeamAsync(DefectQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<DefectDto>>> ListMineAsync(int workerId, DefectQuery query, CancellationToken ct);
    Task<Result<int>> CreateAsync(CreateDefectDto dto, CancellationToken ct);
    Task<Result> CancelAsync(int defectRecordId, CancelDefectDto dto, CancellationToken ct);
    Task<Result> ObjectAsync(int workerId, int defectRecordId, ObjectDefectDto dto, CancellationToken ct);
}
