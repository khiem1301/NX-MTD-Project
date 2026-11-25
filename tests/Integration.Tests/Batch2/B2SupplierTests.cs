using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_053–069 — Supplier (A/suppliers, M/suppliers). Ca đồng thời 059 nằm ở B2ConcurrencyTests.</summary>
public sealed class B2SupplierTests(ITestOutputHelper output) : B2TestBase(output)
{
    private string Url => A + "suppliers";

    private async Task<Supplier?> FindAsync(string name)
    {
        await using var db = NewDb();
        return await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.SupplierName == name);
    }

    [Fact]
    public async Task IT_B2_053_Create_Supplier_Succeeds_With_Vn_CreatedAt_And_Audit()
    {
        var token = await TkAdm();
        var lastAudit = await LastAuditIdAsync();
        var r = await PostAsync(Url, token,
            SupplierBody("Dệt Phong Phú", "0905000001", "pp@det.vn", "Q9", "Vải, Chỉ", "Giao 7 ngày"));
        ShouldBeCreated(r);
        var row = await FindAsync("Dệt Phong Phú");
        row.Should().NotBeNull();
        row!.Status.Should().Be("ACTIVE");
        row.DiscontinueReason.Should().BeNull();
        row.Phone.Should().Be("0905000001");
        row.Email.Should().Be("pp@det.vn");
        row.Address.Should().Be("Q9");
        row.MaterialTypes.Should().Be("Vải, Chỉ");
        row.Note.Should().Be("Giao 7 ngày");
        AssertCreatedNow(row.CreatedAt);
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_054_Duplicate_Name_Phone_Or_Email_Returns_409()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var bodies = new[]
        {
            SupplierBody("Vải Thành Công", "0905000002", "x1@tc.vn"),
            SupplierBody("NCC trùng SĐT", "0902000001"),
            SupplierBody("NCC trùng mail", email: "su1@tc.vn"),
        };
        foreach (var body in bodies)
        {
            var r = await PostAsync(Url, token, body);
            r.Code.Should().Be(409, r.ToString());
            AssertNoSqlLeak(r);
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_055_Multiple_Suppliers_With_Null_Phone_And_Email_Are_Allowed()
    {
        var token = await TkAdm();
        await using var db = NewDb();
        var nullPhones = await db.Suppliers.AsNoTracking().CountAsync(s => s.Phone == null);
        ShouldBeCreated(await PostAsync(Url, token, SupplierBody("NCC-N1", null, null)));
        ShouldBeCreated(await PostAsync(Url, token, SupplierBody("NCC-N2", null, null)));
        (await db.Suppliers.AsNoTracking().CountAsync(s => s.Phone == null)).Should().Be(nullPhones + 2);
        (await db.Suppliers.AsNoTracking().CountAsync(s => s.Email == null)).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task IT_B2_056_Empty_Phone_And_Email_Are_Never_Stored_As_Empty_String()
    {
        var token = await TkAdm();
        var first = await PostAsync(Url, token, SupplierBody("NCC-E1", "", ""));
        var second = await PostAsync(Url, token, SupplierBody("NCC-E2", "", ""));
        AssertNoSqlLeak(first);
        AssertNoSqlLeak(second);
        first.Code.Should().BeOneOf(new[] { 200, 201 }, first.ToString());
        second.Code.Should().BeOneOf(new[] { 200, 201, 409 }, second.ToString());
        await using var db = NewDb();
        (await db.Suppliers.AsNoTracking().AnyAsync(s => s.Phone == "" || s.Email == "")).Should().BeFalse("không tồn tại dòng nào có Phone / Email = ''");
    }

    [Fact]
    public async Task IT_B2_057_Field_Length_Boundaries()
    {
        var token = await TkAdm();
        var before = await CountAllAsync("Supplier");
        ShouldBeCreated(await PostAsync(Url, token, SupplierBody(Viet(200))));
        (await PostAsync(Url, token, SupplierBody(Viet(201)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, SupplierBody("NCC-MT200", materialTypes: Viet(200))));
        (await PostAsync(Url, token, SupplierBody("NCC-MT201", materialTypes: Viet(201)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, SupplierBody("NCC-N500", note: Viet(500))));
        (await PostAsync(Url, token, SupplierBody("NCC-N501", note: Viet(501)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, SupplierBody("NCC-A500", address: Viet(500))));
        (await PostAsync(Url, token, SupplierBody("NCC-A501", address: Viet(501)))).Code.Should().Be(400);

        (await FindAsync(Viet(200)))!.SupplierName.Length.Should().Be(200);
        (await FindAsync("NCC-MT200"))!.MaterialTypes!.Length.Should().Be(200);
        (await FindAsync("NCC-N500"))!.Note!.Length.Should().Be(500);
        (await FindAsync("NCC-A500"))!.Address!.Length.Should().Be(500);
        foreach (var rejected in new[] { Viet(201), "NCC-MT201", "NCC-N501", "NCC-A501" }) (await FindAsync(rejected)).Should().BeNull();
        (await CountAllAsync("Supplier")).Should().Be(before + 4);
    }

    [Fact]
    public async Task IT_B2_058_Missing_Supplier_Name_Returns_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        foreach (var body in new object[] { SupplierBody(null!), SupplierBody(""), SupplierBody("   ") })
        {
            var r = await PostAsync(Url, token, body);
            r.Code.Should().Be(400, r.ToString());
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_060_Get_Supplier_Returns_Detail_With_Po_Count_And_History()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var r = await GetAsync(Url + "/" + B.Su1.SupplierId, token);
        r.Code.Should().Be(200, r.ToString());
        var supplier = B1Json.Prop(r.Data, "supplier")!.Value;
        B1Json.Int(supplier, "supplierId").Should().Be(B.Su1.SupplierId);
        B1Json.Int(supplier, "purchaseOrderCount").Should().Be(2);
        B1Json.Prop(r.Data, "history")!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        await using var db = NewDb();
        (await db.PurchaseOrders.AsNoTracking().CountAsync(p => p.SupplierId == B.Su1.SupplierId)).Should().Be(2);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_061_Get_Missing_Supplier_Returns_404()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        (await GetAsync(Url + "/" + await MissingIdAsync(), token)).Code.Should().Be(404);
        (await GetAsync(Url + "/abc", token)).Code.Should().Be(404);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_062_Update_Supplier_Then_Conflict_On_Existing_Name()
    {
        var token = await TkAdm();
        var ok = await PutAsync(Url + "/" + B.Su2.SupplierId, token, SupplierBody("Phụ Liệu Minh Long"));
        ok.Code.Should().Be(200, ok.ToString());
        var conflict = await PutAsync(Url + "/" + B.Su2.SupplierId, token, SupplierBody("Vải Thành Công"));
        conflict.Code.Should().Be(409, conflict.ToString());
        AssertNoSqlLeak(conflict);
        (await SupplierAsync(B.Su2.SupplierId))!.SupplierName.Should().Be("Phụ Liệu Minh Long");
    }

    [Fact]
    public async Task IT_B2_063_Discontinue_Without_Reason_Is_Allowed_For_Supplier()
    {
        var token = await TkAdm();
        var other = await PostAsync(Url, token, SupplierBody("NCC-063"));
        ShouldBeCreated(other);
        var otherId = (await FindAsync("NCC-063"))!.SupplierId;

        var withNull = await PostAsync(Url + $"/{B.Su2.SupplierId}/discontinue", token, new { Reason = (string?)null });
        withNull.Code.Should().Be(200, withNull.ToString());
        var emptyBody = await PostAsync(Url + $"/{otherId}/discontinue", token, "{}");
        emptyBody.Code.Should().Be(200, emptyBody.ToString());

        foreach (var id in new[] { B.Su2.SupplierId, otherId })
        {
            var row = (await SupplierAsync(id))!;
            row.Status.Should().Be("DISCONTINUED");
            row.DiscontinueReason.Should().BeNull();
        }
    }

    [Fact]
    public async Task IT_B2_064_Discontinue_Reason_Length_Boundary_500_And_501()
    {
        var token = await TkAdm();
        var reason = Viet(500);
        var ok = await PostAsync(Url + $"/{B.Su2.SupplierId}/discontinue", token, new { Reason = reason });
        ok.Code.Should().Be(200, ok.ToString());
        (await SupplierAsync(B.Su2.SupplierId))!.DiscontinueReason.Should().Be(reason);

        var tooLong = await PostAsync(Url + $"/{B.Su1.SupplierId}/discontinue", token, new { Reason = Viet(501) });
        tooLong.Code.Should().Be(400, tooLong.ToString());
        (await SupplierAsync(B.Su1.SupplierId))!.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B2_065_Discontinue_Supplier_With_Purchase_Orders_Keeps_Them_Intact()
    {
        var token = await TkAdm();
        var r = await PostAsync(Url + $"/{B.Su1.SupplierId}/discontinue", token, new { Reason = "Giá cao" });
        r.Code.Should().BeOneOf(new[] { 200, 409 }, r.ToString());
        Note($"Discontinue NCC có PO → {r.Code}");
        await using var db = NewDb();
        var pos = await db.PurchaseOrders.AsNoTracking().Where(p => B.Su1Pos.Select(x => x.PurchaseOrderId).Contains(p.PurchaseOrderId)).ToListAsync();
        pos.Should().HaveCount(2).And.OnlyContain(p => p.SupplierId == B.Su1.SupplierId, "PO vẫn trỏ SU1");
        var row = (await SupplierAsync(B.Su1.SupplierId))!;
        if (r.Code == 200)
        {
            row.Status.Should().Be("DISCONTINUED");
            row.DiscontinueReason.Should().Be("Giá cao");
        }
        else
        {
            row.Status.Should().Be("ACTIVE");
        }
    }

    [Fact]
    public async Task IT_B2_066_Manager_Supplier_Options_Exclude_Discontinued()
    {
        var adm = await TkAdm();
        var stop = await PostAsync(Url + $"/{B.Su1.SupplierId}/discontinue", adm, new { Reason = "Giá cao" });
        stop.Code.Should().Be(200, "tiền điều kiện IT_B2_065: SU1 đã ngừng. " + stop);

        var r = await GetAsync(M + "suppliers", await TkMgr());
        r.Code.Should().Be(200, r.ToString());
        var ids = Ids(r, "supplierId");
        ids.Should().Contain(B.Su2.SupplierId).And.NotContain(new[] { B.Su1.SupplierId, B.Su3.SupplierId });
        await using var db = NewDb();
        ids.Should().BeEquivalentTo(await db.Suppliers.AsNoTracking().Where(s => s.Status == "ACTIVE").Select(s => s.SupplierId).ToListAsync());
        foreach (var item in r.Items())
            item.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).Should().BeEquivalentTo(new[] { "supplierid", "suppliername" });
    }

    [Fact]
    public async Task IT_B2_067_Reactivate_Discontinued_Supplier_And_Reject_Active_One()
    {
        var token = await TkAdm();
        var ok = await PostAsync(Url + $"/{B.Su3.SupplierId}/reactivate", token);
        ok.Code.Should().Be(200, ok.ToString());
        (await SupplierAsync(B.Su3.SupplierId))!.Status.Should().Be("ACTIVE");

        var before = await SnapshotB2Async();
        var active = await PostAsync(Url + $"/{B.Su2.SupplierId}/reactivate", token);
        active.Code.Should().BeOneOf(new[] { 400, 409 }, active.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_068_Discontinue_Already_Discontinued_Supplier_Is_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url + $"/{B.Su3.SupplierId}/discontinue", token, new { Reason = "Lại ngừng" });
        r.Code.Should().BeOneOf(new[] { 400, 409 }, r.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_069_List_Filters_By_Status()
    {
        var token = await TkAdm();
        var all = await GetAsync(Url + "?Status=&PageSize=100", token);
        var active = await GetAsync(Url + "?Status=Active&PageSize=100", token);
        var stopped = await GetAsync(Url + "?Status=Discontinued&PageSize=100", token);
        foreach (var r in new[] { all, active, stopped }) r.Code.Should().Be(200, r.ToString());

        await using var db = NewDb();
        Ids(all, "supplierId").Should().Contain(new[] { B.Su1.SupplierId, B.Su2.SupplierId, B.Su3.SupplierId })
            .And.BeEquivalentTo(await db.Suppliers.AsNoTracking().Select(s => s.SupplierId).ToListAsync());
        var activeIds = Ids(active, "supplierId");
        activeIds.Should().Contain(new[] { B.Su1.SupplierId, B.Su2.SupplierId }).And.NotContain(B.Su3.SupplierId);
        activeIds.Should().BeEquivalentTo(await db.Suppliers.AsNoTracking().Where(s => s.Status == "ACTIVE").Select(s => s.SupplierId).ToListAsync());
        var stoppedIds = Ids(stopped, "supplierId");
        stoppedIds.Should().Contain(B.Su3.SupplierId).And.NotContain(new[] { B.Su1.SupplierId, B.Su2.SupplierId });
        stoppedIds.Should().BeEquivalentTo(await db.Suppliers.AsNoTracking().Where(s => s.Status == "DISCONTINUED").Select(s => s.SupplierId).ToListAsync());
    }
}
