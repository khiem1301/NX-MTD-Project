using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>S0 của IT_B7: tổ T_CUT02/T_CUTOLD, 3 tài khoản tổ cắt, đơn DH-B7-02 (OL3/OL4), lệnh cắt WO1–WO6, bàn cắt CL1/CL_LK/CL9, phiếu bàn giao CH1/CH2/CH9, đơn cắt bù RQ1/RQ2.</summary>
public sealed partial class TestDataSeeder
{
    public async Task<B7Context> SeedBatch7BaseAsync(B2Context b2, B4Context d, S0Context s0, CancellationToken ct = default)
    {
        var k = new B7Context();

        foreach (var o in await _db.CustomerOrders.Where(x => x.OrderCode.StartsWith("DH-B7-")).ToListAsync(ct))
        {
            MovedKeys.Add(("CustomerOrder", "OrderId", o.OrderId, "OrderCode", o.OrderCode));
            o.OrderCode = $"MOVED-{o.OrderId}";
        }

        foreach (var o in await _db.WorkOrders.Where(x => x.WorkOrderCode.StartsWith("LC-B7-")).ToListAsync(ct))
        {
            MovedKeys.Add(("WorkOrder", "WorkOrderId", o.WorkOrderId, "WorkOrderCode", o.WorkOrderCode));
            o.WorkOrderCode = $"MOVED-{o.WorkOrderId}";
        }

        foreach (var o in await _db.CutHandovers.Where(x => x.HandoverCode.StartsWith("HB-B7-")).ToListAsync(ct))
        {
            MovedKeys.Add(("CutHandover", "CutHandoverId", o.CutHandoverId, "HandoverCode", o.HandoverCode));
            o.HandoverCode = $"MOVED-{o.CutHandoverId}";
        }

        foreach (var o in await _db.Requests.Where(x => x.RequestCode.StartsWith("YC-B7-")).ToListAsync(ct))
        {
            MovedKeys.Add(("Request", "RequestId", o.RequestId, "RequestCode", o.RequestCode));
            o.RequestCode = $"MOVED-{o.RequestId}";
        }

        await _db.SaveChangesAsync(ct);

        var hash = _hasher.Hash(S0Context.Password);
        k.TCut02 = await UpsertTeamAsync("T_CUT02", TeamTypes.Cutting, RecordStatus.Active, ct);
        k.TCutOld = await UpsertTeamAsync("T_CUTOLD", TeamTypes.Cutting, RecordStatus.Discontinued, ct);
        k.Td1 = await S0PersonAsync("td_cut01", "S0-B7-TD1", "Tổ Phó Cắt Một", RoleCodes.TeamDeputy, PayTypes.Monthly, s0.TCut01.TeamId, null, hash, ct);
        k.Tl2 = await S0PersonAsync("tl_cut02", "S0-B7-TL2", "Tổ Trưởng Cắt Hai", RoleCodes.TeamLeader, PayTypes.Monthly, k.TCut02.TeamId, null, hash, ct);
        // DB chặn tổ trưởng không thuộc tổ: tắt trigger Employee chỉ trong lúc seed tài khoản bất thường TK_TLNONE.
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Employee DISABLE TRIGGER ALL;", ct);
        try { k.TlNone = await S0PersonAsync("tl_none", "S0-B7-TLN", "Tổ Trưởng Không Tổ", RoleCodes.TeamLeader, PayTypes.Monthly, null, null, hash, ct); }
        finally { await _db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Employee ENABLE TRIGGER ALL;", ct); }

        // --- Đơn DH-B7-02: OL3 (Đen M 200), OL4 (Đen L 100) của PR1 ---
        k.Od2 = Order("DH-B7-02", b2.Cu1, OrderStatuses.InProduction, OrderMaterialStatuses.Partial, null);
        _db.CustomerOrders.Add(k.Od2);
        await _db.SaveChangesAsync(ct);
        k.Ol3 = Line(k.Od2, b2.Pr1VarM, b2.Pr1.ProductId, 200);
        k.Ol4 = Line(k.Od2, b2.Pr1VarL, b2.Pr1.ProductId, 100);
        _db.OrderLines.AddRange(k.Ol3, k.Ol4);

        k.Pcm1 = b2.Pr1Norm;
        k.Pcm2 = new ProductComponentMaterial
        {
            ProductId = b2.Pr1.ProductId, ProductColorId = b2.Pr1Black.ProductColorId, ProductComponentId = b2.Pr1Pocket.ProductComponentId,
            MaterialId = b2.Ma1.MaterialId, Status = RecordStatus.Active,
        };
        _db.ProductComponentMaterials.Add(k.Pcm2);

        var marker = new ProductMarker
        {
            OrderId = k.Od2.OrderId, ProductId = b2.Pr1.ProductId, MarkerNo = 1, MarkerCode = "S1", LengthM = 5.20m, WidthM = 1.50m, Status = RecordStatus.Active,
        };
        _db.ProductMarkers.Add(marker);
        await _db.SaveChangesAsync(ct);
        _db.ProductMarkerRatios.AddRange(
            new ProductMarkerRatio { ProductMarkerId = marker.ProductMarkerId, SizeCode = "M", PiecesPerPly = 2 },
            new ProductMarkerRatio { ProductMarkerId = marker.ProductMarkerId, SizeCode = "L", PiecesPerPly = 1 });

        // --- Vải tổ cắt đang giữ trên đơn (phiếu cấp đã nhận) để bàn cắt không vượt vải còn lại ---
        await AddHeldFabricAsync(k.Od2.OrderId, s0.TCut01.TeamId, b2.Ma1.MaterialId, s0.Wh.EmployeeId, s0.Tl2.EmployeeId, ct);
        await AddHeldFabricAsync(k.Od2.OrderId, k.TCut02.TeamId, b2.Ma1.MaterialId, s0.Wh.EmployeeId, k.Tl2.EmployeeId, ct);

        // --- Đơn cắt bù: RQ1 chưa có lệnh, RQ2 đã có lệnh cắt bù (WO6) ---
        k.Rq1 = B7Request("YC-B7-01", k.Od2.OrderId);
        k.Rq2 = B7Request("YC-B7-02", k.Od2.OrderId);
        _db.Requests.AddRange(k.Rq1, k.Rq2);
        await _db.SaveChangesAsync(ct);
        _db.RequestLines.AddRange(
            new RequestLine { RequestId = k.Rq1.RequestId, OrderLineId = k.Ol3.OrderLineId, ProductComponentMaterialId = k.Pcm1.ProductComponentMaterialId, Quantity = 5m },
            new RequestLine { RequestId = k.Rq2.RequestId, OrderLineId = k.Ol3.OrderLineId, ProductComponentMaterialId = k.Pcm1.ProductComponentMaterialId, Quantity = 5m });

        // --- Lệnh cắt ---
        k.Wo1 = B7Wo("LC-B7-01", k.Od2.OrderId, b2.Pr1.ProductId, s0.TCut01.TeamId, WorkOrderStatuses.Open, 3.00m);
        k.Wo2 = B7Wo("LC-B7-02", k.Od2.OrderId, b2.Pr1.ProductId, s0.TCut01.TeamId, WorkOrderStatuses.Cutting, 0m);
        k.Wo3 = B7Wo("LC-B7-03", k.Od2.OrderId, b2.Pr1.ProductId, k.TCut02.TeamId, WorkOrderStatuses.Open, 0m);
        k.Wo4 = B7Wo("LC-B7-04", k.Od2.OrderId, b2.Pr1.ProductId, s0.TCut01.TeamId, WorkOrderStatuses.Completed, 0m);
        k.Wo5 = B7Wo("LC-B7-05", k.Od2.OrderId, b2.Pr1.ProductId, s0.TCut01.TeamId, WorkOrderStatuses.Cancelled, 0m);
        k.Wo6 = B7Wo("LC-B7-06", k.Od2.OrderId, b2.Pr1.ProductId, s0.TCut01.TeamId, WorkOrderStatuses.Open, 0m);
        k.Wo6.SourceRequestId = k.Rq2.RequestId;
        _db.WorkOrders.AddRange(k.Wo1, k.Wo2, k.Wo3, k.Wo4, k.Wo5, k.Wo6);
        await _db.SaveChangesAsync(ct);

        k.Wol1 = new WorkOrderLine { WorkOrderId = k.Wo1.WorkOrderId, OrderLineId = k.Ol3.OrderLineId, PlannedQty = 120 };
        k.Wol2 = new WorkOrderLine { WorkOrderId = k.Wo1.WorkOrderId, OrderLineId = k.Ol4.OrderLineId, PlannedQty = 60 };
        k.Wol3 = new WorkOrderLine { WorkOrderId = k.Wo2.WorkOrderId, OrderLineId = k.Ol3.OrderLineId, PlannedQty = 50 };
        k.Wol9 = new WorkOrderLine { WorkOrderId = k.Wo3.WorkOrderId, OrderLineId = k.Ol4.OrderLineId, ProductComponentMaterialId = k.Pcm2.ProductComponentMaterialId, PlannedQty = 10 };
        _db.WorkOrderLines.AddRange(k.Wol1, k.Wol2, k.Wol3, k.Wol9,
            new WorkOrderLine { WorkOrderId = k.Wo6.WorkOrderId, OrderLineId = k.Ol3.OrderLineId, ProductComponentMaterialId = k.Pcm1.ProductComponentMaterialId, PlannedQty = 5 });
        await _db.SaveChangesAsync(ct);

        // --- Bàn cắt ---
        k.Cl1 = B7Lay(k.Wo2.WorkOrderId, s0.TCut01.TeamId, "MORNING", CuttingLayStatuses.Recorded);
        k.ClLk = B7Lay(k.Wo2.WorkOrderId, s0.TCut01.TeamId, "AFTERNOON", CuttingLayStatuses.Locked);
        k.ClLk.WorkDate = _t.AddDays(-1);
        k.Cl9 = B7Lay(k.Wo3.WorkOrderId, k.TCut02.TeamId, "MORNING", CuttingLayStatuses.Recorded);
        _db.CuttingLays.AddRange(k.Cl1, k.ClLk, k.Cl9);
        await _db.SaveChangesAsync(ct);
        _db.CuttingLayLines.AddRange(
            new CuttingLayLine { CuttingLayId = k.Cl1.CuttingLayId, OrderLineId = k.Ol3.OrderLineId, ActualQty = 40 },
            new CuttingLayLine { CuttingLayId = k.Cl9.CuttingLayId, OrderLineId = k.Ol4.OrderLineId, ProductComponentMaterialId = k.Pcm2.ProductComponentMaterialId, ActualQty = 4 });
        _db.CuttingLayFabrics.AddRange(
            new CuttingLayFabric { CuttingLayId = k.Cl1.CuttingLayId, MaterialId = b2.Ma1.MaterialId, UsedM = 30.50m },
            new CuttingLayFabric { CuttingLayId = k.Cl9.CuttingLayId, MaterialId = b2.Ma1.MaterialId, UsedM = 3.00m });
        await _db.SaveChangesAsync(ct);

        // --- Phiếu bàn giao BTP ---
        k.Ch1 = B7Handover("HB-B7-01", k.Wo2.WorkOrderId, HandoverStatuses.Pending);
        k.Ch2 = B7Handover("HB-B7-02", k.Wo2.WorkOrderId, HandoverStatuses.Confirmed);
        k.Ch2.ConfirmedBy = s0.Wh.EmployeeId;
        k.Ch2.ConfirmedAt = _now.AddHours(-1);
        k.Ch9 = B7Handover("HB-B7-09", k.Wo3.WorkOrderId, HandoverStatuses.Pending);
        k.ChRej = B7Handover("HB-B7-03", k.Wo2.WorkOrderId, HandoverStatuses.Rejected);
        k.ChRej.RejectReason = "Sai size";
        k.ChCan = B7Handover("HB-B7-04", k.Wo2.WorkOrderId, HandoverStatuses.Cancelled);
        _db.CutHandovers.AddRange(k.Ch1, k.Ch2, k.Ch9, k.ChRej, k.ChCan);
        await _db.SaveChangesAsync(ct);

        k.Chl1 = new CutHandoverLine { CutHandoverId = k.Ch1.CutHandoverId, OrderLineId = k.Ol3.OrderLineId, HandedQty = 30 };
        k.Chl9 = new CutHandoverLine { CutHandoverId = k.Ch9.CutHandoverId, OrderLineId = k.Ol4.OrderLineId, ProductComponentMaterialId = k.Pcm2.ProductComponentMaterialId, HandedQty = 4 };
        _db.CutHandoverLines.AddRange(k.Chl1, k.Chl9,
            new CutHandoverLine { CutHandoverId = k.Ch2.CutHandoverId, OrderLineId = k.Ol3.OrderLineId, HandedQty = 10, ConfirmedQty = 10 });
        await _db.SaveChangesAsync(ct);
        return k;
    }

    private Request B7Request(string code, int orderId) => new()
    {
        RequestCode = code, RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved, OrderId = orderId,
        CreatedAt = _now.AddDays(-1), DecidedAt = _now.AddHours(-12),
    };

    private WorkOrder B7Wo(string code, int orderId, int productId, int teamId, string status, decimal tolerance) => new()
    {
        WorkOrderCode = code, OrderId = orderId, ProductId = productId, CuttingTeamId = teamId, DueDate = _t.AddDays(10), Status = status,
        FabricTolerancePercent = tolerance, CreatedAt = _now, CompletedAt = status == WorkOrderStatuses.Completed ? _now : null,
    };

    private CuttingLay B7Lay(int workOrderId, int teamId, string shift, string status) => new()
    {
        WorkOrderId = workOrderId, CuttingTeamId = teamId, WorkDate = _t, ShiftCode = shift, LaySequence = 1, Status = status, CreatedAt = _now,
    };

    private CutHandover B7Handover(string code, int workOrderId, string status) => new()
    {
        HandoverCode = code, WorkOrderId = workOrderId, HandedAt = _now, Status = status,
    };

    /// <summary>Phiếu cấp vải đã nhận (1000 m) của tổ cắt trên đơn: tổ đang giữ vải.</summary>
    public async Task AddHeldFabricAsync(int orderId, int teamId, int materialId, int issuedBy, int receivedBy, CancellationToken ct = default)
    {
        var issue = new MaterialIssue
        {
            IssueCode = "PC-B7-" + Guid.NewGuid().ToString("N")[..10], WarehouseType = WarehouseTypes.Material, OrderId = orderId, ReceivingTeamId = teamId,
            IssueDate = _t, IssuedBy = issuedBy, Status = IssueStatuses.Received, ReceivedBy = receivedBy, ReceivedAt = _now, CreatedAt = _now,
        };
        _db.MaterialIssues.Add(issue);
        await _db.SaveChangesAsync(ct);
        _db.MaterialIssueLines.Add(new MaterialIssueLine { MaterialIssueId = issue.MaterialIssueId, MaterialId = materialId, IssuedQty = 1000m, ConfirmedQty = 1000m });
        await _db.SaveChangesAsync(ct);
    }
}
