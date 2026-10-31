using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Đề xuất / đổi bậc thợ; dòng APPLIED là lịch sử bậc
/// </summary>
public partial class GradeEvaluation
{
    public int GradeEvaluationId { get; set; }

    public int WorkerId { get; set; }

    public int ProposedBy { get; set; }

    public int? TeamId { get; set; }

    public string? CurrentGradeCode { get; set; }

    public string ProposedGradeCode { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public int RecentDefectCount { get; set; }

    public DateOnly EvaluationDate { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly? EffectiveDate { get; set; }

    public DateTime? ConsideredAt { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual GradeLevelCode? CurrentGradeCodeNavigation { get; set; }

    public virtual Employee ProposedByNavigation { get; set; } = null!;

    public virtual GradeLevelCode ProposedGradeCodeNavigation { get; set; } = null!;

    public virtual Team? Team { get; set; }

    public virtual Employee Worker { get; set; } = null!;
}
