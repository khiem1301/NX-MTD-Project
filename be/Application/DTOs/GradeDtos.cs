using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record GradeLevelDto(int GradeLevelId, string GradeCode, string GradeName, decimal MonthlyAmount,
    DateOnly EffectiveDate, bool IsCurrent);

/// <summary>Đổi tiền bậc = thêm dòng mới với ngày hiệu lực mới, không sửa dòng cũ (lịch sử).</summary>
/// <summary>Một bậc thợ trong danh mục. InUse = đã có nhân viên, đánh giá hoặc tiền bậc dùng (chỉ ngừng dùng được, không xóa).</summary>
public sealed record GradeCodeDto(string GradeCode, string GradeName, short SortOrder, string Status, bool InUse);

public sealed record SaveGradeCodeDto(string GradeCode, string GradeName, short SortOrder);

public sealed record UpdateGradeCodeDto(string GradeName, short SortOrder, string Status);

/// <summary>Sửa phiên bản tiền bậc chưa có hiệu lực (phiên bản đã hiệu lực là lịch sử lương, không sửa).</summary>
public sealed record UpdateGradeLevelDto(string GradeName, decimal MonthlyAmount, DateOnly EffectiveDate);

public sealed record SaveGradeLevelDto(string GradeCode, string GradeName, decimal MonthlyAmount, DateOnly EffectiveDate);

public sealed record GradeEvaluationDto(
    int GradeEvaluationId, int WorkerId, string WorkerCode, string WorkerName, int? TeamId, string? TeamName,
    string ProposedByName, string? CurrentGradeCode, string ProposedGradeCode, string Reason, int RecentDefectCount,
    DateOnly EvaluationDate, string Status, DateOnly? EffectiveDate, DateTime? ConsideredAt, string? DecisionNote,
    DateTime CreatedAt);

public sealed record GradeWorkerDto(int WorkerId, string WorkerCode, string FullName, string? CurrentGradeCode,
    int RecentDefectCount, bool HasPending, string? TeamName = null, int? TeamId = null);

/// <summary>Admin / Quản đốc đặt bậc trực tiếp cho công nhân, không cần tổ trưởng đề xuất. Mặc định có hiệu lực từ hôm nay.</summary>
public sealed record SetGradeDto(int WorkerId, string GradeCode, string Reason, DateOnly? EffectiveDate);

/// <summary>Tổ trưởng / tổ phó đề xuất bậc cho công nhân của tổ mình; số lỗi gần đây do hệ thống tính.</summary>
public sealed record ProposeGradeDto(int WorkerId, string ProposedGradeCode, string Reason);

/// <summary>Áp dụng: bậc mới có hiệu lực từ EffectiveDate (mặc định hôm nay). Không áp dụng: bắt buộc DecisionNote.</summary>
public sealed record ApplyGradeDto(DateOnly? EffectiveDate, string? DecisionNote);

public sealed record GradeEvaluationQuery : PageQuery
{
    public string? Status { get; init; }
    public int? WorkerId { get; init; }
}
