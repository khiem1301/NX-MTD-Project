using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Tổ (cắt / may / văn phòng)
/// </summary>
public partial class Team
{
    public int TeamId { get; set; }

    public string TeamCode { get; set; } = null!;

    public string TeamName { get; set; } = null!;

    public string TeamType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AnnouncementTarget> AnnouncementTargets { get; set; } = new List<AnnouncementTarget>();

    public virtual ICollection<CutHandover> CutHandovers { get; set; } = new List<CutHandover>();

    public virtual ICollection<CuttingLay> CuttingLays { get; set; } = new List<CuttingLay>();

    public virtual ICollection<DefectRecord> DefectRecords { get; set; } = new List<DefectRecord>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<GradeEvaluation> GradeEvaluations { get; set; } = new List<GradeEvaluation>();

    public virtual ICollection<KpiTarget> KpiTargets { get; set; } = new List<KpiTarget>();

    public virtual ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();

    public virtual ICollection<MaterialReturn> MaterialReturns { get; set; } = new List<MaterialReturn>();

    public virtual ICollection<OperationAssignment> OperationAssignments { get; set; } = new List<OperationAssignment>();

    public virtual ICollection<OutputRecord> OutputRecords { get; set; } = new List<OutputRecord>();

    public virtual ICollection<QcBatch> QcBatches { get; set; } = new List<QcBatch>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<TeamMealConfirmation> TeamMealConfirmations { get; set; } = new List<TeamMealConfirmation>();

    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();

    public virtual ICollection<WorkerPieceMovement> WorkerPieceMovements { get; set; } = new List<WorkerPieceMovement>();
}
