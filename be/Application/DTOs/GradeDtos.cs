using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record GradeLevelDto(int GradeLevelId, string GradeCode, string GradeName, decimal MonthlyAmount,
    DateOnly EffectiveDate, bool IsCurrent);

/// <summary>Đổi tiền bậc = thêm dòng mới với ngày hiệu lực mới, không sửa dòng cũ (lịch sử).</summary>
public sealed record SaveGradeLevelDto(string GradeCode, string GradeName, decimal MonthlyAmount, DateOnly EffectiveDate);

public sealed record GradeEvaluationDto(
    int GradeEvaluationId, int WorkerId, string WorkerCode, string WorkerName, int? TeamId, string? TeamName,
    string ProposedByName, string? CurrentGradeCode, string ProposedGradeCode, string Reason, int RecentDefectCount,
    DateOnly EvaluationDate, string Status, DateOnly? EffectiveDate, DateTime? ConsideredAt, string? DecisionNote,
    DateTime CreatedAt);

public sealed record GradeWorkerDto(int WorkerId, string WorkerCode, string FullName, string? CurrentGradeCode,
    int RecentDefectCount, bool HasPending);

/// <summary>Tổ trưởng / tổ phó đề xuất bậc cho công nhân của tổ mình; số lỗi gần đây do hệ thống tính.</summary>
public sealed record ProposeGradeDto(int WorkerId, string ProposedGradeCode, string Reason);

/// <summary>Áp dụng: bậc mới có hiệu lực từ EffectiveDate (mặc định hôm nay). Không áp dụng: bắt buộc DecisionNote.</summary>
public sealed record ApplyGradeDto(DateOnly? EffectiveDate, string? DecisionNote);

public sealed record GradeEvaluationQuery : PageQuery
{
    public string? Status { get; init; }
    public int? WorkerId { get; init; }
}
