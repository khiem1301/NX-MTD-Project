using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwTeamPieceHolding
{
    public int SewingTeamId { get; set; }

    public int? OrderLineId { get; set; }

    public int? ReceivedFromWarehouse { get; set; }

    public int HandedToWorkers { get; set; }

    public int RetrievedFromWorkers { get; set; }

    public int? HeldByTeam { get; set; }
}
