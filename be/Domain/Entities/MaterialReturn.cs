using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phiếu trả NVL / phế phẩm / BTP của tổ cắt, tổ may về kho (DEV-ALL-112)
/// </summary>
public partial class MaterialReturn
{
    public int MaterialReturnId { get; set; }

    public string ReturnCode { get; set; } = null!;

    /// <summary>Chỉ có ở phiếu trả cũ (trước DEV-ALL-112) gắn theo phiếu cấp.</summary>
    public int? MaterialIssueId { get; set; }

    public int ReturningTeamId { get; set; }

    public string WarehouseType { get; set; } = null!;

    public int? OrderId { get; set; }

    public int? WorkOrderId { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public int? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string? RejectReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee? ConfirmedByNavigation { get; set; }

    public virtual MaterialIssue? MaterialIssue { get; set; }

    public virtual Team ReturningTeam { get; set; } = null!;

    public virtual CustomerOrder? Order { get; set; }

    public virtual WorkOrder? WorkOrder { get; set; }

    public virtual ICollection<MaterialReturnLine> MaterialReturnLines { get; set; } = new List<MaterialReturnLine>();
}
