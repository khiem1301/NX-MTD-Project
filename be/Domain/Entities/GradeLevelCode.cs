using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class GradeLevelCode
{
    public string GradeCode { get; set; } = null!;

    public string GradeName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<GradeEvaluation> GradeEvaluationCurrentGradeCodeNavigations { get; set; } = new List<GradeEvaluation>();

    public virtual ICollection<GradeEvaluation> GradeEvaluationProposedGradeCodeNavigations { get; set; } = new List<GradeEvaluation>();

    public virtual ICollection<GradeLevel> GradeLevels { get; set; } = new List<GradeLevel>();
}
