using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch2;
using Integration.Tests.Batch4;
using Integration.Tests.Batch5;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

public sealed record Snap6(Snap4 Base, string Detail);

/// <summary>Nền chung IT_B6: S0 Batch 1 + B2 + B4 + B5 + S0 Batch 6 (<see cref="B6Context"/>, alias R). Mỗi test chạy trong ambient transaction và Rollback.</summary>
public abstract class B6TestBase : B5TestBase
{
    protected B6Context R { get; private set; } = null!;
    protected static string MRM => M + "material-receipts";
    protected static string MRW => WK + "material-receipts";
    protected static string SDM => M + "short-deliveries";
    protected static string SDW => WK + "short-deliveries";
    protected static string INC => WK + "incoming-materials";
    protected static string ATT => C + "Attachments";

    protected B6TestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder)
    {
        await base.OnSeededAsync(seeder);
        R = await seeder.SeedBatch6BaseAsync(B, D, P, S0);
    }

    // ------------------------------------------------------------------ snapshot DB

    protected async Task<Snap6> Snap6()
    {
        var s = await Snap5();
        await using var db = NewDb();
        var mr = await db.MaterialReceipts.AsNoTracking().OrderBy(x => x.MaterialReceiptId).Select(x => new
        {
            x.MaterialReceiptId, x.ReceiptCode, x.ShortageRequestId, x.OrderId, x.Source, x.PurchaseOrderId, x.ReceiptDate, x.Status, x.ReturnReason, x.Note,
            x.SubmittedAt, x.ConfirmedBy, x.ConfirmedAt, x.RowVer,
        }).ToListAsync();
        var mrl = await db.MaterialReceiptLines.AsNoTracking().OrderBy(x => x.MaterialReceiptLineId).Select(x => new
            { x.MaterialReceiptLineId, x.MaterialReceiptId, x.OrderMaterialId, x.PurchaseOrderLineId, x.OriginalReceiptLineId, x.AcceptedQty, x.MaterialId }).ToListAsync();
        var sb = await db.StockBalances.AsNoTracking().OrderBy(x => x.StockBalanceId).Select(x => new
            { x.StockBalanceId, x.WarehouseType, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId, x.OnHandQty, x.UpdatedAt }).ToListAsync();
        var st = await db.StockTransactions.AsNoTracking().OrderBy(x => x.StockTransactionId).Select(x => new
            { x.StockTransactionId, x.StockBalanceId, x.SourceDocType, x.SourceDocId, x.SourceLineId, x.QtyChange, x.BalanceAfter }).ToListAsync();
        var rq = await db.Requests.AsNoTracking().OrderBy(x => x.RequestId).Select(x => new
        {
            x.RequestId, x.RequestCode, x.RequestTypeCode, x.Status, x.MaterialReceiptId, x.ShortageResolution, x.ExpectedDeliveryCount, x.TargetDate,
            x.TargetDateTo, x.ResultNote, x.DecidedAt, x.CompletedAt, x.UpdatedAt, x.RowVer,
        }).ToListAsync();
        var rl = await db.RequestLines.AsNoTracking().OrderBy(x => x.RequestLineId).Select(x => new
            { x.RequestLineId, x.RequestId, x.MaterialId, x.Quantity, x.ReplenishQty, x.MaterialReceiptLineId }).ToListAsync();
        var at = await db.Attachments.AsNoTracking().OrderBy(x => x.AttachmentId).Select(x => new
            { x.AttachmentId, x.OwnerType, x.OwnerId, x.Purpose, x.FileUrl, x.IsCurrent, x.RemovedAt }).ToListAsync();
        var dsd = await db.DeliveryScheduleDates.AsNoTracking().Where(x => x.RequestId != null).OrderBy(x => x.DeliveryScheduleDateId)
            .Select(x => new { x.DeliveryScheduleDateId, x.RequestId, x.SeqNo, x.DeliveryDate }).ToListAsync();
        return new Snap6(s, JsonSerializer.Serialize(new { mr, mrl, sb, st, rq, rl, at, dsd }));
    }

    protected async Task Unchanged6(Snap6 before, string because = "")
    {
        await Unchanged(before.Base, because);
        (await Snap6()).Detail.Should().Be(before.Detail, $"[DB] không được thay đổi. {because}");
    }

    /// <summary>Gửi lần lượt các request bằng <paramref name="who"/>: mỗi request trả một trong <paramref name="codes"/>, không 5xx, không lộ tên constraint, DB không đổi.</summary>
    protected async Task Deny6(string who, int[] codes, params (HttpMethod M, string Url, object? Body)[] reqs)
    {
        var before = await Snap6();
        foreach (var (m, url, body) in reqs)
        {
            var r = await CallAs(who, m, url, body);
            r.ShouldNotBeServerError();
            r.Code.Should().BeOneOf(codes, $"{who} {m} {url}: {r}");
            NoLeak(r);
        }

        await Unchanged6(before);
    }

    // ------------------------------------------------------------------ truy vấn DB

    protected async Task<MaterialReceipt?> MrAsync(int id)
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.MaterialReceiptId == id);
    }

    protected async Task<List<MaterialReceiptLine>> MrlAsync(int receiptId)
    {
        await using var db = NewDb();
        return await db.MaterialReceiptLines.AsNoTracking().Where(x => x.MaterialReceiptId == receiptId).OrderBy(x => x.MaterialReceiptLineId).ToListAsync();
    }

    protected async Task<List<MaterialReceipt>> AllMrAsync()
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().OrderBy(x => x.MaterialReceiptId).ToListAsync();
    }

    protected async Task<int> MrCountAsync()
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().CountAsync();
    }

    protected async Task<MaterialReceipt> NewestMrAsync(int excludeMax)
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().Where(x => x.MaterialReceiptId > excludeMax).OrderByDescending(x => x.MaterialReceiptId).FirstAsync();
    }

    protected async Task<int> MaxMrIdAsync()
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().MaxAsync(x => x.MaterialReceiptId);
    }

    protected async Task<StockBalance?> SbAsync(int materialId)
    {
        await using var db = NewDb();
        return await db.StockBalances.AsNoTracking().SingleOrDefaultAsync(x => x.WarehouseType == WarehouseTypes.Material && x.MaterialId == materialId && x.OrderLineId == null && x.ProductComponentMaterialId == null);
    }

    protected async Task<decimal> OnHandAsync(int materialId) => (await SbAsync(materialId))?.OnHandQty ?? 0m;

    protected async Task<List<StockTransaction>> StAsync(int receiptId)
    {
        await using var db = NewDb();
        return await db.StockTransactions.AsNoTracking().Where(x => x.SourceDocId == receiptId && x.SourceDocType == StockDocTypes.MaterialReceipt).OrderBy(x => x.StockTransactionId).ToListAsync();
    }

    protected async Task<long> MaxStAsync()
    {
        await using var db = NewDb();
        return await db.StockTransactions.AsNoTracking().Select(x => (long?)x.StockTransactionId).MaxAsync() ?? 0;
    }

    protected async Task<Request?> ReqAsync(int id)
    {
        await using var db = NewDb();
        return await db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == id);
    }

    protected async Task<List<Request>> ShortOfReceiptAsync(int receiptId)
    {
        await using var db = NewDb();
        return await db.Requests.AsNoTracking().Where(x => x.RequestTypeCode == RequestTypes.ShortDelivery && x.MaterialReceiptId == receiptId).ToListAsync();
    }

    protected async Task<List<RequestLine>> RlAsync(int requestId)
    {
        await using var db = NewDb();
        return await db.RequestLines.AsNoTracking().Where(x => x.RequestId == requestId).OrderBy(x => x.RequestLineId).ToListAsync();
    }

    protected async Task<List<DeliveryScheduleDate>> ReqDsdAsync(int requestId)
    {
        await using var db = NewDb();
        return await db.DeliveryScheduleDates.AsNoTracking().Where(x => x.RequestId == requestId).OrderBy(x => x.SeqNo).ToListAsync();
    }

    protected async Task<List<Attachment>> AttAsync(string ownerType, int ownerId)
    {
        await using var db = NewDb();
        return await db.Attachments.AsNoTracking().Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId).OrderBy(x => x.AttachmentId).ToListAsync();
    }

    protected async Task<Attachment> AttByIdAsync(long id)
    {
        await using var db = NewDb();
        return await db.Attachments.AsNoTracking().SingleAsync(x => x.AttachmentId == id);
    }

    protected async Task<int> AttCountAsync()
    {
        await using var db = NewDb();
        return await db.Attachments.AsNoTracking().CountAsync();
    }

    protected async Task<int> MissingMrIdAsync() => (await MaxMrIdAsync()) + 1_000_000;

    protected async Task<int> MissingRequestIdAsync()
    {
        await using var db = NewDb();
        return (await db.Requests.AsNoTracking().MaxAsync(x => x.RequestId)) + 1_000_000;
    }

    protected async Task<int> MissingOmIdAsync() => await MissingOrderMaterialIdAsync();

    protected async Task<int> MissingPolIdAsync()
    {
        await using var db = NewDb();
        return (await db.PurchaseOrderLines.AsNoTracking().MaxAsync(x => x.PurchaseOrderLineId)) + 1_000_000;
    }

    // ------------------------------------------------------------------ body mẫu

    protected static object CL(int omId, decimal qty) => new { OrderMaterialId = (int?)omId, PurchaseOrderLineId = (int?)null, AcceptedQty = qty };
    protected static object SL(int polId, decimal qty) => new { OrderMaterialId = (int?)null, PurchaseOrderLineId = (int?)polId, AcceptedQty = qty };
    protected static object RawLine(int? omId, int? polId, decimal qty) => new { OrderMaterialId = omId, PurchaseOrderLineId = polId, AcceptedQty = qty };

    protected Dictionary<string, object?> CustBody(object[]? lines = null, string? note = null, DateOnly? date = null, int? orderId = null) => new()
    {
        ["OrderId"] = orderId ?? R.Od6.OrderId, ["Source"] = "CUSTOMER", ["PurchaseOrderId"] = null, ["ReceiptDate"] = date ?? T, ["Note"] = note,
        ["Lines"] = lines ?? [CL(R.Om1.OrderMaterialId, 50m), CL(R.Om2.OrderMaterialId, 200m)],
    };

    protected Dictionary<string, object?> SupBody(object[]? lines = null, string? note = null, DateOnly? date = null, int? poId = null) => new()
    {
        ["OrderId"] = null, ["Source"] = "SUPPLIER", ["PurchaseOrderId"] = poId ?? R.Po2.PurchaseOrderId, ["ReceiptDate"] = date ?? T, ["Note"] = note,
        ["Lines"] = lines ?? [SL(R.Pol3.PurchaseOrderLineId, 200m)],
    };

    protected Dictionary<string, object?> SuppBody(int? requestId = null, object[]? lines = null, string? note = null, DateOnly? date = null) => new()
    {
        ["ShortageRequestId"] = requestId ?? R.Sd2.RequestId, ["ReceiptDate"] = date ?? T, ["Note"] = note,
        ["Lines"] = lines ?? [new { RequestLineId = R.Rl2.RequestLineId, AcceptedQty = 40m }],
    };

    protected static object SuppLine(int requestLineId, decimal qty) => new { RequestLineId = requestLineId, AcceptedQty = qty };

    protected Dictionary<string, object?> ResolveBody(string resolution = "REPLENISH", object[]? lines = null, string? note = "NCC giao bù", short? count = 2,
        DateOnly[]? dates = null) => new()
    {
        ["Resolution"] = resolution, ["Lines"] = lines, ["Note"] = note, ["ExpectedDeliveryCount"] = count,
        ["ExpectedDeliveryDates"] = dates ?? (count is null ? null : Enumerable.Range(1, Math.Max(0, (int)count)).Select(i => T.AddDays(5 * i)).ToArray()),
    };

    protected static object Rep(int requestLineId, decimal qty) => new { RequestLineId = requestLineId, ReplenishQty = qty };

    protected Task<Api> Resolve(int requestId, object body, string who = "manager") => Call(who, Post, $"{SDM}/{requestId}/resolve", body);
    protected Task<Api> Confirm(int receiptId, string who = "manager") => Call(who, Post, $"{MRM}/{receiptId}/confirm");
    protected Task<Api> ReturnMr(int receiptId, object body, string who = "manager") => Call(who, Post, $"{MRM}/{receiptId}/return", body);

    protected static string Vn(int length) => string.Concat(Enumerable.Repeat("Ghi chú nhập kho ", length / 17 + 1))[..length];

    protected static int ReceiptIdOf(Api r) => B1Json.Int(r.Data, "materialReceiptId") ?? -1;

    protected static void NoLeak(Api r) => r.Body.Should().NotContainAny("trg_", "UQ_", "UX_", "FK_", "usp_Stock_Post", "SqlException", "StackTrace", "   at ");

    protected static void Graceful(Api r, params int[] codes)
    {
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(codes, r.ToString());
        NoLeak(r);
    }

    protected static void NearVnNow(DateTime? value, string what)
    {
        value.Should().NotBeNull(what);
        var v = value!.Value;
        var near = Math.Abs((v - DateTime.UtcNow.AddHours(7)).TotalHours) <= 2 || Math.Abs((v - FixedClock.Default.Now).TotalHours) <= 2;
        near.Should().BeTrue($"{what}: {v:O} phải ≈ giờ VN hiện tại hoặc FixedClock");
    }

    // ------------------------------------------------------------------ multipart upload

    protected async Task<Api> Upload(string? who, string ownerType, object ownerId, string? purpose, byte[]? content, string fileName = "img_ok.jpg",
        string contentType = "image/jpeg")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(ownerType), "ownerType");
        form.Add(new StringContent(ownerId.ToString()!), "ownerId");
        if (purpose is not null) form.Add(new StringContent(purpose), "purpose");
        if (content is not null)
        {
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "file", fileName);
        }

        using var req = new HttpRequestMessage(Post, ATT) { Content = form };
        if (who is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", who == "wh02" ? await TokenAsync(R.Wh2) : await TokenFor(who));
        using var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonElement root = default;
        var isJson = false;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { root = JsonDocument.Parse(text).RootElement.Clone(); isJson = true; }
            catch (JsonException) { }
        }

        return new Api { Status = res.StatusCode, Body = text, Root = root, IsJson = isJson };
    }

    private async Task<string> TokenFor(string who) => who switch
    {
        "admin" => await TkAdm(), "manager" => await TkMgr(), "accountant" => await TkAcc(), "warehouse" => await TkWk(),
        "teamleader" => await TkTl(), "worker" => await TkWrk(), _ => throw new ArgumentException(who),
    };

    protected static byte[] Bytes(int size) => Jpeg(size);

    protected async Task<long> SeedUploadAsync(string ownerType, int ownerId, string who = "warehouse", string purpose = "PHOTO")
    {
        var r = await Upload(who, ownerType, ownerId, purpose, Jpeg(2048));
        r.Code.Should().Be(200, $"seed: upload ảnh mẫu phải thành công. {r}");
        return B1Json.Prop(r.Data, "attachmentId")!.Value.GetInt64();
    }

    protected async Task<RawResponse> RawAsync(string url, string who)
    {
        var token = who == "wh02" ? await TokenAsync(R.Wh2) : await TokenFor(who);
        return await RawGetAsync(url, token);
    }
}
