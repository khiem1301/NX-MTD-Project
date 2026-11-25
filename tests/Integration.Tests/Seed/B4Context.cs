using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>ID động của S0 Batch 4 (đơn hàng khách, phân tổ, NVL đơn): OD1–OD5, OL1–OL3/OL9, MK1, OM1–OM2. PR1/PR2/Cu/Ma/Team lấy từ B2Context + S0Context.</summary>
public sealed class B4Context
{
    public ProductColor Pr1White { get; set; } = null!;
    public ProductVariant Pv3 { get; set; } = null!;      // PR1 Trắng M
    public ProductVariant Pv9 { get; set; } = null!;      // PR2 Xanh M

    public CustomerOrder Od1 { get; set; } = null!;       // DH-0001 NEW / AWAITING
    public CustomerOrder Od2 { get; set; } = null!;       // DH-0002 IN_PRODUCTION / PARTIAL
    public CustomerOrder Od3 { get; set; } = null!;       // DH-0003 CANCELLED
    public CustomerOrder Od4 { get; set; } = null!;       // DH-0004 NEW (PR2)
    public CustomerOrder Od5 { get; set; } = null!;       // DH-0005 STOPPED

    public OrderLine Ol1 { get; set; } = null!;           // OD1, PV1 (Đen M) 100 × 25000
    public OrderLine Ol2 { get; set; } = null!;           // OD1, PV2 (Đen L) 50
    public OrderLine Ol3 { get; set; } = null!;           // OD2, PV3 (Trắng M) 200
    public OrderLine Ol9 { get; set; } = null!;           // OD4, PV9
    public OrderLine Ol3Cancelled { get; set; } = null!;  // OD3, PV1 — dòng của đơn đã hủy (IT_B4_089)

    public OrderLineTeam Olt3 { get; set; } = null!;      // OL3 → T_SEW01
    public ProductMarker Mk1 { get; set; } = null!;       // OD2, PR1, 1, "S1", 5.20 × 1.50, ratio M:2
    public OrderMaterial Om1 { get; set; } = null!;       // OD2, MA1, 150 / 0, đã có MaterialReceiptLine
    public OrderMaterial Om2 { get; set; } = null!;       // OD2, MA3, 100 / 100
    public DispatchNote DnDelivered { get; set; } = null!; // OL3: 80 DELIVERED
    public DispatchNote DnPending { get; set; } = null!;   // OL3: 30 chưa DELIVERED
}
