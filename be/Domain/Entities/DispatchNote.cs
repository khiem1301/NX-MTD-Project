using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phiếu xuất kho thành phẩm (kiêm lệnh giao hàng)
/// </summary>
public partial class DispatchNote
{
    public int DispatchNoteId { get; set; }

    public string DispatchCode { get; set; } = null!;

    public int OrderId { get; set; }

    public DateOnly DeliveryDate { get; set; }

    public string DeliveryAddress { get; set; } = null!;

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? RejectReason { get; set; }

    public int? DispatchedBy { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateOnly? CustomerReceivedDate { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual ICollection<DispatchNoteLine> DispatchNoteLines { get; set; } = new List<DispatchNoteLine>();

    public virtual Employee? DispatchedByNavigation { get; set; }

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual ICollection<VehicleTrip> VehicleTrips { get; set; } = new List<VehicleTrip>();
}
