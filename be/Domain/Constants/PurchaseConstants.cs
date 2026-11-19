namespace NX_MTD.Domain.Constants;

/// <summary>PurchaseOrder.GoodsStatus (CK_PO_GoodsStatus). PENDING_FINALISATION nằm ở
/// <see cref="PurchaseOrderStatuses.PendingFinalisation"/> (Thái, TH-12).</summary>
public static class PurchaseGoodsStatuses
{
    public const string Ordered = "ORDERED";
    public const string PartiallyReceived = "PARTIALLY_RECEIVED";
    public const string Received = "RECEIVED";
    public const string ClosedShort = "CLOSED_SHORT";
    public const string CustomerSupplied = "CUSTOMER_SUPPLIED";
    public const string Rejected = "REJECTED";

    /// <summary>Đã đặt hàng với NCC: có thể thanh toán, không còn sửa NCC / số lượng (GBR-18).</summary>
    public static readonly IReadOnlySet<string> Placed = new HashSet<string>
    {
        Ordered, PartiallyReceived, Received, ClosedShort,
    };

    /// <summary>Kế toán nhập được giá trị thực tế (DEV-ALL-117): đang nhận hàng (NCC giao thiếu, còn giao thêm) hoặc đã chốt không bù.</summary>
    public static readonly IReadOnlySet<string> ActualAmountEditable = new HashSet<string> { PartiallyReceived, ClosedShort };

    /// <summary>Còn có thể sửa đơn giá khi Kế toán trả lại (hàng chưa nhận đủ).</summary>
    public static readonly IReadOnlySet<string> Repriceable = new HashSet<string> { Ordered, PartiallyReceived };
}

/// <summary>PurchaseOrder.PaymentStatus (CK_PO_PaymentStatus). UNPAID dùng lại
/// <see cref="PurchaseOrderStatuses.Unpaid"/>.</summary>
public static class PurchasePaymentStatuses
{
    public const string Paid = "PAID";
    public const string PartiallyPaid = "PARTIALLY_PAID";
    public const string Returned = "RETURNED";
}

/// <summary>Quy tắc các lần thanh toán đơn mua (DEV-ALL-117).</summary>
public static class PurchasePaymentRules
{
    /// <summary>Sửa được một lần thanh toán trong ngần này giờ kể từ lúc nhập.</summary>
    public const int EditWindowHours = 24;
}

/// <summary>Trạng thái thanh toán của đơn hàng khách (DEV-ALL-118), tính từ giá trị chốt và các lần thanh toán, không lưu cột.</summary>
public static class OrderPaymentStates
{
    public const string NotSet = "NotSet";
    public const string Unpaid = "Unpaid";
    public const string Partial = "Partial";
    public const string Settled = "Settled";
}

/// <summary>PurchaseOrder.PaymentMethod (CK_PO_PaymentMethod).</summary>
public static class PaymentMethods
{
    public const string BankTransfer = "BANK_TRANSFER";
    public const string Cash = "CASH";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { BankTransfer, Cash };
}
