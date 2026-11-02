using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Bàn cắt
/// </summary>
public partial class CuttingLay
{
    public int CuttingLayId { get; set; }

    public int WorkOrderId { get; set; }

    public int CuttingTeamId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string ShiftCode { get; set; } = null!;

    public short LaySequence { get; set; }

    public int FabricMaterialId { get; set; }

    public short PlyCount { get; set; }

    /// <summary>Chiều dài bàn cắt (mét); null = bàn nhập trước khi có cột này.</summary>
    public decimal? LayLengthM { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CuttingLayLine> CuttingLayLines { get; set; } = new List<CuttingLayLine>();

    public virtual Team CuttingTeam { get; set; } = null!;

    public virtual Material FabricMaterial { get; set; } = null!;

    public virtual WorkOrder WorkOrder { get; set; } = null!;
}
