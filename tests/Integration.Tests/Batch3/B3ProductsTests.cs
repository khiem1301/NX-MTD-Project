using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_016, 018–024 — Product (A1–A16 đại diện). 017 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3ProductsTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Get = HttpMethod.Get, Post = HttpMethod.Post;

    [Fact]
    public async Task IT_B3_016_Create_Product_Stores_Active_With_No_Children()
    {
        var before = await Snap();
        var r = await Adm(Post, A + "products", new { ProductCode = "SP300", Description = "Quần tây" });

        Created(r);
        await using var db = NewDb();
        var p = await db.Products.AsNoTracking().SingleAsync(x => x.ProductCode == "SP300");
        p.Status.Should().Be("ACTIVE");
        AssertCreatedNow(p.CreatedAt);
        (await db.ProductColors.AsNoTracking().CountAsync(x => x.ProductId == p.ProductId)).Should().Be(0);
        (await db.ProductVariants.AsNoTracking().CountAsync(x => x.ProductId == p.ProductId)).Should().Be(0);
        (await db.Operations.AsNoTracking().CountAsync(x => x.ProductId == p.ProductId)).Should().Be(0);
        (await Snap()).Should().NotBe(before);
    }

    [Theory]
    [InlineData("IT_B3_018", "len30", true)]
    [InlineData("IT_B3_018", "len31", false)]
    [InlineData("IT_B3_018", "unicode", false)]
    public async Task IT_B3_018_ProductCode_Length_And_Unicode(string caseId, string kind, bool ok)
    {
        var code = kind switch { "len30" => new string('A', 30), "len31" => new string('A', 31), _ => "SPĐẶC" };
        var r = await Adm(Post, A + "products", new { ProductCode = code, Description = (string?)null });

        r.ShouldNotBeServerError();
        await using var db = NewDb();
        var stored = await db.Products.AsNoTracking().CountAsync(x => x.ProductCode == code);
        if (ok) { Created(r); stored.Should().Be(1); }
        else
        {
            r.Code.Should().Be(400, r.ToString());
            stored.Should().Be(0);
            (await db.Products.AsNoTracking().AnyAsync(x => x.ProductCode.Contains("??"))).Should().BeFalse("Unicode không được lưu thành ?");
        }
    }

    [Fact]
    public async Task IT_B3_019_Six_Extra_Images_Rolls_Back_Product()
    {
        var before = await Snap();
        var extras = Enumerable.Range(1, 6).Select(i => $"it-b3/extra-{i}.jpg").ToList();

        var r = await Adm(Post, A + "products", ProductBody("SP302", null, null, extras));

        Failed(r, 400);
        await using var db = NewDb();
        (await db.Products.AsNoTracking().AnyAsync(x => x.ProductCode == "SP302")).Should().BeFalse();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_020_Get_Product_Returns_Colors_IsUsed_And_History()
    {
        var before = await Snap();
        var r = await Adm(Get, A + $"products/{B.Pr1.ProductId}");

        r.Code.Should().Be(200, r.ToString());
        var colors = B1Json.Prop(r.Data, "colors")!.Value.EnumerateArray().Select(x => x.GetString()).ToList();
        await using var db = NewDb();
        var expected = await db.ProductColors.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).Select(c => c.Color).ToListAsync();
        colors.Should().BeEquivalentTo(expected).And.BeEquivalentTo(["Đen", "Trắng"]);
        B1Json.Bool(r.Data, "isUsed").Should().BeTrue();
        B1Json.Prop(r.Data, "history")!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_021_Paging_Is_Clamped_Not_Rejected()
    {
        var before = await Snap();
        var big = await Acc(Get, AC + "products?Status=Active&PageSize=500");
        var neg = await Acc(Get, AC + "products?Page=-5");

        big.Code.Should().Be(200, big.ToString());
        big.Items().Count.Should().BeLessThanOrEqualTo(100);
        B1Json.Int(big.Data, "pageSize").Should().BeLessThanOrEqualTo(100);
        neg.Code.Should().Be(200, neg.ToString());
        B1Json.Int(neg.Data, "page").Should().Be(1);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_022_SortDir_Asc_Is_Case_Insensitive_Other_Is_Desc()
    {
        var asc = await Acc(Get, AC + "products?SortDir=ASC");
        var junk = await Acc(Get, AC + "products?SortDir=xyz");

        asc.Code.Should().Be(200, asc.ToString());
        junk.Code.Should().Be(200, junk.ToString());
        var a = asc.Items().Select(i => B1Json.Int(i, "productId")!.Value).ToList();
        var j = junk.Items().Select(i => B1Json.Int(i, "productId")!.Value).ToList();
        a.Should().BeInAscendingOrder();
        j.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task IT_B3_023_Sample_Image_Path_Traversal_Is_Rejected()
    {
        var before = await Snap();
        var (res, _) = await Send("accountant", Get, AC + "products/sample-image?key=../../appsettings.json");
        using (res)
        {
            var body = await res.Content.ReadAsStringAsync();
            ((int)res.StatusCode).Should().BeOneOf(400, 404);
            body.Should().NotContainAny("ConnectionStrings", "Jwt", "\"Key\"");
        }

        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_024_Discontinue_Product_Does_Not_Cascade()
    {
        string Children(string s) => s[s.IndexOf("{\"ops\"", StringComparison.Ordinal)..];
        var before = Children(await Snap());
        var markers = (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Select(m => (m.ProductMarkerId, m.Status)).ToList();

        var r = await Adm(Post, A + $"products/{B.Pr1.ProductId}/discontinue");

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 409);
        Children(await Snap()).Should().Be(before, "Operation / PieceRate không bị xóa hay đổi trạng thái theo");
        (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Select(m => (m.ProductMarkerId, m.Status)).Should().Equal(markers);
        await using var db = NewDb();
        var status = (await db.Products.AsNoTracking().SingleAsync(x => x.ProductId == B.Pr1.ProductId)).Status;
        status.Should().Be(r.Code == 200 ? "DISCONTINUED" : "ACTIVE");
    }
}
