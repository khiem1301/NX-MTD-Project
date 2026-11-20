namespace NX_MTD.Application.DTOs;

/// <summary>
/// DEV-ALL-148: số liệu của một công nhân trong kỳ xét bậc, do hệ thống tổng hợp từ bảng công, sản lượng công đoạn và phiếu lỗi.
/// Không chứa tiền (đơn giá, lương) để không gửi ra ngoài.
/// </summary>
/// <param name="WorkDays">Số ngày có công (&gt; 0).</param>
/// <param name="WorkdayTotal">Tổng ngày công quy đổi (1 công = 8 giờ trong ca).</param>
/// <param name="OutputQty">Sản lượng công đoạn đã trừ phần nhập sai (OUTPUT_ERR).</param>
/// <param name="OperationCount">Số công đoạn khác nhau đã làm (mức đa năng).</param>
/// <param name="AvgSkillLevel">Độ khó trung bình của công đoạn, theo sản lượng (null nếu công đoạn chưa có độ khó).</param>
/// <param name="EfficiencyPercent">Giờ chuẩn làm ra (SL × thời gian chuẩn) / giờ làm thực × 100.</param>
/// <param name="DefectRatePercent">(SL Rework + SL Scrap) / sản lượng × 100.</param>
public sealed record GradeWorkerMetricsDto(
    int TenureMonths, int WorkDays, decimal WorkdayTotal, decimal WorkedHours, decimal OvertimeHours,
    int OutputQty, int OperationCount, decimal? AvgSkillLevel, decimal? EfficiencyPercent,
    int ReworkQty, int ScrapQty, decimal? DefectRatePercent);

/// <summary>
/// Một dòng gợi ý. <c>AiGradeCode</c> / <c>AiReason</c> là bản AI trả; <c>ProposedGradeCode</c> / <c>Reason</c> là bản tổ trưởng
/// sẽ gửi (ban đầu bằng bản AI, đổi khi tổ trưởng sửa). <c>PendingGradeCode</c>: bậc của đề xuất đang chờ quyết định (nếu có).
/// </summary>
public sealed record GradeSuggestionItemDto(
    int WorkerId, string WorkerCode, string FullName, string? CurrentGradeCode, string? PendingGradeCode,
    GradeWorkerMetricsDto Metrics, string? AiGradeCode, string AiReason, string? ProposedGradeCode, string Reason, bool IsEdited);

/// <summary>Bản gợi ý AI của một tổ, lưu tạm đến <c>ExpiresAt</c> để tổ trưởng xem lại / sửa / xác nhận.</summary>
public sealed record GradeSuggestionDto(
    int TeamId, DateOnly PeriodFrom, DateOnly PeriodTo, DateTime GeneratedAt, DateTime ExpiresAt, string GeneratedByName,
    DateTime? ConfirmedAt, IReadOnlyList<GradeSuggestionItemDto> Items);

/// <summary>Tạo / tạo lại gợi ý: tổ trưởng chọn số ngày gần nhất lấy số liệu (công, sản lượng, lỗi).</summary>
public sealed record GenerateGradeSuggestionDto(int LookbackDays);

/// <summary>Tổ trưởng sửa bậc / lý do của một dòng gợi ý.</summary>
public sealed record UpdateGradeSuggestionItemDto(string ProposedGradeCode, string Reason);

/// <summary>Kết quả xác nhận: số đề xuất tạo mới, số đề xuất đang chờ bị thay, số dòng giữ nguyên bậc, các dòng bị bỏ qua.</summary>
public sealed record GradeSuggestionConfirmDto(int Created, int Replaced, int Unchanged, IReadOnlyList<string> Skipped);

// ---------- Dữ liệu gửi AI (đã ẩn danh) ----------

/// <summary>Công nhân gửi AI: chỉ có khóa tạm (W1, W2...), bậc hiện tại và số liệu; không có tên, mã nhân viên, tổ.</summary>
public sealed record GradeAdvisorWorker(string Key, string? CurrentGrade, GradeWorkerMetricsDto Metrics);

/// <summary>Bậc gửi AI: mã + thứ hạng (1 = bậc cao nhất theo tiền bậc đang hiệu lực; không gửi số tiền).</summary>
public sealed record GradeAdvisorGrade(string Code, int Rank);

public sealed record GradeAdvisorRequest(int PeriodDays, IReadOnlyList<GradeAdvisorGrade> Grades, IReadOnlyList<GradeAdvisorWorker> Workers);

public sealed record GradeAdvice(string Key, string GradeCode, string Reason);
