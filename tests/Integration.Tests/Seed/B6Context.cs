using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>ID động của S0 Batch 6 (nhập kho NVL & thiếu giao). Supplier/Material/Order khác lấy từ B2Context + B4Context + B5Context.</summary>
public sealed class B6Context
{
    public Account Wh2 { get; set; } = null!;              // thủ kho thứ hai (TK_WK2)

    public CustomerOrder Od6 { get; set; } = null!;        // OD2 của tài liệu: IN_PRODUCTION
    public CustomerOrder Od7 { get; set; } = null!;        // đơn khác, có OM9
    public OrderMaterial Om1 { get; set; } = null!;        // Od6, Ma2, cần 150 / khách cấp 150 → CUSTOMER
    public OrderMaterial Om2 { get; set; } = null!;        // Od6, Ma3, cần 500 / khách cấp 200 → BOTH, PurchaseQty 300
    public OrderMaterial Om9 { get; set; } = null!;        // Od7, Ma1

    public PurchaseOrder Po1 { get; set; } = null!;        // ORDERED, Od6
    public PurchaseOrder Po2 { get; set; } = null!;        // ORDERED, dự trữ
    public PurchaseOrder Po3 { get; set; } = null!;        // ORDERED, dự trữ: nguồn các phiếu gốc của SD2/SD3/SD9
    public PurchaseOrderLine Pol5 { get; set; } = null!;   // Po3, Ma1, 500
    public PurchaseOrder Po5 { get; set; } = null!;        // CLOSED_SHORT: nguồn phiếu gốc của SD3 (đã chốt không bù)
    public PurchaseOrderLine Pol6 { get; set; } = null!;   // Po5, Ma1, 50, NotReplenishedQty 10
    public PurchaseOrder Po4 { get; set; } = null!;        // REJECTED (đã hủy)
    public PurchaseOrderLine Pol1 { get; set; } = null!;   // Po1, Om2/Ma3, 300
    public PurchaseOrderLine Pol3 { get; set; } = null!;   // Po2, Ma1, 200
    public PurchaseOrderLine Pol4 { get; set; } = null!;   // Po4, Ma1, 100

    public MaterialReceipt Mr1 { get; set; } = null!;      // CUSTOMER Od6, PENDING, 1 dòng Om1 = 100
    public MaterialReceipt Mr2 { get; set; } = null!;      // SUPPLIER Po2, PENDING, 1 dòng Pol3 = 150 (thiếu 50)
    public MaterialReceipt Mr3 { get; set; } = null!;      // SUPPLIER Po1, CONFIRMED, đã sinh SD1
    public MaterialReceipt Mr4 { get; set; } = null!;      // CUSTOMER Od6, RETURNED
    public MaterialReceipt Mr5 { get; set; } = null!;      // SUPPLIER Po2, CONFIRMED, gốc của SD2
    public MaterialReceipt Mr6 { get; set; } = null!;      // SUPPLIER Po2, CONFIRMED, gốc của SD3
    public MaterialReceipt Mr9 { get; set; } = null!;      // SUPPLIER Po2, CONFIRMED, gốc của SD9
    public MaterialReceiptLine Mrl1 { get; set; } = null!;
    public MaterialReceiptLine Mrl2 { get; set; } = null!;
    public MaterialReceiptLine Mrl3 { get; set; } = null!;
    public MaterialReceiptLine Mrl4 { get; set; } = null!;
    public MaterialReceiptLine Mrl5 { get; set; } = null!;
    public MaterialReceiptLine Mrl6 { get; set; } = null!;
    public MaterialReceiptLine Mrl9 { get; set; } = null!;

    public Request Sd1 { get; set; } = null!;              // chưa xử lý, RL1 thiếu 100
    public Request Sd2 { get; set; } = null!;              // REPLENISH, RL2 giao bù 40, đã bù 0
    public Request Sd3 { get; set; } = null!;              // NO_REPLENISH, đã đóng
    public Request Sd9 { get; set; } = null!;              // chưa xử lý, mục tiêu chéo dữ liệu
    public RequestLine Rl1 { get; set; } = null!;
    public RequestLine Rl2 { get; set; } = null!;
    public RequestLine Rl3 { get; set; } = null!;
    public RequestLine Rl9 { get; set; } = null!;

    public Attachment At2 { get; set; } = null!;           // PurchaseOrder (B5 Po3), PAYMENT_PROOF
    public Attachment At3 { get; set; } = null!;           // MaterialReceipt Mr1, IsCurrent = 0
}
