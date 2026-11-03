using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>Chỉ tiêu KPI tổ may — dùng chung cho danh sách và chi tiết.
/// ActualFinishedQty = Σ hàng hoàn thiện cả kỳ (vw_KpiDailyResult), PercentAchieved = thực tế / chỉ tiêu cả kỳ × 100 (MI-17).
/// Lines = chỉ tiêu theo màu × size (DEV-ALL-62); rỗng ở chỉ tiêu cũ chỉ có số tổng.</summary>
public sealed record KpiTargetDto(
    int KpiTargetId, int OrderId, string OrderCode, int ProductId, string ProductCode, string ProductName,
    int SewingTeamId, string SewingTeamName, DateOnly StartDate, DateOnly EndDate, int TargetFinishedQty,
    short WorkingDays, decimal DailyTarget, string Status, string? Result, DateTime CreatedAt,
    int ActualFinishedQty = 0, decimal PercentAchieved = 0m, IReadOnlyList<KpiTargetLineDto>? Lines = null);

/// <summary>Chỉ tiêu của một dòng đơn (màu × size); ActualQty = QC đạt của tổ ở dòng đó trong kỳ.</summary>
public sealed record KpiTargetLineDto(int OrderLineId, string Color, string SizeCode, int TargetQty, int ActualQty);

/// <summary>Tạo mới / sửa chỉ tiêu KPI (sửa chỉ cho phép khi Status = ACTIVE). Chỉ tiêu cả kỳ = Σ số của các dòng (màu × size).
/// WorkingDays do BE tính: số ngày đã đặt KPI, chưa đặt thì số ngày trong kỳ trừ Chủ nhật.</summary>
public sealed record SaveKpiTargetDto(
    int OrderId, int ProductId, int SewingTeamId, DateOnly StartDate, DateOnly EndDate,
    IReadOnlyList<SaveKpiTargetLineDto> Lines);

public sealed record SaveKpiTargetLineDto(int OrderLineId, int TargetQty);

/// <summary>
/// Số đặt được cho từng dòng đơn (màu × size) của mã hàng mà tổ được chia: số đặt − phần đã giao cho tổ khác ở các chỉ tiêu có
/// chia màu × size (chỉ tiêu đang chạy tính số giao, đã đóng tính QC đạt của dòng, đã hủy không tính). ActualQty = QC đạt của
/// chỉ tiêu đang sửa ở dòng đó, chỉ tiêu mới thì 0 (sửa không được hạ dưới số này).
/// </summary>
public sealed record KpiLineCapacityDto(int OrderLineId, string Color, string SizeCode, int OrderedQty, int AssignedToOthers,
    int MaxTarget, int ActualQty);

/// <summary>
/// Chỉ tiêu tối đa lập được cho (đơn, mã hàng): số lượng đơn đặt − phần đã giao ở các chỉ tiêu khác, kể cả của cùng tổ (chỉ tiêu
/// đang chạy tính cả chỉ tiêu, chỉ tiêu đã đóng tính số thực tế làm được, chỉ tiêu đã hủy không tính). DueDate và DueWarningDays
/// để form cảnh báo kỳ kết thúc sau hoặc sát ngày giao hàng; OrderClosed = đơn đã đóng, không đặt chỉ tiêu được.
/// </summary>
public sealed record KpiOrderCapacityDto(int OrderQty, int AssignedToOthers, int MaxTarget, DateOnly? DueDate = null,
    int DueWarningDays = 0, string? OrderStatus = null, bool OrderClosed = false);

/// <summary>Quyết định tăng ca khi KPI không đạt (kết thúc đơn của tổ).</summary>
public static class OvertimeDecisions
{
    public const string None = "None";
    public const string AutoApproved = "AutoApproved";
    public const string LeaderDecides = "LeaderDecides";
}

/// <summary>Một công nhân bị lệch ở một dòng đơn: số báo ở công đoạn cuối và phần lệch (ghi phiếu hàng hỏng nếu &gt; 0).</summary>
public sealed record EndOfOrderRowDto(int OrderLineId, string Color, string SizeCode, int WorkerId, string WorkerCode,
    string WorkerName, int ReportedQty, int MismatchQty);

/// <summary>Xem trước (và kết quả) kết thúc đơn của một tổ: chốt KPI, lệch kiểm cuối đơn theo công nhân, tăng ca.</summary>
public sealed record EndOfOrderPreviewDto(int KpiTargetId, string OrderCode, string ProductCode, string TeamName,
    int TargetQty, int ActualQty, bool Achieved, int ShortfallQty, decimal ShortfallPct, int ThresholdPct, string OtDecision,
    string? FinalOperationName, IReadOnlyList<EndOfOrderRowDto> Rows, IReadOnlyList<string> Blockers, bool CanFinish);

public sealed record KpiTargetQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
    public int? SewingTeamId { get; init; }
}

/// <summary>
/// MI-18 "KPI theo ngày": một dòng kết quả theo ngày của một chỉ tiêu KPI, chiếu thẳng từ view
/// vw_KpiDailyResult (xem VwKpiDailyResult). View chỉ có dòng cho ngày có QC nhập sản lượng (CROSS APPLY
/// trong SQL) — ngày không có dòng nghĩa là ngày đó chưa có hàng hoàn thiện, không phải lỗi dữ liệu.
/// </summary>
public sealed record KpiDailyResultDto(
    int KpiTargetId, int SewingTeamId, int OrderId, int ProductId, DateOnly WorkDate,
    decimal? DailyTarget, int? FinishedQty, int DefectQty, decimal? DefectRatePct, decimal? PercentAchieved);
