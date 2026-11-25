using System.Net.Http.Headers;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch5;
using Integration.Tests.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>Nền cho ca đồng thời / fault-injection IT_B6: S0 (Batch 1 + B2 + B4 + B5 + B6) được COMMIT, request song song dùng connection độc lập, tự dọn sau test (kể cả thẻ kho append-only).</summary>
public abstract class B6ConcurrentTestBase : B5ConcurrentTestBase
{
    private long _reqMark, _attMark;

    protected B6Context R { get; private set; } = null!;

    protected B6ConcurrentTestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder, NxMtdDbContext seedDb)
    {
        _reqMark = await seedDb.Requests.AsNoTracking().MaxAsync(x => (int?)x.RequestId) ?? 0;
        _attMark = await seedDb.Attachments.AsNoTracking().MaxAsync(x => (long?)x.AttachmentId) ?? 0;
        await base.OnSeededAsync(seeder, seedDb);
        R = await seeder.SeedBatch6BaseAsync(B, D, P, S0);
    }

    protected static string MRM => M + "material-receipts";
    protected static string MRW => WK + "material-receipts";
    protected static string SDM => M + "short-deliveries";
    protected static string ATT => C + "Attachments";

    /// <summary>Thẻ kho là sổ chỉ-thêm: tắt trigger chặn DELETE và gỡ vòng FK Request ↔ MaterialReceipt trước khi dọn.</summary>
    protected override async Task BeforeCleanupAsync(SqlConnection conn)
    {
        async Task Exec(string sql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@r", _reqMark);
            cmd.Parameters.AddWithValue("@a", _attMark);
            await cmd.ExecuteNonQueryAsync();
        }

        await Exec("DISABLE TRIGGER dbo.trg_StockTransaction_AppendOnly ON dbo.StockTransaction");
        await Exec("UPDATE dbo.MaterialReceipt SET ShortageRequestId = NULL WHERE ShortageRequestId > @r");
        await Exec("DELETE FROM dbo.DeliveryScheduleDate WHERE RequestId > @r");
        await Exec("DELETE FROM dbo.RequestLine WHERE RequestId > @r");
        await Exec("DELETE FROM dbo.Request WHERE RequestId > @r");
        await Exec("DELETE FROM dbo.Attachment WHERE AttachmentId > @a");
    }

    protected override async Task AfterCleanupAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "ENABLE TRIGGER dbo.trg_StockTransaction_AppendOnly ON dbo.StockTransaction";
        await cmd.ExecuteNonQueryAsync();
    }

    protected Task<string> TkWk2() => TokenAsync(R.Wh2);

    protected Task<Api> ConfirmAsync(int id, string token) => PostAsync($"{MRM}/{id}/confirm", token);

    // ------------------------------------------------------------------ dữ liệu riêng từng vòng

    /// <summary>Phiếu khách cấp PENDING (Om1 / Ma2) hoặc phiếu NCC PENDING (Pol3 / Ma1, PO2) với <paramref name="qty"/>; mỗi vòng một phiếu mới.</summary>
    /// <summary>NVL (và NVL thứ hai) của phiếu vừa tạo bởi <see cref="NewPendingAsync"/>: mỗi phiếu một NVL riêng nên không vướng luật "đã có phiếu chờ xác nhận".</summary>
    protected int Mat { get; private set; }

    protected int Mat2 { get; private set; }

    protected int LastOrderId { get; private set; }

    private async Task<int> NewMaterialAsync(NxMtdDbContext db)
    {
        var m = new Material
        {
            MaterialCode = "C6-" + Guid.NewGuid().ToString("N")[..12], MaterialName = "NVL C6 " + Guid.NewGuid().ToString("N")[..8], MaterialType = MaterialTypes.Trim,
            Color = "", Unit = "cái", Status = MaterialStatuses.Active, CreatedAt = Clock.Now,
        };
        db.Materials.Add(m);
        await db.SaveChangesAsync();
        return m.MaterialId;
    }

    protected async Task<(MaterialReceipt Receipt, List<MaterialReceiptLine> Lines)> NewPendingAsync(decimal qty = 40m, bool supplier = false, int lines = 1, bool photo = true)
    {
        await using var db = NewDb();
        if (!supplier)
        {
            var ord = new CustomerOrder
            {
                OrderCode = "DH-C6-" + Guid.NewGuid().ToString("N")[..10], CustomerId = B.Cu1.CustomerId, ReceivedDate = T.AddDays(-5), DueDate = T.AddDays(30),
                Status = OrderStatuses.InProduction, MaterialStatus = OrderMaterialStatuses.Partial, DeliveryAddress = "KCN Tân Tạo", CreatedAt = Clock.Now,
            };
            db.CustomerOrders.Add(ord);
            await db.SaveChangesAsync();
            LastOrderId = ord.OrderId;
        }

        var mr = new MaterialReceipt
        {
            ReceiptCode = "PN-C6-" + Guid.NewGuid().ToString("N")[..12], OrderId = supplier ? null : LastOrderId,
            PurchaseOrderId = supplier ? R.Po2.PurchaseOrderId : null, ReceiptDate = T, Status = ReceiptStatuses.Pending, SubmittedAt = Clock.Now,
        };
        db.MaterialReceipts.Add(mr);
        await db.SaveChangesAsync();
        var list = new List<MaterialReceiptLine>();
        Mat = await NewMaterialAsync(db);
        if (supplier)
        {
            var pol = new PurchaseOrderLine { PurchaseOrderId = R.Po2.PurchaseOrderId, MaterialId = Mat, OrderedQty = 200m, UnitPrice = 1_000m };
            db.PurchaseOrderLines.Add(pol);
            await db.SaveChangesAsync();
            list.Add(new MaterialReceiptLine { MaterialReceiptId = mr.MaterialReceiptId, PurchaseOrderLineId = pol.PurchaseOrderLineId, MaterialId = Mat, AcceptedQty = qty });
        }
        else
        {
            var om = new OrderMaterial { OrderId = LastOrderId, MaterialId = Mat, RequiredQty = 1_000m, CustomerQty = 1_000m };
            db.OrderMaterials.Add(om);
            await db.SaveChangesAsync();
            list.Add(new MaterialReceiptLine { MaterialReceiptId = mr.MaterialReceiptId, OrderMaterialId = om.OrderMaterialId, AcceptedQty = qty });
            if (lines > 1)
            {
                Mat2 = await NewMaterialAsync(db);
                var om2 = new OrderMaterial { OrderId = LastOrderId, MaterialId = Mat2, RequiredQty = 1_000m, CustomerQty = 1_000m };
                db.OrderMaterials.Add(om2);
                await db.SaveChangesAsync();
                list.Add(new MaterialReceiptLine { MaterialReceiptId = mr.MaterialReceiptId, OrderMaterialId = om2.OrderMaterialId, AcceptedQty = qty });
            }
        }

        db.MaterialReceiptLines.AddRange(list);
        if (photo)
            db.Attachments.Add(new Attachment
            {
                OwnerType = AttachmentOwnerTypes.MaterialReceipt, OwnerId = mr.MaterialReceiptId, Purpose = AttachmentPurposes.DocumentPhoto,
                FileUrl = "it-b6/c6-" + mr.MaterialReceiptId + ".jpg", FileName = "doc.jpg", ContentType = "image/jpeg", IsCurrent = true, UploadedAt = Clock.Now,
            });
        await db.SaveChangesAsync();
        return (mr, list);
    }

    protected async Task<int> NewOmAsync()
    {
        await using var db = NewDb();
        var om = new OrderMaterial { OrderId = R.Od6.OrderId, MaterialId = await NewMaterialAsync(db), RequiredQty = 1_000m, CustomerQty = 1_000m };
        db.OrderMaterials.Add(om);
        await db.SaveChangesAsync();
        return om.OrderMaterialId;
    }

    /// <summary>Body PUT/POST khách cấp cho đúng NVL đã seed trong <see cref="NewPendingAsync"/> (dòng đầu).</summary>
    protected Dictionary<string, object?> BodyFor(MaterialReceiptLine line, decimal qty) => new()
    {
        ["OrderId"] = LastOrderId, ["Source"] = "CUSTOMER", ["PurchaseOrderId"] = null, ["ReceiptDate"] = T, ["Note"] = null,
        ["Lines"] = new[] { CL(line.OrderMaterialId!.Value, qty) },
    };

    protected async Task<MaterialReceipt> MrAsync(int id)
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().SingleAsync(x => x.MaterialReceiptId == id);
    }

    protected async Task<List<MaterialReceiptLine>> MrlAsync(int id)
    {
        await using var db = NewDb();
        return await db.MaterialReceiptLines.AsNoTracking().Where(x => x.MaterialReceiptId == id).OrderBy(x => x.MaterialReceiptLineId).ToListAsync();
    }

    protected async Task<decimal> OnHandAsync(int materialId)
    {
        await using var db = NewDb();
        return await db.StockBalances.AsNoTracking()
            .Where(x => x.WarehouseType == WarehouseTypes.Material && x.MaterialId == materialId && x.OrderLineId == null && x.ProductComponentMaterialId == null)
            .Select(x => (decimal?)x.OnHandQty).SingleOrDefaultAsync() ?? 0m;
    }

    protected async Task<List<StockTransaction>> StAsync(int receiptId)
    {
        await using var db = NewDb();
        return await db.StockTransactions.AsNoTracking().Where(x => x.SourceDocId == receiptId && x.SourceDocType == StockDocTypes.MaterialReceipt).ToListAsync();
    }

    protected async Task<List<Request>> ShortOfAsync(int receiptId)
    {
        await using var db = NewDb();
        return await db.Requests.AsNoTracking().Where(x => x.RequestTypeCode == RequestTypes.ShortDelivery && x.MaterialReceiptId == receiptId).ToListAsync();
    }

    protected async Task<Request> ReqAsync(int id)
    {
        await using var db = NewDb();
        return await db.Requests.AsNoTracking().SingleAsync(x => x.RequestId == id);
    }

    /// <summary>Yêu cầu thiếu giao mới chưa xử lý (gắn phiếu gốc xác nhận mới) với một dòng thiếu <paramref name="shortfall"/>.</summary>
    protected async Task<(Request Req, RequestLine Line)> NewShortAsync(decimal shortfall = 100m, string? resolution = null)
    {
        await using var db = NewDb();
        var mr = new MaterialReceipt
        {
            ReceiptCode = "PN-C6-" + Guid.NewGuid().ToString("N")[..12], PurchaseOrderId = R.Po3.PurchaseOrderId, ReceiptDate = T.AddDays(-1),
            Status = ReceiptStatuses.Confirmed, SubmittedAt = Clock.Now.AddDays(-1), ConfirmedBy = S0.Mgr.EmployeeId, ConfirmedAt = Clock.Now.AddDays(-1),
        };
        db.MaterialReceipts.Add(mr);
        await db.SaveChangesAsync();
        var line = new MaterialReceiptLine { MaterialReceiptId = mr.MaterialReceiptId, PurchaseOrderLineId = R.Pol5.PurchaseOrderLineId, MaterialId = B.Ma1.MaterialId, AcceptedQty = 10m };
        db.MaterialReceiptLines.Add(line);
        await db.SaveChangesAsync();
        var req = new Request
        {
            RequestCode = "YC-C6-" + Guid.NewGuid().ToString("N")[..12], RequestTypeCode = RequestTypes.ShortDelivery, Status = RequestStatuses.PendingProcessing,
            MaterialReceiptId = mr.MaterialReceiptId, ShortageResolution = resolution, CreatedAt = Clock.Now.AddDays(-1),
        };
        db.Requests.Add(req);
        await db.SaveChangesAsync();
        var rl = new RequestLine
        {
            RequestId = req.RequestId, MaterialReceiptLineId = line.MaterialReceiptLineId, MaterialId = B.Ma1.MaterialId,
            PurchaseOrderLineId = R.Pol5.PurchaseOrderLineId, Quantity = shortfall, ReplenishQty = resolution is null ? null : shortfall,
        };
        db.RequestLines.Add(rl);
        await db.SaveChangesAsync();
        return (req, rl);
    }

    protected static object CL(int omId, decimal qty) => new { OrderMaterialId = (int?)omId, PurchaseOrderLineId = (int?)null, AcceptedQty = qty };
    protected static object SL(int polId, decimal qty) => new { OrderMaterialId = (int?)null, PurchaseOrderLineId = (int?)polId, AcceptedQty = qty };

    protected Dictionary<string, object?> CustBody(string? note = null, decimal q1 = 10m) => new()
    {
        ["OrderId"] = R.Od6.OrderId, ["Source"] = "CUSTOMER", ["PurchaseOrderId"] = null, ["ReceiptDate"] = T, ["Note"] = note,
        ["Lines"] = new[] { CL(R.Om1.OrderMaterialId, q1) },
    };

    protected async Task<Api> UploadFor(string token, int receiptId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("MATERIAL_RECEIPT"), "ownerType");
        form.Add(new StringContent(receiptId.ToString()), "ownerId");
        form.Add(new StringContent("PHOTO"), "purpose");
        var file = new ByteArrayContent(Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA="));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "img_ok.jpg");
        using var req = new HttpRequestMessage(HttpMethod.Post, ATT) { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        System.Text.Json.JsonElement root = default;
        var isJson = false;
        try { root = System.Text.Json.JsonDocument.Parse(text).RootElement.Clone(); isJson = true; } catch (System.Text.Json.JsonException) { }
        return new Api { Status = res.StatusCode, Body = text, Root = root, IsJson = isJson };
    }

    protected async Task<List<Attachment>> CurrentAttAsync(int receiptId)
    {
        await using var db = NewDb();
        return await db.Attachments.AsNoTracking().Where(x => x.OwnerType == AttachmentOwnerTypes.MaterialReceipt && x.OwnerId == receiptId).ToListAsync();
    }

    protected async Task<byte[]> RowVerOfMrAsync(int id) => (await MrAsync(id)).RowVer;
}
