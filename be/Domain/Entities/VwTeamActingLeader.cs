using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwTeamActingLeader
{
    public int TeamId { get; set; }

    public string TeamCode { get; set; } = null!;

    public string TeamType { get; set; } = null!;

    public int LeaderEmployeeId { get; set; }

    public int DeputyEmployeeId { get; set; }

    public string DeputyName { get; set; } = null!;

    public DateOnly? WorkDate { get; set; }

    public bool? LeaderCheckedIn { get; set; }

    public bool? IsActingLeader { get; set; }
}
