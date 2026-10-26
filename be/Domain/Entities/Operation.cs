using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Công đoạn
/// </summary>
public partial class Operation
{
    public int OperationId { get; set; }

    public int ProductId { get; set; }

    public int? OperationGroupId { get; set; }

    public string OperationCode { get; set; } = null!;

    public string OperationName { get; set; } = null!;

    public short StationSequence { get; set; }

    public byte? SkillLevel { get; set; }

    public decimal? StandardTimeSec { get; set; }

    public string TeamType { get; set; } = null!;

    public bool IsFinalStep { get; set; }

    public bool UsesSemiFinished { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<DefectRecord> DefectRecords { get; set; } = new List<DefectRecord>();

    public virtual ICollection<OperationAssignment> OperationAssignments { get; set; } = new List<OperationAssignment>();

    public virtual ICollection<OperationComponent> OperationComponents { get; set; } = new List<OperationComponent>();

    public virtual OperationGroup? OperationGroup { get; set; }

    public virtual ICollection<OutputRecord> OutputRecords { get; set; } = new List<OutputRecord>();

    public virtual ICollection<PieceRate> PieceRates { get; set; } = new List<PieceRate>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<WorkAllocation> WorkAllocations { get; set; } = new List<WorkAllocation>();
}
