using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

// ================= KI-01: công đoạn + đơn giá (UC25, UC26) =================

/// <summary>
/// CurrentRate = đơn giá đã duyệt đang hiệu lực hôm nay. NextRate = đơn giá mới nhất (theo ngày hiệu lực) nếu nó chưa
/// thành đơn giá hiện hành: Nháp, Chờ duyệt, Bị từ chối, hoặc đã duyệt nhưng chưa tới ngày hiệu lực; null nếu không có.
/// </summary>
public sealed record OperationDto(
    int OperationId, int ProductId, string ProductCode, string OperationCode, string OperationName,
    short StationSequence, byte? SkillLevel, decimal? StandardTimeSec, string TeamType, bool IsFinalStep,
    string Status, decimal? CurrentRate, OperationRateDto? NextRate = null);

public sealed record OperationRateDto(int PieceRateId, decimal Rate, DateOnly EffectiveDate, string Status, string? RejectReason);

/// <summary>TeamType nhận PascalCase "Sewing" | "Cutting" (mặc định Sewing).</summary>
public sealed record SaveOperationDto(
    int ProductId, string OperationCode, string OperationName, short StationSequence, byte? SkillLevel,
    decimal? StandardTimeSec, string? TeamType, bool IsFinalStep);

public sealed record PieceRateDto(
    int PieceRateId, int OperationId, string OperationCode, string OperationName, int ProductId, string ProductCode,
    decimal Rate, DateOnly EffectiveDate, string Status, DateTime? SubmittedAt, DateTime? ApprovedAt,
    string? ApprovalNote, string? RejectReason);

public sealed record SavePieceRateDto(int OperationId, decimal Rate, DateOnly EffectiveDate);

public sealed record PieceRateQuery : PageQuery
{
    public int? OperationId { get; init; }
    public int? ProductId { get; init; }

    /// <summary>PascalCase, vd. "PendingApproval".</summary>
    public string? Status { get; init; }
}

public sealed record ApprovePieceRateDto(string? Note);

/// <summary>Duyệt nhiều đơn giá chờ duyệt một lần (vd. sau khi nhập công đoạn từ Excel).</summary>
public sealed record ApproveManyPieceRatesDto(IReadOnlyList<int> Ids, string? Note);

public sealed record RejectPieceRateDto(string Reason);

/// <summary>Đơn giá hiệu lực đã duyệt tại một ngày: dùng để khóa vào sản lượng / Scrap.</summary>
public sealed record EffectivePieceRate(int PieceRateId, decimal Rate);

// ================= KI-03: phân công công đoạn (UC52) =================

public sealed record OperationAssignmentDto(
    int OperationAssignmentId, int OrderId, int OperationId, string OperationCode, string OperationName,
    int ProductId, string ProductCode, int WorkerId, string WorkerCode, string WorkerName, DateOnly ValidFrom);

public sealed record AssignOperationDto(int OrderId, int OperationId, int WorkerId);

/// <summary>Một công nhân làm nhiều công đoạn: phân công một lần, hợp lệ hết mới lưu.</summary>
public sealed record AssignOperationsDto(int OrderId, int WorkerId, IReadOnlyList<int> OperationIds);

/// <summary>Đổi phân công = đóng dòng cũ rồi mở dòng mới cho người khác (giữ lịch sử).</summary>
public sealed record ReplaceAssignmentDto(int NewWorkerId);

/// <summary>Chép phân công đang hiệu lực của tổ từ đơn cũ sang đơn mới (cùng công đoạn / mã hàng).</summary>
public sealed record CopyAssignmentsDto(int FromOrderId, int ToOrderId);

// ================= KI-04 + KI-11: sản lượng cuối ca (UC55) =================

/// <summary>Một dòng lưới: một công nhân × công đoạn của một dòng đơn trong ca. Quantity = null nghĩa là chưa nhập.</summary>
public sealed record OutputGridRowDto(
    int OperationId, string OperationCode, string OperationName, int WorkerId, string WorkerCode, string WorkerName,
    long? OutputRecordId, int? Quantity, decimal? UnitRate, bool RateApproved, bool IsOutsideAssignment,
    bool NoAttendanceWarning, bool CanEdit, DateTime? EditDeadline, byte[]? RowVer);

public sealed record OutputGridDto(
    DateOnly WorkDate, string ShiftCode, int OrderLineId, int SewingTeamId, int ReceivedFromWarehouse,
    IReadOnlyList<OutputGridRowDto> Rows);

/// <summary>RowVer có khi sửa dòng đã nhập (lấy từ lưới) để phát hiện hai người sửa cùng lúc; dòng mới để trống.</summary>
public sealed record OutputLineInput(int OperationId, int WorkerId, int Quantity, byte[]? RowVer = null);

/// <summary>SewingTeamId chỉ Quản đốc điền (sửa hộ tổ); Reason bắt buộc khi sửa sau hạn (UC22). Quantity 0 = xóa dòng đã nhập.</summary>
public sealed record SaveOutputDto(
    DateOnly WorkDate, string ShiftCode, int OrderLineId, int? SewingTeamId, string? Reason,
    IReadOnlyList<OutputLineInput> Rows);

public sealed record OutputSaveResultDto(int Inserted, int Updated, int Deleted, IReadOnlyList<string> Warnings);

// ================= KI-06 + KI-11: QC (UC57) =================

public sealed record QcBatchDto(
    int QcBatchId, DateOnly WorkDate, string ShiftCode, TimeOnly BatchTime, int SewingTeamId, string SewingTeamName,
    int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode, int FinishedQty,
    int InspectorId, string InspectorName, DateTime EditDeadline, bool CanEdit, byte[] RowVer);

public sealed record CreateQcBatchDto(
    DateOnly WorkDate, string ShiftCode, TimeOnly BatchTime, int SewingTeamId, int OrderLineId, int FinishedQty);

public sealed record UpdateQcBatchDto(int FinishedQty, string? Reason, byte[] RowVer);

public sealed record DeleteQcBatchDto(string? Reason, byte[] RowVer);

public sealed record QcBatchQuery : PageQuery
{
    public DateOnly? WorkDate { get; init; }
    public int? SewingTeamId { get; init; }
    public int? OrderLineId { get; init; }
}

/// <summary>Hàng đang có trên chuyền của một (tổ, dòng đơn): BTP đã nhận − đã QC đạt − Scrap.</summary>
public sealed record OnLineQtyDto(int SewingTeamId, int OrderLineId, int Received, int Finished, int Scrap, int OnLine);

// ================= KI-08: hàng hỏng (UC56) =================

public sealed record DefectDto(
    int DefectRecordId, string DefectCode, DateOnly WorkDate, string? ShiftCode, int SewingTeamId, int OrderLineId,
    string OrderCode, string ProductCode, int OperationId, string OperationCode, string OperationName,
    int ResponsibleWorkerId, string ResponsibleWorkerName, string DefectType, int Quantity, string DefectReasonCode,
    string? ReasonNote, decimal? ScrapUnitRate, DateTime? WorkerNotifiedAt, string? WorkerObjection, DateTime? ObjectedAt,
    DateTime EditDeadline, string Status, DateTime RecordedAt, bool CanEdit, byte[] RowVer, int OrderId,
    int? ReplacementRequestId, string? ReplacementRequestCode, string? ReplacementRequestStatus);

/// <summary>DefectType nhận "Rework" | "Scrap"; ReasonCode nhận PascalCase ("SkippedStitch", "Other"...).</summary>
public sealed record CreateDefectDto(
    DateOnly WorkDate, string? ShiftCode, int OrderLineId, int OperationId, int ResponsibleWorkerId, string DefectType,
    int Quantity, string ReasonCode, string? ReasonNote);

public sealed record CancelDefectDto(string? Reason, byte[] RowVer);

public sealed record ObjectDefectDto(string Objection, byte[] RowVer);

public sealed record DefectQuery : PageQuery
{
    public DateOnly? WorkDate { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

// ================= KI-10: IOutputQueryService (dùng chung cho UC19, UC21, UC59) =================

/// <summary>Tiến độ một dòng đơn: đặt, cắt, vào kho BTP, cấp tổ, hoàn thiện (QC), nhập kho TP, đã giao, lỗi (UC21).</summary>
public sealed record OrderLineProgressDto(
    int OrderLineId, int OrderId, string OrderCode, int ProductId, string ProductCode, string Color, string SizeCode,
    int OrderedQty, int CutQty, int CutHandedToWarehouseQty, int IssuedToSewingQty, int FinishedQty,
    int FinishedReceivedQty, int DeliveredQty, int ReworkQty, int ScrapQty, decimal? DefectRatePct);

/// <summary>Công đoạn may đang dùng của các mã hàng trong một đơn, để tổ trưởng chọn khi phân công / ghi hàng hỏng. Không kèm đơn giá.</summary>
public sealed record OperationOptionDto(
    int OperationId, int ProductId, string ProductCode, string OperationCode, string OperationName,
    short StationSequence, bool IsFinalStep);

// ================= KI-01: nhập công đoạn từ file Excel quy trình công nghệ =================

/// <summary>Một dòng "bước công việc" đọc từ sheet quy trình. <c>ParseError</c> có khi ô không đọc được hoặc sai giá trị.</summary>
public sealed record OperationExcelRowDto(
    int RowNumber, int Stt, string? Name, byte? SkillLevel, decimal? StandardTimeSec, string? Equipment, decimal? Rate,
    string? ParseError);

/// <summary>Một sheet quy trình công nghệ trong file (có cột "BƯỚC CÔNG VIỆC" và "TGCT").</summary>
public sealed record OperationExcelSheetDto(
    string SheetName, string? ProductText, bool HasRateColumn, IReadOnlyList<OperationExcelRowDto> Rows);

/// <summary>Action nhận "New" | "Update" | "Unchanged" | "Error".</summary>
public sealed record OperationImportRowDto(
    int RowNumber, int Stt, string OperationCode, string? OperationName, byte? SkillLevel, decimal? StandardTimeSec,
    string? Equipment, decimal? Rate, string Action, string? Error, string RateAction = "None", string? RateNote = null);

public sealed record OperationImportPreviewDto(
    string SheetName, IReadOnlyList<string> SheetNames, string? ProductText, int NewCount, int UpdateCount,
    int UnchangedCount, int ErrorCount, bool ProductHasFinalStep, bool HasRateColumn, int RateNewCount,
    int RateUpdateCount, int RateUnchangedCount, int RateSkippedCount, IReadOnlyList<OperationImportRowDto> Rows);

public sealed record OperationImportResultDto(
    int Created, int Updated, int Unchanged, int Skipped, int RateCreated, int RateUpdated, int RateUnchanged, int RateSkipped);
