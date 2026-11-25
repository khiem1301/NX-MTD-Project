using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>S0 của IT_B6: đơn Od6/Od7 + NVL đơn, PO1/PO2/PO4, phiếu nhập Mr1–Mr9, yêu cầu thiếu giao Sd1–Sd9, Attachment At2/At3. Tồn kho dùng B2Context.Ma1Stock.</summary>
public sealed partial class TestDataSeeder
{
    public async Task<B6Context> SeedBatch6BaseAsync(B2Context b2, B4Context d, B5Context p, S0Context s0, CancellationToken ct = default)
    {
        var k = new B6Context();

        foreach (var o in await _db.PurchaseOrders.Where(x => x.PoCode.StartsWith("PO-B6-")).ToListAsync(ct))
        {
            MovedKeys.Add(("PurchaseOrder", "PurchaseOrderId", o.PurchaseOrderId, "PoCode", o.PoCode));
            o.PoCode = $"MOVED-{o.PurchaseOrderId}";
        }

        foreach (var o in await _db.CustomerOrders.Where(x => x.OrderCode.StartsWith("DH-B6-")).ToListAsync(ct))
        {
            MovedKeys.Add(("CustomerOrder", "OrderId", o.OrderId, "OrderCode", o.OrderCode));
            o.OrderCode = $"MOVED-{o.OrderId}";
        }

        foreach (var o in await _db.MaterialReceipts.Where(x => x.ReceiptCode.StartsWith("PN-B6-")).ToListAsync(ct))
        {
            MovedKeys.Add(("MaterialReceipt", "MaterialReceiptId", o.MaterialReceiptId, "ReceiptCode", o.ReceiptCode));
            o.ReceiptCode = $"MOVED-{o.MaterialReceiptId}";
        }

        foreach (var o in await _db.Requests.Where(x => x.RequestCode.StartsWith("YC-B6-")).ToListAsync(ct))
        {
            MovedKeys.Add(("Request", "RequestId", o.RequestId, "RequestCode", o.RequestCode));
            o.RequestCode = $"MOVED-{o.RequestId}";
        }

        await _db.SaveChangesAsync(ct);

        k.Wh2 = await S0PersonAsync("wh02", "S0-WH02", "Thủ Kho Hai", RoleCodes.WarehouseKeeper, PayTypes.Monthly, null, null, _hasher.Hash(S0Context.Password), ct);

        k.Od6 = B6Order("DH-B6-06", b2.Cu1, OrderStatuses.InProduction, OrderMaterialStatuses.Partial);
        k.Od7 = B6Order("DH-B6-07", b2.Cu1, OrderStatuses.New, OrderMaterialStatuses.Awaiting);
        _db.CustomerOrders.AddRange(k.Od6, k.Od7);
        await _db.SaveChangesAsync(ct);

        k.Om1 = new OrderMaterial { OrderId = k.Od6.OrderId, MaterialId = b2.Ma2.MaterialId, RequiredQty = 150m, CustomerQty = 150m };
        k.Om2 = new OrderMaterial { OrderId = k.Od6.OrderId, MaterialId = b2.Ma3.MaterialId, RequiredQty = 500m, CustomerQty = 200m };
        k.Om9 = new OrderMaterial { OrderId = k.Od7.OrderId, MaterialId = b2.Ma1.MaterialId, RequiredQty = 100m, CustomerQty = 0m };
        _db.OrderMaterials.AddRange(k.Om1, k.Om2, k.Om9);

        k.Po1 = B6Po("PO-B6-01", k.Od6.OrderId, b2.Su1);
        k.Po2 = B6Po("PO-B6-02", null, b2.Su2);
        k.Po3 = B6Po("PO-B6-03", null, b2.Su2);
        k.Po5 = B6Po("PO-B6-05", null, b2.Su2);
        k.Po5.GoodsStatus = PurchaseGoodsStatuses.ClosedShort;
        k.Po4 = B6Po("PO-B6-04", null, b2.Su1);
        k.Po4.GoodsStatus = PurchaseGoodsStatuses.Rejected;
        k.Po4.RejectReason = "NCC hết hàng";
        k.Po4.OrderDate = null;
        k.Po4.ExpectedDeliveryCount = null;
        _db.PurchaseOrders.AddRange(k.Po1, k.Po2, k.Po3, k.Po4, k.Po5);
        await _db.SaveChangesAsync(ct);

        k.Pol1 = new PurchaseOrderLine { PurchaseOrderId = k.Po1.PurchaseOrderId, OrderMaterialId = k.Om2.OrderMaterialId, OrderedQty = 300m, UnitPrice = 2_000m };
        k.Pol3 = new PurchaseOrderLine { PurchaseOrderId = k.Po2.PurchaseOrderId, MaterialId = b2.Ma1.MaterialId, OrderedQty = 200m, UnitPrice = 48_000m };
        k.Pol4 = new PurchaseOrderLine { PurchaseOrderId = k.Po4.PurchaseOrderId, MaterialId = b2.Ma1.MaterialId, OrderedQty = 100m, UnitPrice = 50_000m };
        k.Pol5 = new PurchaseOrderLine { PurchaseOrderId = k.Po3.PurchaseOrderId, MaterialId = b2.Ma1.MaterialId, OrderedQty = 500m, UnitPrice = 48_000m };
        k.Pol6 = new PurchaseOrderLine { PurchaseOrderId = k.Po5.PurchaseOrderId, MaterialId = b2.Ma1.MaterialId, OrderedQty = 50m, UnitPrice = 48_000m, NotReplenishedQty = 10m };
        _db.PurchaseOrderLines.AddRange(k.Pol1, k.Pol3, k.Pol4, k.Pol5, k.Pol6);
        _db.DeliveryScheduleDates.AddRange(
            new DeliveryScheduleDate { PurchaseOrderId = k.Po1.PurchaseOrderId, SeqNo = 1, DeliveryDate = _t.AddDays(3) },
            new DeliveryScheduleDate { PurchaseOrderId = k.Po2.PurchaseOrderId, SeqNo = 1, DeliveryDate = _t.AddDays(3) },
            new DeliveryScheduleDate { PurchaseOrderId = k.Po3.PurchaseOrderId, SeqNo = 1, DeliveryDate = _t.AddDays(3) });
        await _db.SaveChangesAsync(ct);

        k.Mr1 = B6Receipt("PN-B6-01", k.Od6.OrderId, null, ReceiptStatuses.Pending, s0);
        k.Mr2 = B6Receipt("PN-B6-02", null, k.Po2.PurchaseOrderId, ReceiptStatuses.Pending, s0);
        k.Mr3 = B6Receipt("PN-B6-03", k.Od6.OrderId, k.Po1.PurchaseOrderId, ReceiptStatuses.Confirmed, s0);
        k.Mr4 = B6Receipt("PN-B6-04", k.Od6.OrderId, null, ReceiptStatuses.Returned, s0);
        k.Mr4.ReturnReason = "Số lượng không khớp";
        k.Mr5 = B6Receipt("PN-B6-05", null, k.Po3.PurchaseOrderId, ReceiptStatuses.Confirmed, s0);
        k.Mr6 = B6Receipt("PN-B6-06", null, k.Po5.PurchaseOrderId, ReceiptStatuses.Confirmed, s0);
        k.Mr9 = B6Receipt("PN-B6-09", null, k.Po3.PurchaseOrderId, ReceiptStatuses.Confirmed, s0);
        _db.MaterialReceipts.AddRange(k.Mr1, k.Mr2, k.Mr3, k.Mr4, k.Mr5, k.Mr6, k.Mr9);
        await _db.SaveChangesAsync(ct);

        k.Mrl1 = new MaterialReceiptLine { MaterialReceiptId = k.Mr1.MaterialReceiptId, OrderMaterialId = k.Om1.OrderMaterialId, AcceptedQty = 100m };
        k.Mrl2 = new MaterialReceiptLine { MaterialReceiptId = k.Mr2.MaterialReceiptId, PurchaseOrderLineId = k.Pol3.PurchaseOrderLineId, MaterialId = b2.Ma1.MaterialId, AcceptedQty = 150m };
        k.Mrl3 = new MaterialReceiptLine { MaterialReceiptId = k.Mr3.MaterialReceiptId, OrderMaterialId = k.Om2.OrderMaterialId, PurchaseOrderLineId = k.Pol1.PurchaseOrderLineId, AcceptedQty = 200m };
        k.Mrl4 = new MaterialReceiptLine { MaterialReceiptId = k.Mr4.MaterialReceiptId, OrderMaterialId = k.Om1.OrderMaterialId, AcceptedQty = 10m };
        k.Mrl5 = new MaterialReceiptLine { MaterialReceiptId = k.Mr5.MaterialReceiptId, PurchaseOrderLineId = k.Pol5.PurchaseOrderLineId, MaterialId = b2.Ma1.MaterialId, AcceptedQty = 100m };
        k.Mrl6 = new MaterialReceiptLine { MaterialReceiptId = k.Mr6.MaterialReceiptId, PurchaseOrderLineId = k.Pol6.PurchaseOrderLineId, MaterialId = b2.Ma1.MaterialId, AcceptedQty = 40m };
        k.Mrl9 = new MaterialReceiptLine { MaterialReceiptId = k.Mr9.MaterialReceiptId, PurchaseOrderLineId = k.Pol5.PurchaseOrderLineId, MaterialId = b2.Ma1.MaterialId, AcceptedQty = 10m };
        _db.MaterialReceiptLines.AddRange(k.Mrl1, k.Mrl2, k.Mrl3, k.Mrl4, k.Mrl5, k.Mrl6, k.Mrl9);
        await _db.SaveChangesAsync(ct);

        k.Sd1 = B6Short("YC-B6-01", k.Mr3, RequestStatuses.PendingProcessing, null);
        k.Sd2 = B6Short("YC-B6-02", k.Mr5, RequestStatuses.PendingProcessing, ShortageResolutions.Replenish);
        k.Sd2.ExpectedDeliveryCount = 1;
        k.Sd2.TargetDate = _t.AddDays(5);
        k.Sd3 = B6Short("YC-B6-03", k.Mr6, RequestStatuses.Completed, ShortageResolutions.NoReplenish);
        k.Sd3.CompletedAt = _now.AddDays(-1);
        k.Sd9 = B6Short("YC-B6-09", k.Mr9, RequestStatuses.PendingProcessing, null);
        _db.Requests.AddRange(k.Sd1, k.Sd2, k.Sd3, k.Sd9);
        await _db.SaveChangesAsync(ct);

        k.Rl1 = B6ShortLine(k.Sd1, k.Mrl3, b2.Ma3.MaterialId, k.Pol1.PurchaseOrderLineId, 100m, null);
        k.Rl2 = B6ShortLine(k.Sd2, k.Mrl5, b2.Ma1.MaterialId, k.Pol5.PurchaseOrderLineId, 40m, 40m);
        k.Rl3 = B6ShortLine(k.Sd3, k.Mrl6, b2.Ma1.MaterialId, k.Pol6.PurchaseOrderLineId, 10m, null);
        k.Rl9 = B6ShortLine(k.Sd9, k.Mrl9, b2.Ma1.MaterialId, k.Pol5.PurchaseOrderLineId, 10m, null);
        _db.RequestLines.AddRange(k.Rl1, k.Rl2, k.Rl3, k.Rl9);

        foreach (var mr in new[] { k.Mr1, k.Mr2 })
            _db.Attachments.Add(new Attachment
            {
                OwnerType = AttachmentOwnerTypes.MaterialReceipt, OwnerId = mr.MaterialReceiptId, Purpose = AttachmentPurposes.DocumentPhoto,
                FileUrl = "it-b6/doc-" + mr.MaterialReceiptId + ".jpg", FileName = "doc.jpg", ContentType = "image/jpeg", IsCurrent = true, UploadedAt = _now.AddHours(-3),
            });

        k.At2 = new Attachment
        {
            OwnerType = AttachmentOwnerTypes.PurchaseOrder, OwnerId = p.Po3.PurchaseOrderId, Purpose = AttachmentPurposes.PaymentProof,
            FileUrl = "it-b6/at2.jpg", FileName = "at2.jpg", ContentType = "image/jpeg", IsCurrent = true, UploadedAt = _now.AddHours(-2),
        };
        k.At3 = new Attachment
        {
            OwnerType = AttachmentOwnerTypes.MaterialReceipt, OwnerId = k.Mr1.MaterialReceiptId, Purpose = AttachmentPurposes.Photo,
            FileUrl = "it-b6/at3.jpg", FileName = "at3.jpg", ContentType = "image/jpeg", IsCurrent = false, RemovedAt = _now.AddDays(-1), UploadedAt = _now.AddDays(-2),
        };
        _db.Attachments.AddRange(k.At2, k.At3);
        await _db.SaveChangesAsync(ct);
        return k;
    }

    private CustomerOrder B6Order(string code, Customer cu, string status, string materialStatus) => new()
    {
        OrderCode = code, CustomerId = cu.CustomerId, ReceivedDate = _t.AddDays(-5), DueDate = _t.AddDays(30), Status = status,
        MaterialStatus = materialStatus, DeliveryAddress = "KCN Tân Tạo", CreatedAt = _now,
    };

    private PurchaseOrder B6Po(string code, int? orderId, Supplier supplier) => new()
    {
        PoCode = code, OrderId = orderId, SupplierId = supplier.SupplierId, GoodsStatus = PurchaseGoodsStatuses.Ordered,
        PaymentStatus = PurchaseOrderStatuses.Unpaid, OrderDate = _t.AddDays(-5), ExpectedDeliveryCount = 1, CreatedAt = _now.AddDays(-5),
    };

    private MaterialReceipt B6Receipt(string code, int? orderId, int? poId, string status, S0Context s0)
    {
        var r = new MaterialReceipt
        {
            ReceiptCode = code, OrderId = orderId, PurchaseOrderId = poId, ReceiptDate = _t.AddDays(-1), Status = status,
            SubmittedAt = _now.AddDays(-1), Source = poId is null ? MaterialSources.Customer : MaterialSources.Supplier,
        };
        if (status == ReceiptStatuses.Confirmed) { r.ConfirmedBy = s0.Mgr.EmployeeId; r.ConfirmedAt = _now.AddDays(-1); }
        return r;
    }

    private Request B6Short(string code, MaterialReceipt receipt, string status, string? resolution) => new()
    {
        RequestCode = code, RequestTypeCode = RequestTypes.ShortDelivery, Status = status, MaterialReceiptId = receipt.MaterialReceiptId,
        ShortageResolution = resolution, CreatedAt = _now.AddDays(-1),
    };

    private static RequestLine B6ShortLine(Request req, MaterialReceiptLine line, int materialId, int polId, decimal shortfall, decimal? replenish) => new()
    {
        RequestId = req.RequestId, MaterialReceiptLineId = line.MaterialReceiptLineId, MaterialId = materialId, PurchaseOrderLineId = polId,
        Quantity = shortfall, ReplenishQty = replenish,
    };
}
