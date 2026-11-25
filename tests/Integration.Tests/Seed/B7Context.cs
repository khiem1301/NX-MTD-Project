using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>ID động của S0 Batch 7 (lệnh cắt, bàn cắt, bàn giao BTP). OD2/OL3/OL4 là đơn riêng của batch (Đen M 200 / Đen L 100); PR1/PCM1 lấy từ B2Context.</summary>
public sealed class B7Context
{
    public Team TCut02 { get; set; } = null!;
    public Team TCutOld { get; set; } = null!;
    public Account Td1 { get; set; } = null!;      // TeamDeputy T_CUT01
    public Account Tl2 { get; set; } = null!;      // TeamLeader T_CUT02
    public Account TlNone { get; set; } = null!;   // TeamLeader không tổ

    public CustomerOrder Od2 { get; set; } = null!;
    public OrderLine Ol3 { get; set; } = null!;
    public OrderLine Ol4 { get; set; } = null!;
    public ProductComponentMaterial Pcm1 { get; set; } = null!;
    public ProductComponentMaterial Pcm2 { get; set; } = null!;

    public WorkOrder Wo1 { get; set; } = null!;
    public WorkOrder Wo2 { get; set; } = null!;
    public WorkOrder Wo3 { get; set; } = null!;
    public WorkOrder Wo4 { get; set; } = null!;
    public WorkOrder Wo5 { get; set; } = null!;
    public WorkOrder Wo6 { get; set; } = null!;
    public WorkOrderLine Wol1 { get; set; } = null!;
    public WorkOrderLine Wol2 { get; set; } = null!;
    public WorkOrderLine Wol3 { get; set; } = null!;
    public WorkOrderLine Wol9 { get; set; } = null!;

    public CuttingLay Cl1 { get; set; } = null!;
    public CuttingLay ClLk { get; set; } = null!;
    public CuttingLay Cl9 { get; set; } = null!;

    public CutHandover Ch1 { get; set; } = null!;
    public CutHandover Ch2 { get; set; } = null!;
    public CutHandover Ch9 { get; set; } = null!;
    public CutHandover ChRej { get; set; } = null!;
    public CutHandover ChCan { get; set; } = null!;
    public CutHandoverLine Chl1 { get; set; } = null!;
    public CutHandoverLine Chl9 { get; set; } = null!;

    public Request Rq1 { get; set; } = null!;
    public Request Rq2 { get; set; } = null!;
}
