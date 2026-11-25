using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>ID động của S0 Batch 5 (mua hàng): PO1–PO6, POL, PP. Supplier/Material/Order lấy từ B2Context + B4Context.</summary>
public sealed class B5Context
{
    public PurchaseOrder Po1 { get; set; } = null!;      // OD2, SU1, PENDING_FINALISATION/UNPAID, 2 dòng, 2 ngày giao
    public PurchaseOrder Po2 { get; set; } = null!;      // dự trữ, SU2, ORDERED/UNPAID, tổng 9.600.000
    public PurchaseOrder Po3 { get; set; } = null!;      // dự trữ, SU1, ORDERED/PARTIALLY_PAID, PP1 (30h) + PP2 (1h)
    public PurchaseOrder Po4 { get; set; } = null!;      // đã hủy (REJECTED)
    public PurchaseOrder Po5 { get; set; } = null!;      // PARTIALLY_RECEIVED, có MaterialReceiptLine
    public PurchaseOrder Po6 { get; set; } = null!;      // ORDERED/PAID, PP9 (1h)

    public PurchaseOrderLine Pol1 { get; set; } = null!; // PO1, OM1/MA1, 100 × 50000
    public PurchaseOrderLine Pol2 { get; set; } = null!; // PO1, OM2/MA3, 500 × 2000
    public PurchaseOrderLine Pol3 { get; set; } = null!; // PO2, MA1, 200 × 48000
    public PurchaseOrderLine Pol31 { get; set; } = null!; // PO3, MA1, 100 × 100000
    public PurchaseOrderLine Pol5 { get; set; } = null!; // PO5, MA1
    public PurchaseOrderLine Pol6 { get; set; } = null!; // PO6, MA1, 40 × 50000

    public PurchasePayment Pp1 { get; set; } = null!;    // PO3, 30 giờ trước, 5.000.000
    public PurchasePayment Pp2 { get; set; } = null!;    // PO3, 1 giờ trước, 3.000.000
    public PurchasePayment Pp9 { get; set; } = null!;    // PO6, 1 giờ trước, 2.000.000
}
