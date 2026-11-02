namespace NX_MTD.Domain.Entities;

/// <summary>Quản đốc, Admin chia một dòng đơn (mã hàng × màu × size) cho một tổ may. Tổ cắt chỉ có một tổ nên không chia.</summary>
public partial class OrderLineTeam
{
    public int OrderLineTeamId { get; set; }

    public int OrderLineId { get; set; }

    public int SewingTeamId { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual Team SewingTeam { get; set; } = null!;
}
