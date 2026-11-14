namespace NX_MTD.Domain.Entities;

/// <summary>
/// Một ngày dự kiến giao trong lịch giao NVL: số lần giao = số dòng (DEV-ALL-113). Chủ lịch là đúng một trong ba: đơn mua
/// (<see cref="PurchaseOrderId"/>), đơn hàng khách giao (<see cref="OrderId"/>) hoặc đơn thiếu NVL giao bù (<see cref="RequestId"/>).
/// </summary>
public partial class DeliveryScheduleDate
{
    public int DeliveryScheduleDateId { get; set; }

    public int? PurchaseOrderId { get; set; }

    public int? OrderId { get; set; }

    public int? RequestId { get; set; }

    /// <summary>Lần giao thứ mấy (từ 1).</summary>
    public short SeqNo { get; set; }

    public DateOnly DeliveryDate { get; set; }

    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    public virtual CustomerOrder? Order { get; set; }

    public virtual Request? Request { get; set; }
}
