using FluentAssertions;
using Integration.Tests.Batch1;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_116–131 — AC/operation-groups. 121 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3OperationGroupsTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Get = HttpMethod.Get, Post = HttpMethod.Post, Put = HttpMethod.Put, Del = HttpMethod.Delete;

    private int Pr1 => B.Pr1.ProductId;
    private static string G(int? id = null) => AC + "operation-groups" + (id is null ? "" : $"/{id}");
    private Task<Api> Create(int productId, string name) => Acc(Post, G(), new { ProductId = productId, GroupName = name });
    private Task<Api> SetGroup(int productId, int[] ops, int? group) => Acc(Put, G() + "/operations", new { ProductId = productId, OperationIds = ops, OperationGroupId = group });

    private async Task<int?> GroupOf(int opId) => (await OpAsync(opId))!.OperationGroupId;

    [Fact]
    public async Task IT_B3_116_List_Counts_All_Operations_Including_Discontinued()
    {
        var before = await Snap();
        var acc = await Acc(Get, G() + $"?productId={Pr1}");
        var admin = await Adm(Get, A + $"operation-groups?productId={Pr1}");

        foreach (var r in new[] { acc, admin })
        {
            r.Code.Should().Be(200, r.ToString());
            var counts = r.Items().ToDictionary(g => B1Json.Int(g, "operationGroupId")!.Value, g => B1Json.Int(g, "operationCount")!.Value);
            counts[P.Og1.OperationGroupId].Should().Be(2);
            counts[P.Og2.OperationGroupId].Should().Be(0);
            counts.Keys.Should().NotContain(P.Og9.OperationGroupId);
        }

        await using var db = NewDb();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Operations, o => o.OperationGroupId == P.Og1.OperationGroupId)).Should().Be(2);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_117_List_Without_Or_With_Unknown_ProductId()
    {
        var before = await Snap();
        var none = await Acc(Get, G());
        var missing = await Acc(Get, G() + $"?productId={await MissingIdAsync()}");

        none.ShouldNotBeServerError();
        missing.ShouldNotBeServerError();
        none.Code.Should().BeOneOf(200, 400);
        missing.Code.Should().BeOneOf(200, 404);
        if (none.Code == 200) none.Items().Should().BeEmpty();
        if (missing.Code == 200) missing.Items().Should().BeEmpty();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_118_Create_Group()
    {
        var r = await Create(Pr1, "Nhóm Đóng Gói");

        Created(r);
        (await GroupsOfAsync(Pr1, "Nhóm Đóng Gói")).Should().ContainSingle();
    }

    [Fact]
    public async Task IT_B3_119_Group_Name_Is_Unique_Per_Product()
    {
        var dup = await Create(Pr1, "Nhóm May");
        var other = await Create(B.Pr2.ProductId, "Nhóm May");

        Failed(dup, 409);
        Created(other);
        (await GroupsOfAsync(Pr1, "Nhóm May")).Should().ContainSingle();
        (await GroupsOfAsync(B.Pr2.ProductId, "Nhóm May")).Should().ContainSingle();
    }

    [Theory]
    [InlineData("IT_B3_120", "len200", 200)]
    [InlineData("IT_B3_120", "len201", 400)]
    [InlineData("IT_B3_120", "empty", 400)]
    [InlineData("IT_B3_120", "spaces", 400)]
    [InlineData("IT_B3_120", "noProduct", 404)]
    public async Task IT_B3_120_GroupName_Boundaries(string caseId, string kind, int expected)
    {
        var name = kind switch { "len200" => Viet(200), "len201" => Viet(201), "empty" => "", "spaces" => "   ", _ => "Nhóm X" };
        var productId = kind == "noProduct" ? await MissingIdAsync() : Pr1;

        var r = await Create(productId, name);

        r.ShouldNotBeServerError();
        if (expected == 200) Created(r); else Failed(r, expected);
        (await GroupsOfAsync(Pr1)).Count.Should().Be(expected == 200 ? 3 : 2);
    }

    [Fact]
    public async Task IT_B3_122_Rename_Group_And_Reject_Duplicate_Name()
    {
        var ok = await Acc(Put, G(P.Og2.OperationGroupId), new { GroupName = "Nhóm Phụ" });
        var dup = await Acc(Put, G(P.Og2.OperationGroupId), new { GroupName = "Nhóm May" });

        ok.Code.Should().Be(200, ok.ToString());
        Failed(dup, 409);
        (await GroupAsync(P.Og2.OperationGroupId))!.GroupName.Should().Be("Nhóm Phụ");
    }

    [Fact]
    public async Task IT_B3_123_Rename_To_Name_Used_By_Other_Product_Is_Allowed()
    {
        var r = await Acc(Put, G(P.Og9.OperationGroupId), new { GroupName = "Nhóm May" });

        r.Code.Should().Be(200, r.ToString());
        (await GroupAsync(P.Og9.OperationGroupId))!.GroupName.Should().Be("Nhóm May");
        (await GroupAsync(P.Og1.OperationGroupId))!.GroupName.Should().Be("Nhóm May");
    }

    [Fact]
    public async Task IT_B3_124_Delete_Empty_Group_Is_Hard_Delete()
    {
        var r = await Acc(Del, G(P.Og2.OperationGroupId));

        r.Code.Should().BeOneOf(200, 204);
        (await GroupAsync(P.Og2.OperationGroupId)).Should().BeNull();
    }

    [Fact]
    public async Task IT_B3_125_Delete_Group_With_Operations_Is_Blocked_Or_Detaches()
    {
        var r = await Acc(Del, G(P.Og1.OperationGroupId));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 204, 409);
        r.Body.Should().NotContainAny("FK_", "SqlException");
        if (r.Code == 409) (await GroupAsync(P.Og1.OperationGroupId)).Should().NotBeNull();
        else
        {
            (await GroupAsync(P.Og1.OperationGroupId)).Should().BeNull();
            (await GroupOf(P.Op1.OperationId)).Should().BeNull();
            (await GroupOf(P.Op5.OperationId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task IT_B3_126_Delete_Unknown_Group_Returns_404()
    {
        var before = await Snap();
        var missing = await Acc(Del, G(await MissingIdAsync()));
        var notNumber = await Acc(Del, AC + "operation-groups/abc");

        Failed(missing, 404);
        Failed(notNumber, 404);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_127_Assign_Operations_To_Group()
    {
        var r = await SetGroup(Pr1, [P.Op2.OperationId, P.Op3.OperationId], P.Og2.OperationGroupId);

        r.Code.Should().Be(200, r.ToString());
        (await GroupOf(P.Op2.OperationId)).Should().Be(P.Og2.OperationGroupId);
        (await GroupOf(P.Op3.OperationId)).Should().Be(P.Og2.OperationGroupId);
    }

    [Fact]
    public async Task IT_B3_128_Unassign_Operation_Decreases_Group_Count()
    {
        var r = await SetGroup(Pr1, [P.Op1.OperationId], null);

        r.Code.Should().Be(200, r.ToString());
        (await GroupOf(P.Op1.OperationId)).Should().BeNull();
        var list = await Acc(Get, G() + $"?productId={Pr1}");
        B1Json.Int(list.Items().Single(g => B1Json.Int(g, "operationGroupId") == P.Og1.OperationGroupId), "operationCount").Should().Be(1);
    }

    [Fact]
    public async Task IT_B3_129_Group_Of_Other_Product_Is_Rejected()
    {
        var before = await Snap();
        var r = await SetGroup(Pr1, [P.Op1.OperationId], P.Og9.OperationGroupId);

        Failed(r, 400, 404);
        (await GroupOf(P.Op1.OperationId)).Should().Be(P.Og1.OperationGroupId);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_130_Operation_Of_Other_Product_Rolls_Back_Whole_Request()
    {
        var before = await Snap();
        var r = await SetGroup(Pr1, [P.Op1.OperationId, P.Op9.OperationId], P.Og2.OperationGroupId);

        Failed(r, 400, 404);
        (await GroupOf(P.Op1.OperationId)).Should().Be(P.Og1.OperationGroupId);
        (await GroupOf(P.Op9.OperationId)).Should().BeNull();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_131_OperationIds_Edge_Cases()
    {
        var before = await Snap();
        var empty = await SetGroup(Pr1, [], P.Og2.OperationGroupId);
        var missing = await SetGroup(Pr1, [await MissingIdAsync()], P.Og2.OperationGroupId);
        empty.ShouldNotBeServerError();
        empty.Code.Should().BeOneOf(200, 400);
        Failed(missing, 400, 404);
        await AssertSame(before);

        var dup = await SetGroup(Pr1, [P.Op1.OperationId, P.Op1.OperationId], P.Og2.OperationGroupId);

        dup.Code.Should().Be(200, dup.ToString());
        (await GroupOf(P.Op1.OperationId)).Should().Be(P.Og2.OperationGroupId);
    }
}
