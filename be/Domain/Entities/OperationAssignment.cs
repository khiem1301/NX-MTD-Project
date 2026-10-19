using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phân công công đoạn
/// </summary>
public partial class OperationAssignment
{
    public int OperationAssignmentId { get; set; }

    public int SewingTeamId { get; set; }

    public int OrderId { get; set; }

    public int OperationId { get; set; }

    public int WorkerId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public int? CopiedFromAssignmentId { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual OperationAssignment? CopiedFromAssignment { get; set; }

    public virtual ICollection<OperationAssignment> InverseCopiedFromAssignment { get; set; } = new List<OperationAssignment>();

    public virtual Operation Operation { get; set; } = null!;

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual Team SewingTeam { get; set; } = null!;

    public virtual Employee Worker { get; set; } = null!;
}
