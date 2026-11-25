using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

public sealed class B5SupplierTests(ITestOutputHelper output) : B5TestBase(output)
{
    private static object Sup(string name, string? phone = null, string? email = null) =>
        new { SupplierName = name, Phone = phone, Email = email, Address = "KCN Tân Tạo", MaterialTypes = "Vải", Note = (string?)null };

    [Fact]
    public async Task IT_B5_014_Manager_Supplier_Options_Are_Active_Only()
    {
        var before = await Snap5();

        var r = await Call("manager", Get, M + "suppliers");

        r.Code.Should().Be(200, r.ToString());
        var ids = IdsOf(r, "supplierId");
        ids.Should().Contain([B.Su1.SupplierId, B.Su2.SupplierId]).And.NotContain(B.Su3.SupplierId);
        r.Items().Should().OnlyContain(i => i.EnumerateObject().Count() == 2);
        await using var db = NewDb();
        var active = await db.Suppliers.AsNoTracking().Where(s => s.Status == RecordStatus.Active).Select(s => s.SupplierId).ToListAsync();
        ids.Should().BeEquivalentTo(active);
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_015_Admin_Creates_Supplier()
    {
        var name = "Dệt Việt Thắng " + Rnd(6);

        var r = await Call("admin", Post, SUP, Sup(name, "09" + Rnd(8), $"vt{Rnd(8)}@det.vn"));

        r.Code.Should().Be(201, r.ToString());
        var s = (await SupplierByNameAsync(name))!;
        s.Status.Should().Be(RecordStatus.Active);
        s.CreatedAt.Year.Should().BeGreaterThan(2000);
    }

    [Fact]
    public async Task IT_B5_017_Blank_Phone_And_Email_Do_Not_Break_Unique_Filtered_Index()
    {
        var n1 = "NCC-A " + Rnd(6); var n2 = "NCC-B " + Rnd(6);

        var a = await Call("admin", Post, SUP, Sup(n1, "", ""));
        var b = await Call("admin", Post, SUP, Sup(n2, "", ""));

        a.Code.Should().Be(201, a.ToString());
        b.Code.Should().BeOneOf(201, 409);
        b.ShouldNotBeServerError();
        await using var db = NewDb();
        (await db.Suppliers.AsNoTracking().AnyAsync(s => s.Phone == "" || s.Email == "")).Should().BeFalse();
    }

    [Theory]
    [InlineData("IT_B5_018", "phone15", 201)]
    [InlineData("IT_B5_018", "phone16", 400)]
    [InlineData("IT_B5_018", "phoneUnicode", 400)]
    [InlineData("IT_B5_018", "name200", 201)]
    [InlineData("IT_B5_018", "name201", 400)]
    public async Task IT_B5_018_Supplier_Field_Length_Boundaries(string caseId, string kind, int expected)
    {
        var name = kind.StartsWith("name") ? Fill(kind == "name200" ? 200 : 201) : "NCC " + Rnd(6);
        var phone = kind switch { "phone15" => "0" + Rnd(14), "phone16" => "0" + Rnd(15), "phoneUnicode" => "09đ12" + Rnd(5), _ => "09" + Rnd(8) };
        var before = await Snap5();

        var r = await Call("admin", Post, SUP, Sup(name, phone, $"s{Rnd(8)}@x.vn"));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        await using var db = NewDb();
        (await db.Suppliers.AsNoTracking().AnyAsync(s => s.Phone!.Contains("?"))).Should().BeFalse();
        if (expected == 201) (await db.Suppliers.AsNoTracking().AnyAsync(s => s.SupplierName == name)).Should().BeTrue();
        else await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_019_Discontinue_Keeps_Purchase_Orders_Linked()
    {
        var r = await Call("admin", Post, $"{SUP}/{B.Su1.SupplierId}/discontinue", new { Reason = "Giá cao" });

        r.Code.Should().BeOneOf(200, 409);
        r.ShouldNotBeServerError();
        if (r.Code == 200)
        {
            var s = await SupplierAsync(B.Su1.SupplierId);
            s.Status.Should().NotBe(RecordStatus.Active);
            s.DiscontinueReason.Should().Be("Giá cao");
        }

        (await PoAsync(P.Po1.PurchaseOrderId)).SupplierId.Should().Be(B.Su1.SupplierId);
        (await PoAsync(P.Po3.PurchaseOrderId)).SupplierId.Should().Be(B.Su1.SupplierId);
    }

    [Theory]
    [InlineData("IT_B5_020", "none")]
    [InlineData("IT_B5_020", "empty")]
    [InlineData("IT_B5_020", "null")]
    public async Task IT_B5_020_Discontinue_Body_Variants_Never_500(string caseId, string kind)
    {
        object? body = kind switch { "empty" => "{}", "null" => new { Reason = (string?)null }, _ => null };

        var r = await Call("admin", Post, $"{SUP}/{B.Su2.SupplierId}/discontinue", body);

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400, 415);
        Note($"discontinue {kind} -> {r.Code}");
        if (r.Code == 200) (await SupplierAsync(B.Su2.SupplierId)).DiscontinueReason.Should().BeNull();
    }

    [Theory]
    [InlineData("IT_B5_021", 500, 200)]
    [InlineData("IT_B5_021", 501, 400)]
    public async Task IT_B5_021_Discontinue_Reason_Length(string caseId, int len, int expected)
    {
        var reason = new string('ạ', len);
        var before = await Snap5();

        var r = await Call("admin", Post, $"{SUP}/{B.Su2.SupplierId}/discontinue", new { Reason = reason });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        if (expected == 200) (await SupplierAsync(B.Su2.SupplierId)).DiscontinueReason.Should().Be(reason);
        else await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_022_Reactivate_And_Detail()
    {
        var ok = await Call("admin", Post, $"{SUP}/{B.Su3.SupplierId}/reactivate");
        var already = await Call("admin", Post, $"{SUP}/{B.Su2.SupplierId}/reactivate");
        var detail = await Call("admin", Get, $"{SUP}/{B.Su1.SupplierId}");

        ok.Code.Should().Be(200, ok.ToString());
        already.Code.Should().BeOneOf(400, 409);
        detail.Code.Should().Be(200, detail.ToString());
        (await SupplierAsync(B.Su3.SupplierId)).Status.Should().Be(RecordStatus.Active);
        B1Json.Prop(detail.Data, "history")!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        await using var db = NewDb();
        var count = await db.PurchaseOrders.AsNoTracking().CountAsync(p => p.SupplierId == B.Su1.SupplierId);
        B1Json.Int(B1Json.Prop(detail.Data, "supplier")!.Value, "purchaseOrderCount").Should().Be(count);
    }
}
