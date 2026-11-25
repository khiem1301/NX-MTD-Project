using FluentAssertions;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_001–015 — ma trận phân quyền chéo Admin ↔ Accountant.</summary>
public sealed class B3RbacTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Get = HttpMethod.Get, Post = HttpMethod.Post, Put = HttpMethod.Put, Del = HttpMethod.Delete;
    private static readonly int[] NoRoute = [404, 405];

    [Fact]
    public Task IT_B3_001_Anonymous_Gets_401() => DenyAsync("anonymous", [401],
        (Get, A + "products"), (Get, AC + "operations?productId={PR1}"), (Get, A + "piece-rates"), (Get, AC + "operation-groups?productId={PR1}"));

    [Fact]
    public async Task IT_B3_002_Expired_Or_Forged_Token_Cannot_Approve()
    {
        var before = await Snap();
        foreach (var token in new[] { await TkExpired(), await TkForged() })
        {
            var r = await PostAsync(A + R("piece-rates/{PP1}/approve"), token, new { Note = (string?)null });
            r.Code.Should().Be(401, r.ToString());
        }

        var rate = await RateAsync(P.PrP1.PieceRateId);
        rate.Status.Should().Be("PENDING_APPROVAL");
        rate.ApprovedAt.Should().BeNull();
        await AssertSame(before);
    }

    [Fact]
    public Task IT_B3_003_Admin_On_Accountant_Prefix_Gets_403() => DenyAsync("admin", [403],
        (Get, AC + "operations?productId={PR1}"), (Get, AC + "piece-rates"), (Get, AC + "products"));

    [Fact]
    public async Task IT_B3_004_Admin_Cannot_Write_Accountant_Operations_Or_Rates()
    {
        await DenyAsync("admin", [403],
            (Post, AC + "operations"), (Post, AC + "piece-rates"), (Post, AC + "piece-rates/{PD1}/submit"));
        (await RateAsync(P.PrD1.PieceRateId)).Status.Should().Be("DRAFT");
    }

    [Fact]
    public Task IT_B3_005_Admin_Has_No_Operation_Write_Routes() => DenyAsync("admin", NoRoute,
        (Post, A + "operations"), (Put, A + "operations/{OP1}"), (Post, A + "operations/{OP1}/discontinue"),
        (Post, A + "operations/import-data?productId={PR1}"));

    [Fact]
    public Task IT_B3_006_Admin_Has_No_OperationGroup_Write_Routes() => DenyAsync("admin", NoRoute,
        (Post, A + "operation-groups"), (Put, A + "operation-groups/{OG1}"), (Del, A + "operation-groups/{OG2}"), (Put, A + "operation-groups/operations"));

    [Fact]
    public Task IT_B3_007_Admin_Has_No_PieceRate_Create_Or_Submit_Routes() => DenyAsync("admin", NoRoute,
        (Post, A + "piece-rates"), (Put, A + "piece-rates/{PD1}"), (Post, A + "piece-rates/submit-many"));

    [Fact]
    public async Task IT_B3_008_Accountant_Cannot_Approve_Or_Reject_On_Admin_Prefix()
    {
        var before = await Snap();
        await DenyAsync("accountant", [403],
            (Post, A + "piece-rates/{PP1}/approve"), (Post, A + "piece-rates/approve-many"), (Post, A + "piece-rates/{PP1}/reject"));
        var rate = await RateAsync(P.PrP1.PieceRateId);
        rate.Status.Should().Be("PENDING_APPROVAL");
        rate.ApprovedAt.Should().BeNull();
        rate.DecisionNote.Should().BeNull();
        await AssertSame(before);
    }

    [Fact]
    public Task IT_B3_009_Accountant_Prefix_Has_No_Approve_Or_Reject_Route() => DenyAsync("accountant", NoRoute,
        (Post, AC + "piece-rates/{PP1}/approve"), (Post, AC + "piece-rates/{PP1}/reject"));

    [Fact]
    public Task IT_B3_010_Accountant_Cannot_Use_Admin_Products() => DenyAsync("accountant", [403],
        (Get, A + "products"), (Put, A + "products/{PR1}/spec"), (Post, A + "products/{PR1}/import-data"));

    [Fact]
    public Task IT_B3_011_Accountant_Products_Only_Has_List_Spec_Image() => DenyAsync("accountant", NoRoute,
        (Get, AC + "products/{PR1}"), (Post, AC + "products"), (Put, AC + "products/{PR1}/spec"), (Post, AC + "products/{PR1}/discontinue"));

    [Theory]
    [InlineData("IT_B3_012", "manager")]
    [InlineData("IT_B3_012", "warehouse")]
    [InlineData("IT_B3_012", "teamleader")]
    [InlineData("IT_B3_012", "worker")]
    public Task IT_B3_012_Other_Roles_Are_Forbidden_Everywhere(string caseId, string who) => DenyAsync(who, [403],
        (Get, A + "products"), (Get, AC + "operations?productId={PR1}"), (Get, A + "piece-rates"), (Post, AC + "piece-rates"));

    [Fact]
    public Task IT_B3_013_Manager_Cannot_Import_Spec() => DenyAsync("manager", [403], (Post, A + "products/{PR1}/import-data"));

    [Fact]
    public async Task IT_B3_014_Admin_Can_View_Operations_And_Groups()
    {
        var before = await Snap();
        foreach (var url in new[] { A + "operations?productId={PR1}", A + "operation-groups?productId={PR1}" })
        {
            var r = await Adm(Get, R(url));
            r.Code.Should().Be(200, r.ToString());
        }

        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_015", "/api/v1/ADMIN/PieceRates", false)]
    [InlineData("IT_B3_015", "/api/v1/admin/piece-rates", true)]
    public async Task IT_B3_015_Route_Casing_And_Kebab(string caseId, string url, bool kebab)
    {
        var before = await Snap();
        var r = await Adm(Get, url);
        r.ShouldNotBeServerError();
        if (kebab) { r.Code.Should().BeOneOf(200, 404); Note($"kebab piece-rates → {r.Code}"); }
        else r.Code.Should().Be(200, r.ToString());
        await AssertSame(before);
    }
}
