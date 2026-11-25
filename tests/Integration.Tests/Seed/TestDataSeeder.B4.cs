using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>S0 của IT_B4: chỉ bổ sung phần còn thiếu so với S0 Batch 2 (PV3, PV9, đơn DH-0001…DH-0005, phân tổ, bìa MK1, NVL đơn, phiếu xuất).</summary>
public sealed partial class TestDataSeeder
{
    public async Task<B4Context> SeedBatch4BaseAsync(B2Context b2, S0Context s0, CancellationToken ct = default)
    {
        var k = new B4Context();

        // Đơn có sẵn trong Test DB trùng mã DH-000x được đổi tạm (khôi phục ở chế độ đồng thời qua MovedKeys).
        var codes = new[] { "DH-0001", "DH-0002", "DH-0003", "DH-0004", "DH-0005" };
        foreach (var o in await _db.CustomerOrders.Where(x => codes.Contains(x.OrderCode)).ToListAsync(ct))
        {
            MovedKeys.Add(("CustomerOrder", "OrderId", o.OrderId, "OrderCode", o.OrderCode));
            o.OrderCode = $"MOVED-{o.OrderId}";
        }

        await _db.SaveChangesAsync(ct);

        // --- PR1 Trắng M (PV3); PR2 Xanh M (PV9) ---
        k.Pr1White = new ProductColor { ProductId = b2.Pr1.ProductId, Color = "Trắng", CreatedAt = _now };
        var pr2Blue = new ProductColor { ProductId = b2.Pr2.ProductId, Color = "Xanh", CreatedAt = _now };
        _db.ProductColors.AddRange(k.Pr1White, pr2Blue);
        await _db.SaveChangesAsync(ct);
        k.Pv3 = new ProductVariant { ProductId = b2.Pr1.ProductId, ProductColorId = k.Pr1White.ProductColorId, SizeCode = "M", Status = RecordStatus.Active };
        k.Pv9 = new ProductVariant { ProductId = b2.Pr2.ProductId, ProductColorId = pr2Blue.ProductColorId, SizeCode = "M", Status = RecordStatus.Active };
        _db.ProductVariants.AddRange(k.Pv3, k.Pv9);

        // Nhóm bìa 1 của PR1 (trigger kiểm tra bìa cần nhóm bìa đã khai).
        _db.ProductMarkerComponents.Add(new ProductMarkerComponent { ProductId = b2.Pr1.ProductId, MarkerNo = 1, ProductComponentId = b2.Pr1Front.ProductComponentId });
        _db.ProductMarkerFabrics.Add(new ProductMarkerFabric
        {
            ProductId = b2.Pr1.ProductId, MarkerNo = 1, ProductColorId = b2.Pr1Black.ProductColorId, MaterialId = b2.Ma1.MaterialId,
        });
        await _db.SaveChangesAsync(ct);

        // --- Đơn DH-0001…DH-0005 ---
        k.Od1 = Order("DH-0001", b2.Cu1, OrderStatuses.New, OrderMaterialStatuses.Awaiting, null);
        k.Od2 = Order("DH-0002", b2.Cu1, OrderStatuses.InProduction, OrderMaterialStatuses.Partial, null);
        k.Od3 = Order("DH-0003", b2.Cu2, OrderStatuses.Cancelled, OrderMaterialStatuses.Awaiting, "Khách hủy");
        k.Od4 = Order("DH-0004", b2.Cu1, OrderStatuses.New, OrderMaterialStatuses.Awaiting, null);
        k.Od5 = Order("DH-0005", b2.Cu1, OrderStatuses.Stopped, OrderMaterialStatuses.Partial, "Thiếu vải");
        _db.CustomerOrders.AddRange(k.Od1, k.Od2, k.Od3, k.Od4, k.Od5);
        await _db.SaveChangesAsync(ct);

        k.Ol1 = Line(k.Od1, b2.Pr1VarM, b2.Pr1.ProductId, 100);
        k.Ol2 = Line(k.Od1, b2.Pr1VarL, b2.Pr1.ProductId, 50);
        k.Ol3 = Line(k.Od2, k.Pv3, b2.Pr1.ProductId, 200);
        k.Ol9 = Line(k.Od4, k.Pv9, b2.Pr2.ProductId, 10);
        k.Ol3Cancelled = Line(k.Od3, b2.Pr1VarM, b2.Pr1.ProductId, 20);
        _db.OrderLines.AddRange(k.Ol1, k.Ol2, k.Ol3, k.Ol9, k.Ol3Cancelled);
        await _db.SaveChangesAsync(ct);

        // --- OD2: phân tổ, bìa, NVL, phiếu xuất ---
        k.Olt3 = new OrderLineTeam { OrderLineId = k.Ol3.OrderLineId, SewingTeamId = s0.TSew01.TeamId, AssignedAt = _now };
        _db.OrderLineTeams.Add(k.Olt3);

        k.Mk1 = new ProductMarker
        {
            OrderId = k.Od2.OrderId, ProductId = b2.Pr1.ProductId, MarkerNo = 1, MarkerCode = "S1", LengthM = 5.20m, WidthM = 1.50m, Status = RecordStatus.Active,
        };
        _db.ProductMarkers.Add(k.Mk1);
        await _db.SaveChangesAsync(ct);
        _db.ProductMarkerRatios.Add(new ProductMarkerRatio { ProductMarkerId = k.Mk1.ProductMarkerId, SizeCode = "M", PiecesPerPly = 2 });

        k.Om1 = new OrderMaterial { OrderId = k.Od2.OrderId, MaterialId = b2.Ma1.MaterialId, RequiredQty = 150m, CustomerQty = 0m };
        k.Om2 = new OrderMaterial { OrderId = k.Od2.OrderId, MaterialId = b2.Ma3.MaterialId, RequiredQty = 100m, CustomerQty = 100m };
        _db.OrderMaterials.AddRange(k.Om1, k.Om2);
        await _db.SaveChangesAsync(ct);

        var receipt = new MaterialReceipt
        {
            ReceiptCode = "GRN-B4-01", OrderId = k.Od2.OrderId, Source = MaterialSources.Supplier, ReceiptDate = _t.AddDays(-1),
            Status = ReceiptStatuses.Confirmed, SubmittedAt = _now.AddDays(-1), ConfirmedBy = s0.Wh.EmployeeId, ConfirmedAt = _now.AddDays(-1),
        };
        _db.MaterialReceipts.Add(receipt);
        await _db.SaveChangesAsync(ct);
        _db.MaterialReceiptLines.Add(new MaterialReceiptLine
        {
            MaterialReceiptId = receipt.MaterialReceiptId, OrderMaterialId = k.Om1.OrderMaterialId, AcceptedQty = 50m,
        });

        k.DnDelivered = Dispatch("DN-B4-01", k.Od2, DispatchStatuses.Delivered, s0);
        k.DnPending = Dispatch("DN-B4-02", k.Od2, DispatchStatuses.Pending, s0);
        _db.DispatchNotes.AddRange(k.DnDelivered, k.DnPending);
        await _db.SaveChangesAsync(ct);
        _db.DispatchNoteLines.AddRange(
            new DispatchNoteLine { DispatchNoteId = k.DnDelivered.DispatchNoteId, OrderLineId = k.Ol3.OrderLineId, IssueQty = 80 },
            new DispatchNoteLine { DispatchNoteId = k.DnPending.DispatchNoteId, OrderLineId = k.Ol3.OrderLineId, IssueQty = 30 });
        await _db.SaveChangesAsync(ct);
        return k;
    }

    private CustomerOrder Order(string code, Customer customer, string status, string materialStatus, string? stopReason) => new()
    {
        OrderCode = code, CustomerId = customer.CustomerId, ReceivedDate = _t.AddDays(-10), DueDate = _t.AddDays(30),
        Status = status, MaterialStatus = materialStatus, StopReason = status is OrderStatuses.Stopped ? stopReason : null,
        DeliveryAddress = "KCN Tân Tạo", CreatedAt = _now,
    };

    private static OrderLine Line(CustomerOrder order, ProductVariant variant, int productId, int qty) => new()
    {
        OrderId = order.OrderId, ProductVariantId = variant.ProductVariantId, ProductId = productId, OrderedQty = qty, CmtUnitPrice = 25_000m,
    };

    private DispatchNote Dispatch(string code, CustomerOrder order, string status, S0Context s0)
    {
        var delivered = status == DispatchStatuses.Delivered;
        return new DispatchNote
        {
            DispatchCode = code, OrderId = order.OrderId, DeliveryDate = _t, DeliveryAddress = order.DeliveryAddress, Status = status, CreatedAt = _now,
            DispatchedBy = delivered ? s0.Wh.EmployeeId : null, DispatchedAt = delivered ? _now : null,
            CustomerReceivedDate = delivered ? _t : null, DeliveryMethod = delivered ? DeliveryMethods.CustomerPickup : null,
        };
    }
}
