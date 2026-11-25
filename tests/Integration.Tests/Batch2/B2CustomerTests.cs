using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_020–052 — Customer (A/customers). Các ca đồng thời 032, 033, 039 nằm ở B2ConcurrencyTests.</summary>
public sealed class B2CustomerTests(ITestOutputHelper output) : B2TestBase(output)
{
    private string Url => A + "customers";

    private async Task<Customer?> FindAsync(string name)
    {
        await using var db = NewDb();
        return await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerName == name);
    }

    private async Task<int> CountWhereAsync(Func<IQueryable<Customer>, IQueryable<Customer>> filter)
    {
        await using var db = NewDb();
        return await filter(db.Customers.AsNoTracking()).CountAsync();
    }

    [Fact]
    public async Task IT_B2_020_Create_Customer_Succeeds_And_Is_Audited()
    {
        var token = await TkAdm();
        var lastAudit = await LastAuditIdAsync();
        var r = await PostAsync(Url, token, CustomerBody("May Sài Gòn", "0903000001", "sg@may.vn", "12 Lê Lợi, Q1"));
        ShouldBeCreated(r);
        r.Data.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        B1Json.Int(r.Data, "customerId").Should().BeGreaterThan(0);
        Note($"Status trong response = '{B1Json.Str(r.Data, "status")}'");

        var row = await FindAsync("May Sài Gòn");
        row.Should().NotBeNull();
        row!.Status.Should().Be("ACTIVE");
        row.DiscontinueReason.Should().BeNull();
        row.Phone.Should().Be("0903000001");
        row.Email.Should().Be("sg@may.vn");
        row.DefaultAddress.Should().Be("12 Lê Lợi, Q1");
        B1Json.Int(r.Data, "customerId").Should().Be(row.CustomerId);
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_021_Duplicate_Name_Returns_409_Without_Leaking_Constraint()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url, token, CustomerBody("Công ty May Việt", "0903000002", "dup@may.vn"));
        r.Code.Should().Be(409, r.ToString());
        AssertNoSqlLeak(r);
        r.Body.Should().NotContain("UQ_Customer_Name");
        (await CountCustomersAsync("Công ty May Việt")).Should().Be(1);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_022_Name_Case_And_Trailing_Space_Is_Handled_Without_500()
    {
        var token = await TkAdm();
        var before = await CountAllAsync("Customer");
        var upper = await PostAsync(Url, token, CustomerBody("CÔNG TY MAY VIỆT"));
        var spaced = await PostAsync(Url, token, CustomerBody("Công ty May Việt "));
        var created = 0;
        foreach (var r in new[] { upper, spaced })
        {
            AssertNoSqlLeak(r);
            r.Code.Should().BeOneOf(new[] { 200, 201, 409 }, r.ToString());
            if (r.Code != 409) created++;
        }

        Note($"HOA → {upper.Code}; thừa khoảng trắng → {spaced.Code}");
        (await CountAllAsync("Customer")).Should().Be(before + created, "DB nhất quán với API");
    }

    [Fact]
    public async Task IT_B2_023_Duplicate_Phone_Returns_409()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url, token, CustomerBody("Khách trùng SĐT", "0901000001"));
        r.Code.Should().Be(409, r.ToString());
        AssertNoSqlLeak(r);
        (await FindAsync("Khách trùng SĐT")).Should().BeNull();
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_024_Duplicate_Email_Returns_409_Including_Case_Variant()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var exact = await PostAsync(Url, token, CustomerBody("Khách trùng mail 1", email: "cu1@mayviet.vn"));
        exact.Code.Should().Be(409, exact.ToString());
        AssertNoSqlLeak(exact);
        await AssertB2UnchangedAsync(before);

        var upper = await PostAsync(Url, token, CustomerBody("Khách trùng mail 2", email: "CU1@MAYVIET.VN"));
        AssertNoSqlLeak(upper);
        upper.Code.Should().BeOneOf(new[] { 200, 201, 409 }, upper.ToString());
        Note($"Email khác hoa/thường → {upper.Code}");
        (await FindAsync("Khách trùng mail 2") is null).Should().Be(upper.Code == 409);
    }

    [Fact]
    public async Task IT_B2_025_Multiple_Customers_With_Null_Phone_And_Email_Are_Allowed()
    {
        var token = await TkAdm();
        var nullPhones = await CountWhereAsync(q => q.Where(c => c.Phone == null));
        var nullEmails = await CountWhereAsync(q => q.Where(c => c.Email == null));
        ShouldBeCreated(await PostAsync(Url, token, CustomerBody("KH-N1", null, null)));
        ShouldBeCreated(await PostAsync(Url, token, CustomerBody("KH-N2", null, null)));
        (await CountWhereAsync(q => q.Where(c => c.Phone == null))).Should().Be(nullPhones + 2);
        (await CountWhereAsync(q => q.Where(c => c.Email == null))).Should().Be(nullEmails + 2);
    }

    [Fact]
    public async Task IT_B2_026_Empty_Phone_And_Email_Are_Normalized_To_Null_Or_Rejected()
    {
        var token = await TkAdm();
        var first = await PostAsync(Url, token, CustomerBody("KH-E1", "", ""));
        var second = await PostAsync(Url, token, CustomerBody("KH-E2", "", ""));
        AssertNoSqlLeak(first);
        AssertNoSqlLeak(second);
        first.Code.Should().BeOneOf(new[] { 200, 201 }, first.ToString());
        second.Code.Should().BeOneOf(new[] { 200, 201, 409 }, second.ToString());

        foreach (var (name, r) in new[] { ("KH-E1", first), ("KH-E2", second) })
        {
            var row = await FindAsync(name);
            if (r.Code == 409) { row.Should().BeNull(); continue; }
            row.Should().NotBeNull();
            row!.Phone.Should().BeNull("'' phải được chuẩn hóa thành NULL để không vi phạm filtered unique");
            row.Email.Should().BeNull();
        }
    }

    [Fact]
    public async Task IT_B2_027_Name_Length_Boundary_200_And_201()
    {
        var token = await TkAdm();
        var ok = Viet(200);
        var tooLong = Viet(201);
        var before = await CountAllAsync("Customer");
        ShouldBeCreated(await PostAsync(Url, token, CustomerBody(ok)));
        var over = await PostAsync(Url, token, CustomerBody(tooLong));
        over.Code.Should().Be(400, over.ToString());
        (await FindAsync(ok))!.CustomerName.Length.Should().Be(200);
        (await FindAsync(tooLong)).Should().BeNull();
        (await CountAllAsync("Customer")).Should().Be(before + 1);
    }

    [Fact]
    public async Task IT_B2_028_Phone_Email_Address_Length_Boundaries()
    {
        var token = await TkAdm();
        var before = await CountAllAsync("Customer");

        ShouldBeCreated(await PostAsync(Url, token, CustomerBody("KH-P15", PhoneOf(15))));
        (await PostAsync(Url, token, CustomerBody("KH-P16", PhoneOf(16)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, CustomerBody("KH-M100", null, EmailOf(100))));
        (await PostAsync(Url, token, CustomerBody("KH-M101", null, EmailOf(101)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, CustomerBody("KH-A500", null, null, Viet(500))));
        (await PostAsync(Url, token, CustomerBody("KH-A501", null, null, Viet(501)))).Code.Should().Be(400);

        (await FindAsync("KH-P15"))!.Phone!.Length.Should().Be(15);
        (await FindAsync("KH-M100"))!.Email!.Length.Should().Be(100);
        (await FindAsync("KH-A500"))!.DefaultAddress!.Length.Should().Be(500);
        foreach (var rejected in new[] { "KH-P16", "KH-M101", "KH-A501" }) (await FindAsync(rejected)).Should().BeNull(rejected);
        (await CountAllAsync("Customer")).Should().Be(before + 3);
    }

    [Fact]
    public async Task IT_B2_029_Missing_Name_Returns_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        foreach (var body in new object[] { CustomerBody(null!), CustomerBody(""), CustomerBody("   "), "{}" })
        {
            var r = await PostAsync(Url, token, body);
            r.Code.Should().Be(400, r.ToString());
            r.ShouldNotBeServerError();
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_030_Invalid_Email_And_Phone_Return_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var bodies = new[]
        {
            CustomerBody("KH-bad-email", null, "abc"),
            CustomerBody("KH-bad-phone", "09ab-12"),
            CustomerBody("KH-bad-phone-vn", "09đ"),
        };
        foreach (var body in bodies)
        {
            var r = await PostAsync(Url, token, body);
            r.Code.Should().Be(400, r.ToString());
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_031_Mass_Assignment_Fields_Are_Ignored()
    {
        var token = await TkAdm();
        var body = new
        {
            CustomerId = 999, Status = "DISCONTINUED", CreatedAt = "0001-01-01", DiscontinueReason = "x",
            CustomerName = "KH-mass", Phone = AutoPhone(), Email = AutoEmail(), DefaultAddress = "Địa chỉ IT B2",
        };
        ShouldBeCreated(await PostAsync(Url, token, body));
        var row = await FindAsync("KH-mass");
        row.Should().NotBeNull();
        row!.CustomerId.Should().NotBe(999);
        row.Status.Should().Be("ACTIVE");
        row.DiscontinueReason.Should().BeNull();
    }

    [Fact]
    public async Task IT_B2_034_Update_Customer_Succeeds_And_Is_Audited()
    {
        var token = await TkAdm();
        var lastAudit = await LastAuditIdAsync();
        var r = await PutAsync(Url + "/" + B.Cu2.CustomerId, token, CustomerBody("Garment ABC Ltd", "0904000001", null, "KCN Tân Bình"));
        r.Code.Should().Be(200, r.ToString());
        var row = (await CustomerAsync(B.Cu2.CustomerId))!;
        row.CustomerName.Should().Be("Garment ABC Ltd");
        row.Phone.Should().Be("0904000001");
        row.Email.Should().BeNull();
        row.DefaultAddress.Should().Be("KCN Tân Bình");
        row.Status.Should().Be("ACTIVE");
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_035_Update_To_Existing_Name_Or_Phone_Returns_409()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var byName = await PutAsync(Url + "/" + B.Cu2.CustomerId, token, CustomerBody("Công ty May Việt"));
        byName.Code.Should().Be(409, byName.ToString());
        AssertNoSqlLeak(byName);
        var byPhone = await PutAsync(Url + "/" + B.Cu2.CustomerId, token, CustomerBody("Garment ABC", "0901000001"));
        byPhone.Code.Should().Be(409, byPhone.ToString());
        AssertNoSqlLeak(byPhone);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_036_Update_With_Own_Unique_Values_Does_Not_Conflict()
    {
        var token = await TkAdm();
        var r = await PutAsync(Url + "/" + B.Cu1.CustomerId, token, CustomerBody("Công ty May Việt", "0901000001", "cu1@mayviet.vn", B.Cu1.DefaultAddress));
        r.Code.Should().Be(200, r.ToString());
        var row = (await CustomerAsync(B.Cu1.CustomerId))!;
        row.CustomerName.Should().Be("Công ty May Việt");
        row.Phone.Should().Be("0901000001");
        row.Email.Should().Be("cu1@mayviet.vn");
        row.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B2_037_Update_Missing_Customer_Returns_404_Without_Upsert()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var missing = await MissingIdAsync();
        foreach (var id in new[] { missing.ToString(), "abc", "0" })
        {
            var r = await PutAsync(Url + "/" + id, token, CustomerBody("KH-upsert"));
            r.Code.Should().Be(404, $"{id}: {r}");
        }

        (await FindAsync("KH-upsert")).Should().BeNull();
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_038_Update_Cannot_Change_Status_Via_Body()
    {
        var token = await TkAdm();
        var body = new { CustomerName = "Garment ABC", Phone = AutoPhone(), Email = AutoEmail(), DefaultAddress = "Địa chỉ IT B2", Status = "DISCONTINUED" };
        var r = await PutAsync(Url + "/" + B.Cu2.CustomerId, token, body);
        r.Code.Should().Be(200, r.ToString());
        (await CustomerAsync(B.Cu2.CustomerId))!.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B2_040_Discontinue_Customer_Stores_Reason_And_Is_Audited()
    {
        var token = await TkAdm();
        var lastAudit = await LastAuditIdAsync();
        var r = await PostAsync(Url + $"/{B.Cu2.CustomerId}/discontinue", token, new { Reason = "Ngừng đặt hàng" });
        r.Code.Should().Be(200, r.ToString());
        var row = (await CustomerAsync(B.Cu2.CustomerId))!;
        row.Status.Should().Be("DISCONTINUED");
        row.DiscontinueReason.Should().Be("Ngừng đặt hàng");
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_041_Discontinue_Without_Reason_Returns_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var url = Url + $"/{B.Cu2.CustomerId}/discontinue";
        foreach (var body in new object[] { new { Reason = (string?)null }, new { Reason = "" }, "{}" })
        {
            var r = await PostAsync(url, token, body);
            r.Code.Should().Be(400, r.ToString());
        }

        var noBody = await PostAsync(url, token);
        noBody.Code.Should().BeOneOf(new[] { 400, 415 }, noBody.ToString());
        noBody.ShouldNotBeServerError();
        (await CustomerAsync(B.Cu2.CustomerId))!.Status.Should().Be("ACTIVE");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_042_Discontinue_Reason_Length_Boundary_500_And_501()
    {
        var token = await TkAdm();
        var reason = Viet(500);
        var ok = await PostAsync(Url + $"/{B.Cu2.CustomerId}/discontinue", token, new { Reason = reason });
        ok.Code.Should().Be(200, ok.ToString());
        (await CustomerAsync(B.Cu2.CustomerId))!.DiscontinueReason.Should().Be(reason);

        var tooLong = await PostAsync(Url + $"/{B.Cu1.CustomerId}/discontinue", token, new { Reason = Viet(501) });
        tooLong.Code.Should().Be(400, tooLong.ToString());
        var cu1 = (await CustomerAsync(B.Cu1.CustomerId))!;
        cu1.Status.Should().Be("ACTIVE");
        cu1.DiscontinueReason.Should().BeNull();
    }

    [Fact]
    public async Task IT_B2_043_Discontinue_Customer_With_Orders_Keeps_Orders_Intact()
    {
        var token = await TkAdm();
        var r = await PostAsync(Url + $"/{B.Cu1.CustomerId}/discontinue", token, new { Reason = "Tạm dừng" });
        r.Code.Should().BeOneOf(new[] { 200, 409 }, r.ToString());
        Note($"Discontinue khách có đơn → {r.Code}");
        await using var db = NewDb();
        var order = await db.CustomerOrders.AsNoTracking().SingleAsync(o => o.OrderId == B.Od1.OrderId);
        order.CustomerId.Should().Be(B.Cu1.CustomerId, "đơn không bị cascade / SET NULL");
        order.Status.Should().Be(B.Od1.Status);
        (await CustomerAsync(B.Cu1.CustomerId))!.Status.Should().Be(r.Code == 200 ? "DISCONTINUED" : "ACTIVE");
    }

    [Fact]
    public async Task IT_B2_044_Discontinue_Already_Discontinued_Customer_Is_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url + $"/{B.Cu3.CustomerId}/discontinue", token, new { Reason = "Lý do mới" });
        r.Code.Should().BeOneOf(new[] { 400, 409 }, r.ToString());
        (await CustomerAsync(B.Cu3.CustomerId))!.DiscontinueReason.Should().Be("Hết hợp tác");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_045_Reactivate_Customer_Restores_Active_And_Is_Audited()
    {
        var token = await TkAdm();
        var lastAudit = await LastAuditIdAsync();
        var r = await PostAsync(Url + $"/{B.Cu3.CustomerId}/reactivate", token);
        r.Code.Should().Be(200, r.ToString());
        var row = (await CustomerAsync(B.Cu3.CustomerId))!;
        row.Status.Should().Be("ACTIVE");
        Note($"DiscontinueReason sau reactivate = '{row.DiscontinueReason ?? "NULL"}'");
        row.DiscontinueReason.Should().BeOneOf(new string?[] { null, "Hết hợp tác" });
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_046_Reactivate_Active_Or_Missing_Customer_Is_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var active = await PostAsync(Url + $"/{B.Cu1.CustomerId}/reactivate", token);
        active.Code.Should().BeOneOf(new[] { 400, 409 }, active.ToString());
        var missing = await PostAsync(Url + $"/{await MissingIdAsync()}/reactivate", token);
        missing.Code.Should().Be(404, missing.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_047_List_Filters_By_Status()
    {
        var token = await TkAdm();
        var active = await GetAsync(Url + "?Status=Active&PageSize=100", token);
        active.Code.Should().Be(200, active.ToString());
        var discontinued = await GetAsync(Url + "?Status=Discontinued&PageSize=100", token);
        discontinued.Code.Should().Be(200, discontinued.ToString());
        var all = await GetAsync(Url + "?PageSize=100", token);
        all.Code.Should().Be(200, all.ToString());

        await using var db = NewDb();
        var dbActive = await db.Customers.AsNoTracking().Where(c => c.Status == "ACTIVE").Select(c => c.CustomerId).ToListAsync();
        var dbStopped = await db.Customers.AsNoTracking().Where(c => c.Status == "DISCONTINUED").Select(c => c.CustomerId).ToListAsync();
        var dbAll = await db.Customers.AsNoTracking().Select(c => c.CustomerId).ToListAsync();

        var activeIds = Ids(active, "customerId");
        activeIds.Should().Contain(new[] { B.Cu1.CustomerId, B.Cu2.CustomerId }).And.NotContain(B.Cu3.CustomerId);
        activeIds.Should().BeEquivalentTo(dbActive, "khớp WHERE Status='ACTIVE'");
        var stoppedIds = Ids(discontinued, "customerId");
        stoppedIds.Should().Contain(B.Cu3.CustomerId).And.NotContain(new[] { B.Cu1.CustomerId, B.Cu2.CustomerId });
        stoppedIds.Should().BeEquivalentTo(dbStopped, "khớp giá trị ngừng (HOA)");
        Ids(all, "customerId").Should().BeEquivalentTo(dbAll);
        Note($"Status trong response: {Statuses(activeIds.Count > 0 ? active.Items().Select(i => B1Json.Str(i, "status")) : [])}");
    }

    [Fact]
    public async Task IT_B2_048_Unknown_Status_Filter_Is_Handled_Without_500()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var upper = await GetAsync(Url + "?Status=ACTIVE&PageSize=100", token);
        upper.ShouldNotBeServerError();
        upper.Code.Should().BeOneOf(new[] { 200, 400 }, upper.ToString());
        Note($"Status=ACTIVE → {upper.Code}");
        var unknown = await GetAsync(Url + "?Status=Xyz&PageSize=100", token);
        unknown.ShouldNotBeServerError();
        if (unknown.Code == 200) unknown.Items().Should().BeEmpty("giá trị lạ không được trả về toàn bộ danh sách");
        else unknown.Code.Should().Be(400);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_049_Paging_Boundaries()
    {
        var token = await TkAdm();
        (await GetAsync(Url + "?Page=0", token)).Code.Should().Be(400);
        (await GetAsync(Url + "?Page=1", token)).Code.Should().Be(200);
        (await GetAsync(Url + "?PageSize=0", token)).Code.Should().Be(400);
        var one = await GetAsync(Url + "?PageSize=1", token);
        one.Code.Should().Be(200);
        one.Items().Count.Should().BeLessThanOrEqualTo(1);
        var hundred = await GetAsync(Url + "?PageSize=100", token);
        hundred.Code.Should().Be(200);
        hundred.Items().Count.Should().BeLessThanOrEqualTo(100);
        (await GetAsync(Url + "?PageSize=101", token)).Code.Should().Be(400);
    }

    [Fact]
    public async Task IT_B2_050_Keyword_Boundary_And_Injection()
    {
        var token = await TkAdm();
        var total = await CountAllAsync("Customer");
        (await GetAsync(Url + "?Keyword=" + new string('a', 100), token)).Code.Should().Be(200);
        (await GetAsync(Url + "?Keyword=" + new string('a', 101), token)).Code.Should().Be(400);
        var injection = await GetAsync(Url + "?PageSize=100&Keyword=" + Uri.EscapeDataString("' OR 1=1 --"), token);
        injection.Code.Should().Be(200, injection.ToString());
        injection.Items().Should().BeEmpty("không được trả toàn bộ danh sách khi Keyword là chuỗi tấn công");
        var percent = await GetAsync(Url + "?PageSize=100&Keyword=" + Uri.EscapeDataString("%"), token);
        percent.ShouldNotBeServerError();
        Note($"Keyword='%' → {percent.Code}, {percent.Items().Count} dòng");
        (await CountAllAsync("Customer")).Should().Be(total);
    }

    [Fact]
    public async Task IT_B2_051_Sorting_Direction_And_Unknown_Sort_Column()
    {
        var token = await TkAdm();
        var total = await CountAllAsync("Customer");
        await using var db = NewDb();
        var asc = await db.Customers.AsNoTracking().OrderBy(c => c.CustomerName).Select(c => c.CustomerId).ToListAsync();

        var up = await GetAsync(Url + "?PageSize=100&SortBy=CustomerName&SortDir=asc", token);
        up.Code.Should().Be(200, up.ToString());
        up.Items().Select(i => B1Json.Int(i, "customerId")!.Value).ToList().Should().Equal(asc, "asc = tăng dần theo tên");
        var down = await GetAsync(Url + "?PageSize=100&SortBy=CustomerName&SortDir=xyz", token);
        down.Code.Should().Be(200, down.ToString());
        down.Items().Select(i => B1Json.Int(i, "customerId")!.Value).ToList().Should().Equal(Enumerable.Reverse(asc).ToList(), "SortDir lạ = giảm dần");

        foreach (var sortBy in new[] { "NoSuchColumn", "CustomerName; DROP TABLE Customer" })
        {
            var r = await GetAsync(Url + "?PageSize=100&SortBy=" + Uri.EscapeDataString(sortBy), token);
            r.ShouldNotBeServerError();
            r.Code.Should().BeOneOf(new[] { 200, 400 }, r.ToString());
        }

        (await CountAllAsync("Customer")).Should().Be(total, "bảng Customer còn nguyên");
    }

    [Fact]
    public async Task IT_B2_052_Options_Returns_Unpaged_List()
    {
        var token = await TkAdm();
        var r = await GetAsync(Url + "/options", token);
        r.Code.Should().Be(200, r.ToString());
        r.Data.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array, "options không phân trang");
        var ids = Ids(r, "customerId");
        ids.Should().Contain(new[] { B.Cu1.CustomerId, B.Cu2.CustomerId });
        Note($"Options có chứa khách đã ngừng (CU3): {ids.Contains(B.Cu3.CustomerId)}");
        await using var db = NewDb();
        ids.Count.Should().BeLessThanOrEqualTo(await db.Customers.AsNoTracking().CountAsync());
        foreach (var item in r.Items())
        {
            B1Json.Str(item, "customerName").Should().NotBeNullOrEmpty();
            B1Json.Prop(item, "phone").Should().BeNull("không có SĐT / email trong CustomerOptionDto");
        }
    }
}
