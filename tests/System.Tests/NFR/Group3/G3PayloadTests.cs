using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace System.Tests.NFR.Group3;

/// <summary>G3-08, G3-09: upload, payload lớn, validation đầu vào và cấu hình.</summary>
[Collection("NFR_G3")]
public sealed class G3PayloadTests : G3Base
{
    private const int LimitBytes = 5 * 1024 * 1024; // Attachment:MaxFileMb và MAX_IMAGE_BYTES của FE

    private static Task<Resp> Attach(string user, int requestId, byte[] data, string name, string type) =>
        Send(user, HttpMethod.Post, "common/attachments",
            Multipart(new ByteArrayContent(data), name, type, ("ownerType", "Request"), ("ownerId", requestId.ToString()), ("purpose", "Photo")), expectContinue: true);

    private static Task<Resp> SampleImage(byte[] data, string name, string type) =>
        Send(Acc.Admin, HttpMethod.Post, "admin/products/sample-image", Multipart(new ByteArrayContent(data), name, type), expectContinue: true);

    [Fact]
    public async Task NFR_G3_039_UploadSizeBoundary()
    {
        var p = new Probe("NFR_G3_039");
        var request = await CreateOwnRequest(Acc.WorkerA, NewDataName("size"));
        foreach (var (size, label) in new[] { (LimitBytes, "= 5 MB"), (LimitBytes + 1, "5 MB + 1 byte") })
        {
            var results = new[]
            {
                ("attachments", await Attach(Acc.WorkerA, request, Png(size), "nfr.png", "image/png")),
                ("sample-image", await SampleImage(Png(size), "nfr.png", "image/png")),
            };
            foreach (var (name, r) in results)
                if (size == LimitBytes) p.Check(r.Success, $"{name} {label} (cần 2xx)", r);
                else p.Status(r, $"{name} {label}", 413);
        }
        p.Done();
    }

    private static readonly (string Path, string User)[] Imports =
    {
        ("admin/employee-import/preview", Acc.Admin), ("admin/employee-import", Acc.Admin),
        ("manager/employee-import/preview", Acc.Manager), ("manager/employee-import", Acc.Manager),
        ("admin/customer-orders/import/preview", Acc.Admin),
        ("admin/order-materials/import/preview?orderId=7", Acc.Admin), ("admin/order-materials/import?orderId=7", Acc.Admin),
        ("manager/order-materials/import/preview?orderId=7", Acc.Manager), ("manager/order-materials/import?orderId=7", Acc.Manager),
        ("admin/products/excel/parse", Acc.Admin), ("admin/products/3/import/preview", Acc.Admin), ("admin/products/3/import", Acc.Admin),
        ("manager/order-markers/excel/parse", Acc.Manager),
        ("manager/order-markers/7/products/4/import/preview", Acc.Manager), ("manager/order-markers/7/products/4/import", Acc.Manager),
        ("accountant/operations/import/preview?productId=3", Acc.Accountant), ("accountant/operations/import?productId=3", Acc.Accountant),
        ("accountant/monthly-costs/import/preview?periodMonth=2026-11", Acc.Accountant), ("accountant/monthly-costs/import?periodMonth=2026-11", Acc.Accountant),
        ("accountant/payrolls/import/preview", Acc.Accountant), ("accountant/payrolls/import?periodMonth=2026-11", Acc.Accountant),
        ("accountant/attendance/import/preview", Acc.Admin), ("accountant/attendance/import", Acc.Admin),
    };

    /// <summary>File .xlsx giả: magic ZIP + dữ liệu ngẫu nhiên (không nén được).</summary>
    private static string TempBlob(int megabytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"NFR_AUTO_{megabytes}mb.xlsx");
        using var fs = File.Create(path);
        var chunk = new byte[1024 * 1024];
        RandomNumberGenerator.Fill(chunk);
        for (var i = 0; i < megabytes; i++)
        {
            if (i == 0) { var head = (byte[])chunk.Clone(); head[0] = 0x50; head[1] = 0x4B; head[2] = 3; head[3] = 4; fs.Write(head); }
            else fs.Write(chunk);
        }
        return path;
    }

    [Fact]
    public async Task NFR_G3_040_OversizedImports()
    {
        var p = new Probe("NFR_G3_040");
        var peak = 0.0;
        foreach (var mb in new[] { 50, 200 })
        {
            var file = TempBlob(mb);
            RegisterCleanup($"xóa file {mb} MB", () => { File.Delete(file); return Task.CompletedTask; });
            foreach (var (path, user) in Imports)
            {
                using var ram = new RamMonitor();
                var r = await Send(user, HttpMethod.Post, path, Multipart(new StreamContent(File.OpenRead(file)), "big.xlsx", XlsxType), expectContinue: true);
                var delta = ram.PeakDeltaMb();
                peak = Math.Max(peak, delta);
                p.Check(r.Is(413, 400) && r.Ms < 10_000, $"{mb} MB {path}: 413/400 < 10 s (đo {r.Ms:0} ms)", r);
                p.Check(delta <= 500, $"{mb} MB {path}: RAM +{delta:0} MB (cần ≤ 500)");
                p.Check(ram.Alive, $"{mb} MB {path}: API không restart");
            }
        }
        p.Note($"{Imports.Length} API import; RAM đỉnh +{peak:0} MB");
        p.Done();
    }

    /// <summary>xlsx hợp lệ về cấu trúc, sharedStrings giải nén &gt; 1 GB, file nén khoảng 1 MB.</summary>
    private static byte[] ZipBomb()
    {
        var basis = Xlsx(new[] { "A" }, new object[] { "x" });
        var ms = new MemoryStream();
        ms.Write(basis);
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry("xl/sharedStrings.xml")?.Delete();
        }
        var output = new MemoryStream();
        ms.Position = 0;
        using (var source = new ZipArchive(ms, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName, CompressionLevel.SmallestSize).Open();
                from.CopyTo(to);
            }
            using var bomb = target.CreateEntry("xl/sharedStrings.xml", CompressionLevel.SmallestSize).Open();
            bomb.Write(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"1\" uniqueCount=\"1\"><si><t>"));
            var chunk = Encoding.ASCII.GetBytes(new string('A', 1024 * 1024));
            for (var i = 0; i < 1100; i++) bomb.Write(chunk);
            bomb.Write(Encoding.UTF8.GetBytes("</t></si></sst>"));
        }
        return output.ToArray();
    }

    [Fact]
    public async Task NFR_G3_041_ZipBomb()
    {
        var p = new Probe("NFR_G3_041");
        var bomb = ZipBomb();
        var peak = 0.0;
        var targets = new[]
        {
            "admin/employee-import/preview", "admin/customer-orders/import/preview", "admin/products/excel/parse",
            "manager/order-markers/excel/parse", "accountant/payrolls/import/preview", "accountant/attendance/import/preview",
        };
        foreach (var path in targets)
        {
            var (_, user) = Imports.First(i => i.Path.Split('?')[0] == path);
            bool killed;
            using (var ram = new RamMonitor())
            {
                var r = await Send(user, HttpMethod.Post, path, Multipart(new ByteArrayContent(bomb), "bomb.xlsx", XlsxType));
                var delta = ram.PeakDeltaMb();
                peak = Math.Max(peak, delta);
                p.Check(r.Status == 400 && r.Ms < 10_000, $"{path}: 400 < 10 s (đo {r.Ms:0} ms)", r);
                p.Check(delta <= 200, $"{path}: RAM +{delta:0} MB (cần ≤ 200)");
                p.Check(ram.Alive, $"{path}: API không bị sập");
                killed = ram.Killed || !ram.Alive;
            }
            if (killed)
            {
                p.Note($"Dừng sau {path} vì API phải khởi động lại (chặn bảo vệ máy 2,5 GB)");
                break;
            }
        }
        p.Note($"File nén {bomb.Length / 1024} KB, giải nén > 1,1 GB; RAM đỉnh +{peak:0} MB");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_042_FakeFilesAndTraversalNames()
    {
        var p = new Probe("NFR_G3_042");
        var request = await CreateOwnRequest(Acc.WorkerA, NewDataName("mime"));
        var exe = new byte[4096];
        exe[0] = (byte)'M'; exe[1] = (byte)'Z';
        p.Status(await Attach(Acc.WorkerA, request, exe, "virus.png", "image/png"), "attachments: .exe đội lốt png", 400);
        p.Status(await SampleImage(exe, "virus.png", "image/png"), "sample-image: .exe đội lốt png", 400);
        foreach (var (path, user) in Imports.Where(i => i.Path.Contains("preview") || i.Path.Contains("parse")))
            p.Status(await Send(user, HttpMethod.Post, path, Multipart(new ByteArrayContent(exe), "virus.xlsx", XlsxType)), $"{path}: .exe đội lốt xlsx", 400);

        var started = DateTime.UtcNow;
        foreach (var name in new[] { "../../appsettings.json.png", "..\\..\\web.config.png", "a.png%00.exe" })
        {
            var r = await Attach(Acc.WorkerA, request, Png(), name, "image/png");
            if (!p.Check(r.Success || r.Status == 400, $"upload tên '{name}': chấp nhận (đã làm sạch) hoặc 400", r)) continue;
        }
        var stored = Items(await Seed(Acc.WorkerA, $"common/attachments?ownerType=Request&ownerId={request}"));
        foreach (var f in stored)
        {
            var fileName = f!["fileName"]?.GetValue<string>() ?? "";
            p.Check(!fileName.Contains("..") && !fileName.Contains('/') && !fileName.Contains('\\'), $"tên lưu '{fileName}' phải sạch");
        }

        // Không file nào bị ghi ra ngoài thư mục lưu trữ
        var storage = Path.GetFullPath(G3Env.Required("NFR_STORAGE_ROOT"));
        var escaped = new[] { Directory.GetParent(storage)!.FullName, Directory.GetParent(storage)!.Parent!.FullName }
            .SelectMany(d => Directory.GetFiles(d))
            .Where(f => Regex.IsMatch(Path.GetFileName(f), @"(web\.config|appsettings\.json)", RegexOptions.IgnoreCase) && File.GetLastWriteTimeUtc(f) >= started)
            .ToList();
        p.Check(escaped.Count == 0, $"File bị ghi ra ngoài thư mục lưu trữ: {string.Join(", ", escaped)}");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_043_ExportEscapesFormulas()
    {
        var p = new Probe("NFR_G3_043");
        var months = new[] { Month(2), Month(1), Month() };
        async Task<List<IXLCell>> ExportCells()
        {
            var cells = new List<IXLCell>();
            foreach (var month in months)
            {
                var r = await Get(Acc.Accountant, $"accountant/accounting-reports/export?periodMonth={month}");
                if (!r.Success) continue;
                var wb = new XLWorkbook(new MemoryStream(r.Bytes));
                cells.AddRange(wb.Worksheets.SelectMany(w => w.CellsUsed()));
            }
            return cells;
        }

        var baseline = (await ExportCells()).Where(c => !c.HasFormula).Select(c => c.GetString()).ToHashSet();
        var vehicle = Items(await Seed(Acc.Accountant, "accountant/vehicles?PageSize=100")).Select(c => c!).FirstOrDefault(c => baseline.Contains(c["vehicleType"]!.GetValue<string>()))
                      ?? throw new InvalidOperationException("THIẾU SEED: không có xe nào xuất hiện trong file xuất sổ xe.");
        var id = vehicle["vehicleId"]!.GetValue<int>();
        object Body(string type) => new { plateNumber = vehicle["plateNumber"]!.GetValue<string>(), vehicleType = type, fuelNormPer100Km = vehicle["fuelNormPer100Km"]?.GetValue<decimal>() };
        var original = vehicle["vehicleType"]!.GetValue<string>();
        RegisterCleanup("trả loại xe", () => Put(Acc.Accountant, $"accountant/vehicles/{id}", Body(original)));

        foreach (var payload in new[] { "=HYPERLINK(\"http://evil\")", "+SUM(1)", "-1+1", "@SUM(1)" })
        {
            var save = await Put(Acc.Accountant, $"accountant/vehicles/{id}", Body(payload));
            if (save.Status == 400) { p.Check(true, $"{payload} bị chặn từ đầu vào", save); continue; }
            Assert.True(save.Success, $"THIẾU SEED: đổi loại xe → {save.Status} {save.Text}");
            var cells = await ExportCells();
            var hit = cells.Where(c => (c.HasFormula ? c.FormulaA1 : c.GetString()).Contains(payload.TrimStart('='))).ToList();
            p.Check(hit.Count > 0 && hit.All(c => !c.HasFormula && c.DataType == XLDataType.Text), $"ô '{payload}' phải là text trong file xuất (tìm thấy {hit.Count} ô, công thức {hit.Count(c => c.HasFormula)})", save);
        }
        p.Note("Không có API xuất cho mã hàng / đơn hàng: kiểm qua trường Loại xe trong accountant/accounting-reports/export (không đi qua đường import)");
        p.Done();
    }

    private static readonly (string Path, string User)[] SearchLists =
    {
        ("admin/audit-logs", Acc.Admin), ("common/notifications", Acc.Admin), ("common/requests", Acc.WorkerA), ("common/request-inbox", Acc.Admin),
        ("admin/employees", Acc.Admin), ("manager/employees", Acc.Manager), ("admin/materials", Acc.Admin), ("manager/materials", Acc.Manager),
        ("admin/order-materials", Acc.Admin), ("manager/order-materials", Acc.Manager), ("admin/customer-orders", Acc.Admin),
        ("admin/products", Acc.Admin), ("admin/grade-evaluations", Acc.Admin), ("warehouse-keeper/material-receipts", Acc.Keeper),
        ("warehouse-keeper/material-issues", Acc.Keeper),
    };

    [Fact]
    public async Task NFR_G3_044_SearchSqlInjection()
    {
        var p = new Probe("NFR_G3_044");
        var skipped = new List<string>();
        foreach (var (path, user) in SearchLists)
        {
            var all = await Get(user, path + "?PageSize=100");
            if (!all.Success || all.Data is not { } data || (data is not JsonArray && data["items"] is null)) { skipped.Add($"{path}={all.Status}"); continue; }
            var baseline = Total(data);
            foreach (var payload in new[] { "' OR 1=1--", "'; WAITFOR DELAY '0:0:5'--", "%' AND 1=CONVERT(int,@@version)--" })
            {
                var q = Uri.EscapeDataString(payload);
                var r = await Get(user, $"{path}?Keyword={q}&search={q}&PageSize=100");
                var ok = r.Status == 400 || (r.Status == 200 && Total(r.Data!) <= baseline);
                p.Check(ok && r.Ms < 800, $"{path} '{payload}' (đo {r.Ms:0} ms)", r);
            }
        }
        if (skipped.Count > 0) p.Note("Bỏ qua do không đọc được danh sách nền: " + string.Join(", ", skipped));
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_046_OutOfRangeValues_400()
    {
        var p = new Probe("NFR_G3_046");
        foreach (var path in new[] { "admin/employees", "admin/audit-logs", "admin/materials", "admin/customer-orders", "common/notifications" })
            foreach (var size in new[] { "-1", "0", "2147483647" })
                p.Status(await Get(Acc.Admin, $"{path}?Page=1&PageSize={size}"), $"{path} pageSize={size}", 400);
        foreach (var date in new[] { ("0001-01-01", "0001-01-02"), ("9999-12-30", "9999-12-31"), ("0001-01-01", "9999-12-31") })
            p.Status(await Get(Acc.Admin, $"accountant/attendance/timesheets?FromDate={date.Item1}&ToDate={date.Item2}"), $"timesheets {date.Item1}..{date.Item2}", 400);
        foreach (var qty in new[] { "-1", "0", "1000000000000000000" })
            p.Status(await Send(Acc.Leader, HttpMethod.Post, "team-leader/work-allocations",
                Raw($"{{\"movementType\":\"HANDOVER\",\"workerId\":{Acc.WorkerAId},\"orderLineId\":20,\"operationId\":25,\"quantity\":{qty}}}", "application/json")), $"giao việc quantity={qty}", 400);
        foreach (var amount in new[] { "0", "1e30", "99999999999999999999999999" })
            p.Status(await Send(Acc.Accountant, HttpMethod.Put, "accountant/order-payments/3/final-amount",
                Raw($"{{\"amount\":{amount},\"reason\":\"NFR_AUTO_\"}}", "application/json")), $"final-amount={amount}", 400);
        foreach (var width in new[] { "-1", "1e30" })
        {
            var name = NewDataName("w");
            p.Status(await Send(Acc.Admin, HttpMethod.Post, "admin/materials",
                Raw($"{{\"materialCode\":\"{name[..Math.Min(20, name.Length)]}\",\"materialName\":\"{name}\",\"materialType\":\"FABRIC\",\"unit\":\"m\",\"color\":\"Đen\",\"widthM\":{width}}}", "application/json")), $"vật tư widthM={width}", 400);
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_047_BulkArrayLimits()
    {
        var p = new Probe("NFR_G3_047");
        var big = Enumerable.Range(1_000_000, 100_000).ToArray();
        var dup = new[] { 999_999, 999_999 };
        var endpoints = new (string Path, string User, Func<int[], object> Body)[]
        {
            ("admin/piece-rates/approve-many", Acc.Admin, ids => new { ids }),
            ("accountant/piece-rates/submit-many", Acc.Accountant, ids => new { ids }),
            ("team-leader/operation-assignments/bulk", Acc.Leader, ids => new { orderId = 3, workerId = Acc.WorkerAId, operationIds = ids }),
            ("manager/order-line-teams/bulk", Acc.Manager, ids => new { orderLineIds = ids, teamIds = ids.Length == 0 ? ids : new[] { 3 } }),
            ("manager/order-materials/batch", Acc.Manager, ids => new { orderId = 7, lines = ids.Select(i => new { materialId = i, requiredQty = 1 }).ToArray() }),
            ("admin/order-materials/batch", Acc.Admin, ids => new { orderId = 7, lines = ids.Select(i => new { materialId = i, requiredQty = 1 }).ToArray() }),
            ("worker/work-allocations/confirm-batch", Acc.WorkerA, ids => new { items = ids.Select(i => new { workAllocationId = i, confirmedQty = 1 }).ToArray() }),
        };
        foreach (var (path, user, body) in endpoints)
        {
            var large = await Post(user, path, body(big));
            p.Check(large.Is(400, 413) && large.Ms < 5000, $"{path} 100.000 phần tử: 400/413 < 5 s (đo {large.Ms:0} ms)", large);
            p.Status(await Post(user, path, body(Array.Empty<int>())), $"{path} mảng rỗng", 400);
            var same = await Post(user, path, body(dup));
            p.Check(same.Status is > 0 and < 500, $"{path} phần tử trùng: không 5xx", same);
        }
        p.Done();
    }

    private static readonly Regex Unsafe = new(@"\bat [A-Za-z_]+\.[A-Za-z_.]+\(|\.cs:line \d+|System\.[A-Z][A-Za-z]+\.|SqlException|Microsoft\.(EntityFrameworkCore|Data\.SqlClient|AspNetCore)|\bdbo\.|[A-Z]:\\\\|Server=.+Database=");

    [Fact]
    public async Task NFR_G3_048_MalformedBodies_SafeErrors()
    {
        var p = new Probe("NFR_G3_048");
        var endpoints = new (string? User, HttpMethod Method, string Path, string WrongType)[]
        {
            (null, HttpMethod.Post, "common/auth/login", "{\"username\":123,\"password\":true}"),
            (Acc.Leader, HttpMethod.Post, "team-leader/work-allocations", "{\"movementType\":\"HANDOVER\",\"quantity\":\"abc\"}"),
            (Acc.Admin, HttpMethod.Post, "admin/materials", "{\"materialName\":{\"a\":1},\"widthM\":\"abc\"}"),
            (Acc.Admin, HttpMethod.Post, "admin/customer-orders", "{\"customerId\":\"abc\",\"lines\":\"qty\"}"),
            (Acc.Accountant, HttpMethod.Put, "accountant/order-payments/3/final-amount", "{\"amount\":\"abc\"}"),
            (Acc.WorkerA, HttpMethod.Patch, "common/my-meals/today", "{\"willEat\":\"abc\"}"),
        };
        foreach (var (user, method, path, wrong) in endpoints)
            foreach (var (label, content) in new[]
                     {
                         ("JSON sai cú pháp", Raw("{\"a\":", "application/json")),
                         ("sai kiểu", Raw(wrong, "application/json")),
                         ("text/plain", Raw("abc", "text/plain")),
                     })
            {
                var r = await Send(user, method, path, content);
                var leak = Unsafe.Match(r.Text);
                p.Check(r.Is(400, 415) && !leak.Success, $"{path} {label}{(leak.Success ? $" lộ '{leak.Value}'" : "")}", r);
            }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_049_CorsPolicy()
    {
        var p = new Probe("NFR_G3_049");
        foreach (var path in new[] { "common/auth/login", "admin/employees" })
        {
            var r = await Send(null, HttpMethod.Options, path, headers: new[] { ("Origin", "http://evil.test"), ("Access-Control-Request-Method", "POST"), ("Access-Control-Request-Headers", "authorization,content-type") });
            string H(string name) => r.Headers is not null && r.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : "";
            var origin = H("Access-Control-Allow-Origin");
            p.Check(r.Status is > 0 and < 500 && origin != "http://evil.test" && !(origin == "*" && H("Access-Control-Allow-Credentials") == "true") && origin != "*",
                $"{path}: Allow-Origin cho origin lạ = '{origin}'", r);
        }
        p.Note("HSTS: CHƯA kiểm được vì môi trường test không có reverse proxy HTTPS");
        p.Done();
    }
}
