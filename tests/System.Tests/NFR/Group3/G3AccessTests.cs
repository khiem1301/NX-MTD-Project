namespace System.Tests.NFR.Group3;

/// <summary>G3-01, G3-02: ma trận role theo prefix và ngoại lệ quyền.</summary>
[Collection("NFR_G3")]
public sealed class G3AccessTests : G3Base
{
    private static readonly (string Prefix, string[] Allowed)[] Matrix =
    {
        ("admin", new[] { "Admin" }),
        ("manager", new[] { "Admin", "Manager" }),
        ("accountant", new[] { "Accountant" }),
        ("warehouse-keeper", new[] { "WarehouseKeeper" }),
        ("team-leader", new[] { "TeamLeader", "TeamDeputy" }),
        ("qa-qc", new[] { "QaQc" }),
        ("worker", new[] { "Worker" }),
        ("electrician", new[] { "Electrician" }),
    };

    private static readonly Dictionary<string, string> ListPath = new()
    {
        ["admin"] = "admin/employees", ["manager"] = "manager/materials", ["accountant"] = "accountant/products",
        ["warehouse-keeper"] = "warehouse-keeper/materials", ["team-leader"] = "team-leader/work-allocations",
        ["qa-qc"] = "qa-qc/qc-batches", ["worker"] = "worker/work-allocations", ["electrician"] = "electrician/ping",
    };

    private static (string Path, object Body)? SamplePost(string prefix)
    {
        var name = NewDataName("rbac");
        object material = new { materialCode = name[..20], materialName = name, materialType = "TRIM", unit = "cái", color = "Đen" };
        return prefix switch
        {
            "admin" => ("admin/customers", new { customerName = name, phone = "0900000111", email = $"{Guid.NewGuid():N}@nfr.test", defaultAddress = name }),
            "manager" => ("manager/materials", material),
            "accountant" => ("accountant/materials", material),
            "warehouse-keeper" => ("warehouse-keeper/materials", material),
            "team-leader" => ("team-leader/work-allocations", new { movementType = "HANDOVER", workerId = Acc.WorkerAId, orderLineId = 20, operationId = 25, quantity = 1 }),
            "qa-qc" => ("qa-qc/qc-batches", new { workDate = DateTime.Today.ToString("yyyy-MM-dd"), shiftCode = "MORNING", batchTime = "09:00:00", sewingTeamId = 3, orderLineId = 33, finishedQty = 1 }),
            "worker" => ("worker/work-allocations/confirm-batch", new { items = new[] { new { workAllocationId = 719, confirmedQty = 3 } } }),
            _ => null,
        };
    }

    private static async Task<string> DataFingerprint()
    {
        var parts = new List<string>();
        foreach (var (user, path) in new[]
                 {
                     (Acc.Admin, "admin/customers?PageSize=1"), (Acc.Admin, "admin/materials?PageSize=1"),
                     (Acc.Leader, "team-leader/work-allocations?PageSize=1"), (Acc.Leader2, "team-leader/work-allocations?Status=PENDING&PageSize=100"),
                     (Acc.Qa, "qa-qc/qc-batches?PageSize=1"),
                 })
            parts.Add($"{path}={Total(await Seed(user, path))}");
        return string.Join(",", parts);
    }

    [Fact]
    public async Task NFR_G3_001_ValidRolePrefixPairs_Return200()
    {
        var p = new Probe("NFR_G3_001");
        foreach (var (prefix, allowed) in Matrix)
            foreach (var (role, user) in Acc.Roles.Where(r => allowed.Contains(r.Role)))
                foreach (var path in new[] { $"{prefix}/home", ListPath[prefix] })
                    p.Status(await Get(user, path), $"{role} GET {path}", 200);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_002_WrongRole_Gets403_AndNothingChanges()
    {
        var p = new Probe("NFR_G3_002");
        var before = await DataFingerprint();
        foreach (var (prefix, allowed) in Matrix)
            foreach (var (role, user) in Acc.Roles.Where(r => !allowed.Contains(r.Role)))
            {
                foreach (var path in new[] { $"{prefix}/home", ListPath[prefix] })
                    p.Status(await Get(user, path), $"{role} GET {path}", 403);
                if (SamplePost(prefix) is { } post)
                    p.Status(await Post(user, post.Path, post.Body), $"{role} POST {post.Path}", 403);
            }
        var after = await DataFingerprint();
        p.Check(before == after, $"Dữ liệu đổi sau POST sai role: {before} ≠ {after}");
        p.Done();
    }

    private static bool AnonymousByDesign(OpenApi.Route r) =>
        r.Template is "/api/v1/common/auth/login" or "/api/v1/common/auth/refresh" || r.Template.StartsWith("/api/v1/agent/attendance-sync/");

    [Fact]
    public async Task NFR_G3_003_NoOrEmptyToken_Gets401()
    {
        var p = new Probe("NFR_G3_003");
        var routes = OpenApi.Routes().Where(r => !AnonymousByDesign(r)).ToList();
        foreach (var r in routes)
        {
            p.Status(await Send(null, new HttpMethod(r.Method), r.Concrete), $"không header {r.Method} {r.Template}", 401);
            p.Status(await Send(null, new HttpMethod(r.Method), r.Concrete, authorization: "Bearer "), $"Bearer rỗng {r.Method} {r.Template}", 401);
        }
        p.Note($"{routes.Count} route");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_004_RouteScan_OnlyLoginAndRefreshAnonymous()
    {
        var p = new Probe("NFR_G3_004");
        var session = await Login("CNCAT2");
        var open = new List<string>();
        var routes = OpenApi.Routes();
        foreach (var r in routes)
        {
            HttpContent? body = r.Template switch
            {
                "/api/v1/common/auth/login" => Json(new { username = "CNCAT2", password = Acc.Password }),
                "/api/v1/common/auth/refresh" => Json(new { refreshToken = session["refreshToken"]!.GetValue<string>() }),
                _ => null,
            };
            var res = await Send(null, new HttpMethod(r.Method), r.Concrete, body);
            var whitelisted = r.Template is "/api/v1/common/auth/login" or "/api/v1/common/auth/refresh";
            if (res.Success) open.Add($"{r.Method} {r.Template}");
            if (!whitelisted) p.Status(res, $"{r.Method} {r.Template}", 401);
        }
        p.Check(open.Count == 2, $"Số route 2xx khi không token = {open.Count} (cần 2): {string.Join(", ", open.Take(5))}");
        p.Note($"{routes.Count} route quét được");
        p.Done();
    }

    private static string TimesheetQuery(string extra = "") =>
        $"accountant/attendance/timesheets?FromDate={DateTime.Today.AddDays(-14):yyyy-MM-dd}&ToDate={DateTime.Today:yyyy-MM-dd}&PageSize=100{extra}";

    [Fact]
    public async Task NFR_G3_005_Timesheets_AllowedRoles_200()
    {
        var p = new Probe("NFR_G3_005");
        foreach (var user in new[] { Acc.Accountant, Acc.Admin, Acc.Manager })
            p.Status(await Get(user, TimesheetQuery()), $"{user} timesheets", 200);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_006_AdminOnlyAttendance_RejectsAccountantAndManager()
    {
        var p = new Probe("NFR_G3_006");
        var file = Xlsx(new[] { "Mã chấm công", "Ngày", "Giờ vào", "Giờ ra" }, new object[] { 999001, "2026-08-02", "07:30", "17:00" });
        Task<Resp> Upload(string user, string path) =>
            Send(user, HttpMethod.Post, path, Multipart(new ByteArrayContent(file), "cc.xlsx", XlsxType));

        foreach (var user in new[] { Acc.Accountant, Acc.Manager, Acc.Admin })
        {
            var calls = new (string Name, Func<Task<Resp>> Call)[]
            {
                ("GET sync-sessions", () => Get(user, "accountant/attendance/sync-sessions")),
                ("POST rj-employees/sync", () => Post(user, "accountant/attendance/rj-employees/sync")),
                ("POST import/preview", () => Upload(user, "accountant/attendance/import/preview")),
                ("POST import", () => Upload(user, "accountant/attendance/import")),
            };
            foreach (var (name, call) in calls)
            {
                var r = await call();
                if (user == Acc.Admin) p.Check(r.Success, $"Admin {name} (cần 2xx)", r);
                else p.Status(r, $"{user} {name}", 403);
            }
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_007_CommonHrLookups_WorkerForbidden()
    {
        var p = new Probe("NFR_G3_007");
        foreach (var path in new[] { "common/grade-code-options", "common/hr-lookups/teams", "common/order-lookups/orders" })
            foreach (var (role, user) in Acc.Roles)
                p.Status(await Get(user, path), $"{role} GET {path}", role == "Worker" ? 403 : 200);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_008_RestrictedCommonApis_Return403()
    {
        var p = new Probe("NFR_G3_008");
        var outsiders = new[] { Acc.Keeper, Acc.Leader, Acc.Deputy, Acc.Qa, Acc.WorkerA, Acc.Electrician };
        foreach (var user in outsiders)
            foreach (var path in new[] { "common/rj-employees", "common/import-templates/spec" })
                p.Status(await Get(user, path), $"{user} GET {path}", 403);
        foreach (var user in new[] { Acc.WorkerA, Acc.Electrician })
            p.Status(await Get(user, "common/request-inbox"), $"{user} GET common/request-inbox", 403);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_009_SewingLeader_CannotUseCuttingApis()
    {
        var p = new Probe("NFR_G3_009");
        foreach (var path in new[] { "team-leader/cut-handovers", "team-leader/cutting-lays", "team-leader/work-orders" })
        {
            p.Status(await Get(Acc.Leader, path), $"TL may GET {path}", 403);
            p.Check((await Get(Acc.CutLeader, path)) is { Success: true }, $"TL cắt GET {path} (cần 2xx)");
        }
        var lay = await Seed(Acc.CutLeader, "team-leader/cutting-lays/28");
        var handover = await Seed(Acc.CutLeader, "team-leader/cut-handovers/15");
        var handoverBody = new
        {
            workOrderId = handover["workOrderId"]!.GetValue<int>(),
            lines = handover["lines"]!.AsArray().Select(l => l!.DeepClone()).ToArray(),
        };
        p.Status(await Post(Acc.Leader, "team-leader/cut-handovers", handoverBody), "TL may POST cut-handovers", 403);
        p.Status(await Post(Acc.Leader, "team-leader/cutting-lays", lay.DeepClone()), "TL may POST cutting-lays", 403);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_010_ManagerTimesheets_ForeignOrUnknownTeam()
    {
        var p = new Probe("NFR_G3_010");
        var unknown = await Get(Acc.Manager, TimesheetQuery("&TeamId=999999"));
        p.Check(unknown.Status == 404 || (unknown.Status == 200 && Items(unknown.Data!).Count == 0), "teamId không tồn tại → 404 hoặc rỗng", unknown);
        var scoped = await Get(Acc.Manager, TimesheetQuery("&TeamId=3"));
        if (p.Check(scoped.Status is 200 or 403, "lọc teamId=3 → 200 hoặc 403", scoped) && scoped.Status == 200)
        {
            var foreign = Items(scoped.Data!).Count(i => i!["teamId"] is { } t && t.GetValue<int>() != 3 || i!["teamCode"] is { } c && c.GetValue<string>() != "MAY1");
            p.Check(foreign == 0, $"{foreign} dòng chấm công ngoài tổ được lọc", scoped);
        }
        p.Note("Nguồn không định nghĩa phạm vi quản lý riêng của Quản đốc");
        p.Done();
    }
}
