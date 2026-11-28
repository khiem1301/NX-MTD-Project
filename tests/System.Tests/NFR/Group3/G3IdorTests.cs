using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace System.Tests.NFR.Group3;

/// <summary>G3-03, G3-04, G3-05: IDOR. Mỗi ca dùng ID có thật của người / tổ khác, đã xác nhận chủ sở hữu đọc được.</summary>
[Collection("NFR_G3")]
public sealed class G3IdorTests : G3Base
{
    private static string Snap(JsonNode n) => n.ToJsonString();

    [Fact]
    public async Task NFR_G3_011_OwnerReadsOwnRequestAndPayslip()
    {
        var p = new Probe("NFR_G3_011");
        var title = NewDataName("own");
        var id = await CreateOwnRequest(Acc.WorkerA, title);
        var req = await Get(Acc.WorkerA, $"common/requests/{id}");
        p.Status(req, "A đọc đơn của mình", 200);
        p.Check(req.Text.Contains(title), "Đơn trả về đúng đơn của A", req);

        var list = Items(await Seed(Acc.WorkerA, "common/my-payslips"));
        var month = list[0]!["periodMonth"]!.GetValue<string>();
        var slip = await Get(Acc.WorkerA, $"common/my-payslips/{month}");
        p.Status(slip, "A đọc phiếu lương của mình", 200);
        var net = list[0]!["netPay"]!.GetValue<decimal>();
        p.Check(slip.Data?["netPay"]?.GetValue<decimal>() == net, "Phiếu lương đúng thực lĩnh của A", slip);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_012_CannotTouchOthersRequests()
    {
        var p = new Probe("NFR_G3_012");
        var bId = await CreateOwnRequest(Acc.WorkerB, NewDataName("reqB"));
        var before = await Seed(Acc.WorkerB, $"common/requests/{bId}");
        var rowVer = before["rowVer"]?.GetValue<string>();
        int[] blocked = { 403, 404 };

        p.Status(await Get(Acc.WorkerA, $"common/requests/{bId}"), "A GET đơn B", blocked);
        p.Status(await Put(Acc.WorkerA, $"common/requests/{bId}", new { title = NewDataName("hack"), content = "x", rowVer }), "A PUT đơn B", blocked);
        p.Status(await Post(Acc.WorkerA, $"common/requests/{bId}/cancel", new { reason = "NFR idor", rowVer }), "A cancel đơn B", blocked);
        foreach (var (action, body) in new (string, object)[]
                 {
                     ("approve", new { note = "x", rowVer }), ("reject", new { reason = "x", rowVer }),
                     ("complete", new { resultNote = "x", rowVer }), ("not-complete", new { reason = "x", rowVer }),
                 })
            p.Status(await Post(Acc.WorkerA, $"common/request-inbox/{bId}/{action}", body), $"A inbox {action} đơn B", blocked);

        var own = Items(await Seed(Acc.WorkerA, "common/requests?PageSize=100")).Select(i => i!["requestId"]!.GetValue<int>()).ToHashSet();
        for (var id = Math.Max(1, bId - 50); id <= bId + 50; id++)
            if (!own.Contains(id))
                p.Status(await Get(Acc.WorkerA, $"common/requests/{id}"), $"A GET đơn lân cận #{id}", blocked);

        var after = await Seed(Acc.WorkerB, $"common/requests/{bId}");
        p.Check(Snap(before["status"]!) == Snap(after["status"]!), "Trạng thái đơn B không đổi");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_013_CannotMarkOthersNotificationRead()
    {
        var p = new Probe("NFR_G3_013");
        async Task<JsonNode?> Unread() => Items(await Seed(Acc.WorkerB, "common/notifications?UnreadOnly=true&PageSize=100")).FirstOrDefault();
        var target = await Unread() ?? throw new InvalidOperationException("THIẾU SEED: B không có thông báo chưa đọc.");
        var id = target["notificationId"]!.GetValue<long>();
        p.Status(await Post(Acc.WorkerA, $"common/notifications/{id}/read"), "A đánh dấu đọc thông báo B", 403, 404);
        var still = Items(await Seed(Acc.WorkerB, "common/notifications?UnreadOnly=true&PageSize=100")).Any(n => n!["notificationId"]!.GetValue<long>() == id);
        p.Check(still, "Thông báo của B vẫn chưa đọc");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_014_MyApis_IgnoreInjectedIdentity()
    {
        var p = new Probe("NFR_G3_014");
        var month = Items(await Seed(Acc.WorkerA, "common/my-payslips"))[0]!["periodMonth"]!.GetValue<string>();
        var day = (await Seed(Acc.WorkerA, "common/my-estimated-pay"))["days"]!.AsArray()[0]!["workDate"]!.GetValue<string>();
        var range = $"FromDate={DateTime.Today.AddDays(-14):yyyy-MM-dd}&ToDate={DateTime.Today:yyyy-MM-dd}";
        var paths = new[]
        {
            $"common/my-payslips/{month}", "common/my-payslips", "common/my-estimated-pay", $"common/my-estimated-pay/days/{day}",
            $"common/my-attendance/timesheets?{range}", $"common/my-attendance/timesheets/summary?{range}",
            "common/my-defects", "common/my-meals/today",
        };
        var inject = $"userId={Acc.WorkerBId}&employeeId={Acc.WorkerBId}&workerId={Acc.WorkerBId}&TeamId=4";
        foreach (var path in paths)
        {
            var own = await Get(Acc.WorkerA, path);
            if (!own.Success) throw new InvalidOperationException($"THIẾU SEED: A GET {path} → {own.Status}");
            var hacked = await Get(Acc.WorkerA, path + (path.Contains('?') ? "&" : "?") + inject);
            p.Check(hacked.Status == 400 || (hacked.Status == 200 && hacked.Text == own.Text), $"{path} + id của B phải trả dữ liệu A hoặc 400", hacked);
        }

        // Chèn định danh của B vào body: suất ăn của B không được đổi
        var bBefore = await Seed(Acc.WorkerB, "common/my-meals/today");
        var aMeal = await Seed(Acc.WorkerA, "common/my-meals/today");
        var willEat = aMeal["willEat"]?.GetValue<bool>() ?? true;
        var patch = await Patch(Acc.WorkerA, "common/my-meals/today",
            new { mealDate = aMeal["mealDate"]?.GetValue<string>(), willEat = !willEat, employeeId = Acc.WorkerBId, workerId = Acc.WorkerBId, userId = Acc.WorkerBId });
        var bAfter = await Seed(Acc.WorkerB, "common/my-meals/today");
        p.Check(Snap(bBefore) == Snap(bAfter), "PATCH có employeeId của B không đổi suất ăn của B", patch);
        p.Done();
    }

    private static async Task<List<JsonNode>> Pending(string worker) =>
        Items(await Seed(worker, "worker/work-allocations?Status=PENDING&PageSize=100")).Select(n => n!).ToList();

    /// <summary>Bơm phiếu giao việc PENDING bằng API của Tổ trưởng (đơn 3 còn công đoạn chưa giao hết).</summary>
    private static async Task EnsurePending(string leader, string worker, int workerId, int orderLineId, int need)
    {
        var log = new List<string>();
        foreach (var operationId in new[] { 25, 26 })
        {
            if ((await Pending(worker)).Count >= need) return;
            var a = await Post(leader, "team-leader/operation-assignments", new { orderId = 3, operationId, workerId });
            var s = await Post(leader, "team-leader/work-allocations", new { movementType = "HANDOVER", workerId, orderLineId, operationId, quantity = 1 });
            log.Add($"op{operationId}: phân công {a.Status}, giao {s.Status} {s.Text}");
        }
        if ((await Pending(worker)).Count < need)
            throw new InvalidOperationException($"THIẾU SEED: không tạo đủ {need} phiếu PENDING cho {worker}. {string.Join(" | ", log)}");
    }

    [Fact]
    public async Task NFR_G3_015_WorkerCannotConfirmOthersAllocations()
    {
        var p = new Probe("NFR_G3_015");
        await EnsurePending(Acc.Leader, Acc.WorkerA, Acc.WorkerAId, 20, 2);
        await EnsurePending(Acc.Leader2, Acc.WorkerB, Acc.WorkerBId, 23, 1);
        var mine = (await Pending(Acc.WorkerA)).Take(2).ToList();
        var other = (await Pending(Acc.WorkerB))[0];
        int Id(JsonNode n) => n["workAllocationId"]!.GetValue<int>();
        int Qty(JsonNode n) => n["quantity"]!.GetValue<int>();

        p.Status(await Post(Acc.WorkerA, $"worker/work-allocations/{Id(other)}/confirm", new { confirmedQty = Qty(other) }), "A confirm phiếu B", 403, 404);
        p.Status(await Post(Acc.WorkerA, $"worker/work-allocations/{Id(other)}/reject", new { reason = "NFR idor" }), "A reject phiếu B", 403, 404);

        var batch = await Post(Acc.WorkerA, "worker/work-allocations/confirm-batch",
            new { items = mine.Append(other).Select(n => new { workAllocationId = Id(n), confirmedQty = Qty(n) }).ToArray() });
        p.Check(batch.Is(400, 403) || batch.Success, "confirm-batch lẫn ID của B → 400/403 hoặc chỉ xử lý ID của A", batch);

        var bNow = (await Pending(Acc.WorkerB)).Any(n => Id(n) == Id(other));
        p.Check(bNow, "Phiếu của B vẫn PENDING");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_016_LeaderCannotTouchOtherTeamDocuments()
    {
        var p = new Probe("NFR_G3_016");
        int[] blocked = { 403, 404 };
        // Đối tượng của TEAM_B (MAY2 / tổ cắt), đọc bằng đúng chủ sở hữu để lấy body hợp lệ
        var issue = await Seed(Acc.Leader2, "team-leader/material-issues/39");
        var ret = await Seed(Acc.Keeper, "warehouse-keeper/material-returns/7");
        var lay = await Seed(Acc.CutLeader, "team-leader/cutting-lays/28");
        var handover = await Seed(Acc.CutLeader, "team-leader/cut-handovers/15");
        async Task<JsonNode> Assignments() => await Seed(Acc.Leader2, "team-leader/operation-assignments?orderId=7");
        async Task<JsonNode> Defect() => Items(await Seed(Acc.Leader2, "team-leader/defect-records?PageSize=100")).First(d => d!["defectRecordId"]!.GetValue<int>() == 22)!;
        async Task<JsonNode> Slip() => Items(await Seed(Acc.Leader2, "team-leader/work-allocations?Status=PENDING&PageSize=100")).FirstOrDefault(d => d!["workAllocationId"]!.GetValue<int>() == 719)
                                       ?? throw new InvalidOperationException("THIẾU SEED: phiếu 719 của MAY2 không còn PENDING.");
        var assignments = await Assignments();
        var defect = await Defect();
        var slip = await Slip();

        p.Status(await Get(Acc.Leader, "team-leader/material-issues/39"), "GET material-issues/{B}", blocked);
        p.Status(await Get(Acc.Leader, "team-leader/material-issues/39/excel"), "GET material-issues/{B}/excel", blocked);
        var lines = issue["lines"]!.AsArray().Select(l => new { materialIssueLineId = l!["materialIssueLineId"]!.GetValue<int>(), confirmedQty = 1 }).ToArray();
        p.Status(await Post(Acc.Leader, "team-leader/material-issues/39/confirm", new { lines }), "confirm material-issues/{B}", blocked);
        p.Status(await Post(Acc.Leader, "team-leader/material-issues/39/reject", new { reason = "NFR idor" }), "reject material-issues/{B}", blocked);
        p.Status(await Get(Acc.Leader, "team-leader/material-returns/7"), "GET material-returns/{B}", blocked);
        p.Status(await Post(Acc.Leader, "team-leader/material-returns/7/cancel"), "cancel material-returns/{B}", blocked);
        p.Status(await Get(Acc.Leader, "team-leader/cutting-lays/28"), "GET cutting-lays/{B}", blocked);
        p.Status(await Put(Acc.Leader, "team-leader/cutting-lays/28", lay), "PUT cutting-lays/{B}", blocked);
        p.Status(await Get(Acc.Leader, "team-leader/cut-handovers/15"), "GET cut-handovers/{B}", blocked);
        p.Status(await Post(Acc.Leader, "team-leader/cut-handovers/15/cancel"), "cancel cut-handovers/{B}", blocked);
        p.Status(await Post(Acc.Leader, "team-leader/operation-assignments/80/replace", new { newWorkerId = Acc.WorkerAId }), "replace operation-assignments/{B}", blocked);
        p.Status(await Post(Acc.Leader, "team-leader/operation-assignments/80/close"), "close operation-assignments/{B}", blocked);
        var rowVer = defect["rowVer"]?.GetValue<string>();
        p.Status(await Put(Acc.Leader, "team-leader/defect-records/22", new
        {
            shiftCode = defect["shiftCode"]!.GetValue<string>(), operationId = defect["operationId"]!.GetValue<int>(),
            responsibleWorkerId = defect["responsibleWorkerId"]!.GetValue<int>(), defectType = defect["defectType"]!.GetValue<string>(),
            quantity = 1, reasonCode = defect["defectReasonCode"]?.GetValue<string>(), reason = "NFR idor", rowVer,
        }), "PUT defect-records/{B}", blocked);
        p.Status(await Post(Acc.Leader, "team-leader/defect-records/22/cancel", new { reason = "NFR idor", rowVer }), "cancel defect-records/{B}", blocked);
        p.Status(await Post(Acc.Leader, "team-leader/work-allocations/719/confirm-on-behalf", new { confirmedQty = 3, onBehalfReason = "NFR idor" }), "confirm-on-behalf work-allocations/{B}", blocked);

        p.Check(Snap(issue) == Snap(await Seed(Acc.Leader2, "team-leader/material-issues/39")), "Phiếu cấp của B không đổi");
        p.Check(Snap(ret) == Snap(await Seed(Acc.Keeper, "warehouse-keeper/material-returns/7")), "Phiếu trả của B không đổi");
        p.Check(Snap(lay) == Snap(await Seed(Acc.CutLeader, "team-leader/cutting-lays/28")), "Bàn cắt không đổi");
        p.Check(Snap(handover) == Snap(await Seed(Acc.CutLeader, "team-leader/cut-handovers/15")), "Bàn giao cắt không đổi");
        p.Check(Snap(assignments) == Snap(await Assignments()), "Phân công công đoạn của B không đổi");
        p.Check((await Defect())["quantity"]!.GetValue<int>() == defect["quantity"]!.GetValue<int>(), "Phiếu lỗi của B không đổi");
        p.Check((await Slip())["status"]?.ToJsonString() == slip["status"]?.ToJsonString(), "Phiếu giao việc của B không đổi");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_017_LeaderCannotActOnOtherTeamWorker()
    {
        var p = new Probe("NFR_G3_017");
        async Task<int> PendingCount() => Total(await Seed(Acc.Leader2, "team-leader/work-allocations?Status=PENDING&WorkerId=33&PageSize=100"));
        var before = await PendingCount();
        if (before == 0) throw new InvalidOperationException("THIẾU SEED: worker 33 (MAY2) không còn phiếu PENDING.");
        p.Status(await Put(Acc.Leader, $"team-leader/grade-suggestions/items/{Acc.WorkerBId}", new { proposedGradeCode = "A", reason = "NFR idor" }), "PUT grade-suggestions/items/{worker B}", 403, 404);
        p.Status(await Delete(Acc.Leader, $"team-leader/grade-suggestions/items/{Acc.WorkerBId}"), "DELETE grade-suggestions/items/{worker B}", 403, 404);
        p.Status(await Post(Acc.Leader, "team-leader/work-allocations/workers/33/confirm-on-behalf", new { onBehalfReason = "NFR idor" }), "confirm-on-behalf worker B", 403, 404);
        p.Check(before == await PendingCount(), "Phiếu PENDING của worker B không đổi");
        p.Note("Spec ghi GET grade-suggestions/items/{workerId}; API chỉ có PUT/DELETE");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_018_LeaderListsStayInOwnTeam()
    {
        var p = new Probe("NFR_G3_018");
        var lists = new[]
        {
            "team-leader/work-allocations", "team-leader/work-allocations/workers", "team-leader/defect-records",
            "team-leader/material-issues", "team-leader/material-returns", "team-leader/operation-assignments?orderId=7",
            "team-leader/piece-holdings", "team-leader/work-allocations/board?orderId=7",
        };
        var foreign = new Regex("\"(sewingTeamId|receivingTeamId|teamId)\":4[,}]|\"teamCode\":\"MAY2\"|\"workerCode\":\"CNMAY(9|1[0-6])\"");
        foreach (var list in lists)
            foreach (var q in new[] { "teamId=4", "teamId=", "teamId=0", "teamId=-1", "sewingTeamId=4", $"workerId={Acc.WorkerBId}" })
            {
                var r = await Get(Acc.Leader, list + (list.Contains('?') ? "&" : "?") + q + "&PageSize=100");
                if (r.Status == 200) p.Check(!foreign.IsMatch(r.Text), $"{list} ?{q} lộ dữ liệu tổ B", r);
                else p.Status(r, $"{list} ?{q}", 400, 403);
            }
        p.Done();
    }

    private static readonly (string OwnerType, int OwnerId, int AttachmentId)[] ForeignFiles =
    {
        ("PurchaseOrder", 2, 15), ("DispatchNote", 1, 0), ("MaterialReceipt", 17, 27), ("VehicleTrip", 1, 35), ("Request", 2, 64),
    };

    [Fact]
    public async Task NFR_G3_019_WorkerCannotReadForeignAttachments()
    {
        var p = new Probe("NFR_G3_019");
        foreach (var (type, ownerId, attachmentId) in ForeignFiles)
        {
            var control = await Get(Acc.Admin, $"common/attachments/{attachmentId}/content");
            if (!control.Success) throw new InvalidOperationException($"THIẾU SEED: Admin không tải được file #{attachmentId} → {control.Status}");
            var list = await Get(Acc.WorkerA, $"common/attachments?ownerType={type}&ownerId={ownerId}");
            p.Check(list.Is(403, 404) || (list.Status == 200 && Items(list.Data!).Count == 0), $"Worker list {type}/{ownerId} phải bị chặn hoặc rỗng", list);
            var content = await Get(Acc.WorkerA, $"common/attachments/{attachmentId}/content");
            p.Status(content, $"Worker tải file #{attachmentId} ({type}), Admin đọc → {control.Status}", 403, 404);
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_020_WorkerCannotAttachToForeignObjects()
    {
        var p = new Probe("NFR_G3_020");
        foreach (var (type, ownerId) in new[] { ("PurchaseOrder", 2), ("DispatchNote", 1), ("Request", 2), ("Payroll", 2), ("CustomerOrder", 1), ("NFR_INVALID", 1) })
        {
            var listPath = $"common/attachments?ownerType={type}&ownerId={ownerId}";
            var before = await Get(Acc.Admin, listPath);
            var name = NewDataName("atk") + ".png";
            var r = await Send(Acc.WorkerA, HttpMethod.Post, "common/attachments",
                Multipart(new ByteArrayContent(Png()), name, "image/png", ("ownerType", type), ("ownerId", ownerId.ToString()), ("purpose", "Photo")));
            p.Status(r, $"Worker đính kèm vào {type}/{ownerId}", 403, 400);
            var after = await Get(Acc.Admin, listPath);
            p.Check(!after.Text.Contains(name), $"File không xuất hiện ở {type}/{ownerId} (trước {before.Status}, sau {after.Status})");
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_021_WorkerCannotDeleteOthersAttachments()
    {
        var p = new Probe("NFR_G3_021");
        var bRequest = await CreateOwnRequest(Acc.WorkerB, NewDataName("att"));
        var up = await Send(Acc.WorkerB, HttpMethod.Post, "common/attachments",
            Multipart(new ByteArrayContent(Png()), NewDataName() + ".png", "image/png", ("ownerType", "Request"), ("ownerId", bRequest.ToString()), ("purpose", "Photo")));
        var bFile = (up.Data is System.Text.Json.Nodes.JsonValue fv ? fv.GetValue<int>() : up.Data?["attachmentId"]?.GetValue<int>()) ?? throw new InvalidOperationException($"THIẾU SEED: B tải file → {up.Status} {up.Text}");
        foreach (var (id, owner, who) in new[] { (bFile, Acc.WorkerB, "B"), (15, Acc.Accountant, "Kế toán") })
        {
            if (!(await Get(owner, $"common/attachments/{id}/content")).Success)
                throw new InvalidOperationException($"THIẾU SEED: {owner} không tải được file #{id}.");
            p.Status(await Delete(Acc.WorkerA, $"common/attachments/{id}"), $"Worker A xóa file của {who}", 403, 404);
            p.Status(await Get(owner, $"common/attachments/{id}/content"), $"File của {who} vẫn tải được", 200);
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_022_WorkerLookups_NoSensitiveFields()
    {
        var p = new Probe("NFR_G3_022");
        var sensitive = new Regex("\"([A-Za-z]*(price|cost|amount|salary|netPay|unitRate|payment|revenue)[A-Za-z]*)\"\\s*:", RegexOptions.IgnoreCase);
        var called = 0;
        foreach (var path in new[]
                 {
                     "common/lookups/orders", "common/lookups/orders/4/lines", "common/lookups/order-lines/28/components", "common/lookups/teams",
                     "common/lookups/materials", "common/lookups/scrap-materials", "common/lookups/requisition-materials?orderId=4",
                     "common/lookups/orders/4/requisition-pieces",
                 })
        {
            var r = await Get(Acc.WorkerA, path);
            if (r.Status == 200) called++;
            var hits = sensitive.Matches(r.Text).Select(m => m.Groups[1].Value).Distinct().ToList();
            p.Check(hits.Count == 0 && r.Status < 500, $"{path} lộ trường {string.Join(",", hits)}", r);
        }
        p.Note($"{called}/8 lookup trả 200 cho Worker. Danh sách trường được phép nhóm chưa chốt: chỉ kiểm trường giá / chi phí / lương");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_023_PaymentIdMustBelongToParent()
    {
        var p = new Probe("NFR_G3_023");
        var x = await Seed(Acc.Accountant, "accountant/order-payments/1");
        var y = await Seed(Acc.Accountant, "accountant/order-payments/2");
        var pay = y["payments"]!.AsArray()[0]!;
        object Body(JsonNode pm, JsonNode parent) => new
        {
            paymentDate = pm["paymentDate"]!.GetValue<string>(), amount = 1000, paymentMethod = pm["paymentMethod"]!.GetValue<string>(),
            paymentNote = "NFR idor", reason = "NFR idor", rowVer = parent["rowVer"]!.GetValue<string>(),
        };
        p.Status(await Put(Acc.Accountant, $"accountant/order-payments/1/payments/{pay["orderPaymentId"]}", Body(pay, x)), "order-payments X + paymentId của Y", 404);
        p.Check(Snap(y["payments"]!) == Snap((await Seed(Acc.Accountant, "accountant/order-payments/2"))["payments"]!), "Payment của đơn Y không đổi");

        var withPayments = new List<JsonNode>();
        foreach (var row in Items(await Seed(Acc.Accountant, "accountant/purchase-payments?PageSize=100")))
        {
            var detail = await Seed(Acc.Accountant, $"accountant/purchase-payments/{row!["purchaseOrderId"]}");
            if (detail["payments"] is JsonArray { Count: > 0 }) withPayments.Add(detail);
            if (withPayments.Count == 2) break;
        }
        if (withPayments.Count < 2) throw new InvalidOperationException("THIẾU SEED: cần 2 đơn mua có khoản thanh toán chi tiết.");
        var (px, py) = (withPayments[0], withPayments[1]);
        var pp = py["payments"]!.AsArray()[0]!;
        var ppId = pp["purchasePaymentId"] ?? pp["paymentId"] ?? pp["id"];
        p.Status(await Put(Acc.Accountant, $"accountant/purchase-payments/{px["purchaseOrderId"]}/payments/{ppId}", Body(pp, px)), "purchase-payments X + paymentId của Y", 404);
        p.Check(Snap(py["payments"]!) == Snap((await Seed(Acc.Accountant, $"accountant/purchase-payments/{py["purchaseOrderId"]}"))["payments"]!), "Payment của đơn mua Y không đổi");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_024_MarkerProductMustBelongToOrder()
    {
        var p = new Probe("NFR_G3_024");
        var order8 = await Seed(Acc.Manager, "manager/order-markers/8");
        var order7 = await Seed(Acc.Manager, "manager/order-markers/7");
        var markers = order8["products"]!.AsArray().First(x => x!["productId"]!.GetValue<int>() == 1)!["markers"]!.DeepClone();
        p.Status(await Put(Acc.Manager, "manager/order-markers/7/products/1", new { markers }), "PUT order 7 + productId của đơn 8", 404);
        p.Check(Snap(order8) == Snap(await Seed(Acc.Manager, "manager/order-markers/8")), "Bìa của đơn 8 không đổi");
        p.Check(Snap(order7) == Snap(await Seed(Acc.Manager, "manager/order-markers/7")), "Bìa của đơn 7 không đổi");
        p.Note("API không có GET theo productId, chỉ kiểm PUT");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_025_QaCannotCancelOtherInspectorsDocument()
    {
        var p = new Probe("NFR_G3_025");
        var doc = await Seed(Acc.Qa, "qa-qc/fg-handovers/12");
        if (doc["status"]!.GetValue<string>() != "PENDING" || doc["inspectorId"]!.GetValue<int>() != Acc.QaId)
            throw new InvalidOperationException("THIẾU SEED: cần phiếu bàn giao 12 PENDING của QAQC1.");
        var r = await Post(Acc.Qa2, "qa-qc/fg-handovers/12/cancel");
        if (r.Status == 403) p.Check(true, "QaQc B bị chặn", r);
        else
        {
            p.Check(r.Status is > 0 and < 500, "Không được 500", r);
            var editor = await EmployeeId(Acc.Qa2);
            var audit = Items(await Seed(Acc.Admin, "admin/audit-logs?PageSize=50"));
            p.Check(!r.Success || audit.Any(a => a!["recordId"]?.ToString() == "12" && a["changedBy"]?.GetValue<int>() == editor),
                "Không chặn 403 thì audit log phải ghi người sửa", r);
            p.Note($"QaQc khác người tạo hủy được: HTTP {r.Status}. Nhóm chưa chốt quy tắc chỉ người tạo được sửa");
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_026_PayrollRoleAndMissingIds()
    {
        var p = new Probe("NFR_G3_026");
        var list = Items(await Seed(Acc.Admin, "admin/payrolls"));
        var pending = list.First(x => x!["status"]!.GetValue<string>() == "PendingApproval")!["payrollId"]!.GetValue<int>();
        var approved = list.First(x => x!["status"]!.GetValue<string>() == "Approved")!["payrollId"]!.GetValue<int>();
        p.Status(await Post(Acc.Accountant, $"admin/payrolls/{pending}/approve"), "Accountant approve", 403);
        p.Check((await Seed(Acc.Admin, $"admin/payrolls/{pending}")).ToJsonString().Contains("PendingApproval"), "Bảng lương vẫn chờ duyệt");
        foreach (var user in new[] { Acc.Manager, Acc.WorkerA })
        {
            var r = await Get(user, $"accountant/payrolls/{approved}/file");
            p.Status(r, $"{user} tải file lương", 403);
        }
        p.Status(await Post(Acc.Admin, "admin/payrolls/999999/approve"), "approve id không tồn tại", 404);
        p.Status(await Post(Acc.Accountant, "accountant/payrolls/999999/submit"), "submit id không tồn tại", 404);
        p.Status(await Get(Acc.Accountant, "accountant/payrolls/999999/file"), "file id không tồn tại", 404);
        p.Done();
    }
}
