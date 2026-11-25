using FluentAssertions;
using Integration.Tests.Batch1;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_067–094 — AC/operations (CRUD, công đoạn cuối, FK ghép). 078, 088 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3OperationsTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Get = HttpMethod.Get, Post = HttpMethod.Post, Put = HttpMethod.Put;

    private int Pr1 => B.Pr1.ProductId;
    private Task<Api> Create(object body) => Acc(Post, AC + "operations", body);
    private string OpUrl(int id, string? tail = null) => AC + $"operations/{id}" + (tail is null ? "" : "/" + tail);

    [Fact]
    public async Task IT_B3_067_Create_Operation_Stores_Upper_Case_And_Component_Link()
    {
        var r = await Create(SaveOp(Pr1, "CD03", "May túi", 3, 2, 45.50m, "Sewing", false, null, [B.Pr1Pocket.ProductComponentId]));

        Created(r);
        var op = (await OpByCodeAsync(Pr1, "CD03"))!;
        (op.TeamType, op.Status, op.IsFinalStep, op.OperationGroupId).Should().Be(("SEWING", "ACTIVE", false, (int?)null));
        op.StandardTimeSec.Should().Be(45.50m);
        (await OpComponentsAsync(op.OperationId)).Should().Equal(B.Pr1Pocket.ProductComponentId);
        await using var db = NewDb();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.OperationComponents, c => c.OperationId == op.OperationId)).ProductId.Should().Be(Pr1);
    }

    [Fact]
    public async Task IT_B3_068_Many_Operations_Per_Product_Work()
    {
        Created(await Create(SaveOp(Pr1, "CD03")));

        var r = await Acc(Get, AC + $"operations?productId={Pr1}&includeDiscontinued=false");

        r.Code.Should().Be(200, r.ToString());
        var codes = r.Items().Select(i => B1Json.Str(i, "operationCode")).ToList();
        codes.Should().Contain(["CD01", "CD02", "CD99", "CD03"]).And.NotContain(["CD98", "CD05"]);
        (await CountOpsAsync(Pr1)).Should().Be(6);
    }

    [Fact]
    public async Task IT_B3_069_IncludeDiscontinued_Adds_Retired_Operations()
    {
        var before = await Snap();
        var r = await Acc(Get, AC + $"operations?productId={Pr1}&includeDiscontinued=true");

        r.Code.Should().Be(200, r.ToString());
        var byCode = r.Items().ToDictionary(i => B1Json.Str(i, "operationCode")!, i => B1Json.Str(i, "status")!);
        byCode.Keys.Should().Contain(["CD98", "CD05"]);
        byCode["CD98"].Should().NotBe(byCode["CD01"]);
        byCode["CD05"].Should().NotBe(byCode["CD01"]);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_070_TeamType_Cutting_And_Default_Sewing()
    {
        Created(await Create(SaveOp(Pr1, "CD70", team: "Cutting")));
        Created(await Create(SaveOp(Pr1, "CD71", team: null)));

        (await OpByCodeAsync(Pr1, "CD70"))!.TeamType.Should().Be("CUTTING");
        (await OpByCodeAsync(Pr1, "CD71"))!.TeamType.Should().Be("SEWING");
    }

    [Theory]
    [InlineData("IT_B3_071", "Office")]
    [InlineData("IT_B3_071", "SEWINGXXXXX")]
    public async Task IT_B3_071_Invalid_TeamType_Returns_400(string caseId, string team)
    {
        var before = await Snap();
        var r = await Create(SaveOp(Pr1, "CD72", team: team));

        Failed(r, 400);
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_072", "CD01")]
    [InlineData("IT_B3_072", "cd01")]
    public async Task IT_B3_072_Duplicate_Code_Returns_409(string caseId, string code)
    {
        var before = await Snap();
        var r = await Create(SaveOp(Pr1, code));

        r.ShouldNotBeServerError();
        if (code == "CD01") Failed(r, 409); else Note($"mã chữ thường → {r.Code}");
        if (r.Code >= 400) await AssertSame(before);
        (await CountOpsAsync(Pr1, "CD01")).Should().Be(1);
    }

    [Fact]
    public async Task IT_B3_073_Same_Code_Can_Exist_In_Two_Products()
    {
        var r1 = await Acc(Get, AC + $"operations?productId={Pr1}");
        var r2 = await Acc(Get, AC + $"operations?productId={B.Pr2.ProductId}");

        r1.Items().Select(i => B1Json.Int(i, "productId")).Should().OnlyContain(p => p == Pr1);
        r2.Items().Select(i => B1Json.Int(i, "productId")).Should().OnlyContain(p => p == B.Pr2.ProductId);
        (await CountOpsAsync(Pr1, "CD01")).Should().Be(1);
        (await CountOpsAsync(B.Pr2.ProductId, "CD01")).Should().Be(1);
    }

    [Fact]
    public async Task IT_B3_074_Second_Final_Step_Is_Rejected()
    {
        var before = await Snap();
        var r = await Create(SaveOp(Pr1, "CD100", final: true));

        Failed(r, 400, 409);
        (await FinalActiveCountAsync(Pr1)).Should().Be(1);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_075_Update_To_Final_Step_While_Another_Exists_Is_Rejected()
    {
        var r = await Acc(Put, OpUrl(P.Op2.OperationId), SaveOp(Pr1, "CD02", "Công đoạn CD02", 2, final: true));

        Failed(r, 400, 409);
        (await OpAsync(P.Op2.OperationId))!.IsFinalStep.Should().BeFalse();
    }

    [Fact]
    public async Task IT_B3_076_Reactivate_Final_Step_While_Another_Is_Active_Is_Rejected()
    {
        var r = await Acc(Post, OpUrl(P.Op4.OperationId, "reactivate"));

        Failed(r, 400, 409);
        (await OpAsync(P.Op4.OperationId))!.Status.Should().Be("DISCONTINUED");
        (await FinalActiveCountAsync(Pr1)).Should().Be(1);
    }

    [Fact]
    public async Task IT_B3_077_Swap_Final_Step_Keeps_Exactly_One_Active()
    {
        var off = await Acc(Post, OpUrl(P.Op3.OperationId, "discontinue"));
        var on = await Acc(Post, OpUrl(P.Op4.OperationId, "reactivate"));

        off.Code.Should().Be(200, off.ToString());
        on.Code.Should().Be(200, on.ToString());
        (await OpAsync(P.Op3.OperationId))!.Status.Should().Be("DISCONTINUED");
        (await OpAsync(P.Op4.OperationId))!.Status.Should().Be("ACTIVE");
        (await FinalActiveCountAsync(Pr1)).Should().Be(1);
    }

    [Fact]
    public async Task IT_B3_079_Component_Of_Other_Product_Rolls_Back_Create()
    {
        var before = await Snap();
        var r = await Create(SaveOp(Pr1, "CD79", comps: [B.Pr1Front.ProductComponentId, P.C9.ProductComponentId]));

        Failed(r, 400, 404);
        (await OpByCodeAsync(Pr1, "CD79")).Should().BeNull();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_080_ComponentIds_Edge_Cases()
    {
        var missing = await Create(SaveOp(Pr1, "CD80", comps: [await MissingIdAsync()]));
        var dup = await Create(SaveOp(Pr1, "CD81", comps: [B.Pr1Front.ProductComponentId, B.Pr1Front.ProductComponentId]));
        var empty = await Create(SaveOp(Pr1, "CD82", comps: []));

        Failed(missing, 400, 404);
        dup.ShouldNotBeServerError();
        dup.Code.Should().BeOneOf(200, 201, 400);
        if (dup.Code < 400) (await OpComponentsAsync((await OpByCodeAsync(Pr1, "CD81"))!.OperationId)).Should().ContainSingle();
        Created(empty);
        (await OpComponentsAsync((await OpByCodeAsync(Pr1, "CD82"))!.OperationId)).Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B3_081_New_GroupName_Creates_Group_For_Same_Product()
    {
        var r = await Create(SaveOp(Pr1, "CD83", group: "Nhóm Hoàn Thiện"));

        Created(r);
        var group = (await GroupsOfAsync(Pr1, "Nhóm Hoàn Thiện")).Single();
        (await OpByCodeAsync(Pr1, "CD83"))!.OperationGroupId.Should().Be(group.OperationGroupId);
        group.ProductId.Should().Be(Pr1);
    }

    [Theory]
    [InlineData("IT_B3_082", "Nhóm May")]
    [InlineData("IT_B3_082", "nhóm may")]
    public async Task IT_B3_082_Existing_GroupName_Is_Reused(string caseId, string name)
    {
        var r = await Create(SaveOp(Pr1, "CD84", group: name));

        Created(r);
        (await GroupsOfAsync(Pr1)).Count(g => string.Equals(g.GroupName, "Nhóm May", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        (await OpByCodeAsync(Pr1, "CD84"))!.OperationGroupId.Should().Be(P.Og1.OperationGroupId);
    }

    [Fact]
    public async Task IT_B3_083_Group_Name_Of_Other_Product_Creates_New_Group()
    {
        var r = await Create(SaveOp(Pr1, "CD85", group: "Nhóm B"));

        Created(r);
        var mine = (await GroupsOfAsync(Pr1, "Nhóm B")).Single();
        mine.OperationGroupId.Should().NotBe(P.Og9.OperationGroupId);
        (await OpByCodeAsync(Pr1, "CD85"))!.OperationGroupId.Should().Be(mine.OperationGroupId);
        (await GroupAsync(P.Og9.OperationGroupId))!.ProductId.Should().Be(B.Pr2.ProductId);
    }

    [Theory]
    [InlineData("IT_B3_084", "code", 30, true)]
    [InlineData("IT_B3_084", "code", 31, false)]
    [InlineData("IT_B3_084", "name", 200, true)]
    [InlineData("IT_B3_084", "name", 201, false)]
    [InlineData("IT_B3_084", "group", 200, true)]
    [InlineData("IT_B3_084", "group", 201, false)]
    public async Task IT_B3_084_Text_Length_Boundaries(string caseId, string field, int length, bool ok)
    {
        var code = field == "code" ? new string('C', length) : "CD86";
        var name = field == "name" ? Viet(length) : "Công đoạn IT";
        var group = field == "group" ? Viet(length) : null;

        var r = await Create(SaveOp(Pr1, code, name, group: group));

        r.ShouldNotBeServerError();
        if (ok) { Created(r); (await OpByCodeAsync(Pr1, code)).Should().NotBeNull(); }
        else { r.Code.Should().Be(400, r.ToString()); (await OpByCodeAsync(Pr1, code)).Should().BeNull(); }
    }

    [Theory]
    [InlineData("IT_B3_085", "seq", 0, 0)]
    [InlineData("IT_B3_085", "seq", -1, 0)]
    [InlineData("IT_B3_085", "seq", 32767, 0)]
    [InlineData("IT_B3_085", "seq", 32768, 400)]
    [InlineData("IT_B3_085", "skill", 0, 0)]
    [InlineData("IT_B3_085", "skill", 255, 0)]
    [InlineData("IT_B3_085", "skill", 256, 400)]
    [InlineData("IT_B3_085", "skill", -1, 400)]
    public async Task IT_B3_085_Smallint_And_Tinyint_Boundaries(string caseId, string field, int value, int expected)
    {
        var body = field == "seq" ? SaveOp(Pr1, "CD87", seq: value) : SaveOp(Pr1, "CD87", skill: value);

        var r = await Create(body);

        r.ShouldNotBeServerError();
        if (expected == 0) r.Code.Should().BeOneOf(200, 201, 400); else r.Code.Should().Be(expected, r.ToString());
        var stored = await OpByCodeAsync(Pr1, "CD87");
        if (r.Code < 400) stored.Should().NotBeNull(); else stored.Should().BeNull();
    }

    [Theory]
    [InlineData("IT_B3_086", "9999999.99", 201)]
    [InlineData("IT_B3_086", "10000000.00", 400)]
    [InlineData("IT_B3_086", "45.555", 0)]
    [InlineData("IT_B3_086", "0", 0)]
    [InlineData("IT_B3_086", "-1", 400)]
    [InlineData("IT_B3_086", "null", 201)]
    public async Task IT_B3_086_StandardTime_Boundaries(string caseId, string time, int expected)
    {
        decimal? value = time == "null" ? null : decimal.Parse(time, System.Globalization.CultureInfo.InvariantCulture);

        var r = await Create(SaveOp(Pr1, "CD88", time: value));

        r.ShouldNotBeServerError();
        if (expected == 201) Created(r);
        else if (expected == 0) r.Code.Should().BeOneOf(200, 201, 400);
        else r.Code.Should().Be(expected, r.ToString());
        var stored = await OpByCodeAsync(Pr1, "CD88");
        if (r.Code < 400) stored!.StandardTimeSec.Should().Be(value is null ? null : decimal.Round(value.Value, 2));
        else stored.Should().BeNull();
    }

    [Fact]
    public async Task IT_B3_087_Missing_Fields_And_Bad_Products()
    {
        var before = await Snap();
        var noCode = await Create(new { ProductId = Pr1, OperationName = "x", StationSequence = 3, IsFinalStep = false });
        var noName = await Create(new { ProductId = Pr1, OperationCode = "CD89", StationSequence = 3, IsFinalStep = false });
        var noProduct = await Create(SaveOp(await MissingIdAsync(), "CD90"));
        var retired = await Create(SaveOp(B.Pr3.ProductId, "CD91"));

        Failed(noCode, 400);
        Failed(noName, 400);
        Failed(noProduct, 400, 404);
        Failed(retired, 400);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_089_Update_Operation_Replaces_Components()
    {
        var lastAudit = await LastAuditIdAsync();
        var r = await Acc(Put, OpUrl(P.Op2.OperationId), SaveOp(Pr1, "CD02", "May cổ", 2, comps: [B.Pr1Front.ProductComponentId, B.Pr1Label.ProductComponentId]));

        r.Code.Should().Be(200, r.ToString());
        (await OpAsync(P.Op2.OperationId))!.OperationName.Should().Be("May cổ");
        (await OpComponentsAsync(P.Op2.OperationId)).Should().BeEquivalentTo([B.Pr1Front.ProductComponentId, B.Pr1Label.ProductComponentId]);
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B3_090_Moving_Operation_To_Other_Product_Is_Rejected()
    {
        var r = await Acc(Put, OpUrl(P.Op2.OperationId), SaveOp(B.Pr2.ProductId, "CD02"));

        Failed(r, 400, 409);
        (await OpAsync(P.Op2.OperationId))!.ProductId.Should().Be(Pr1);
    }

    [Fact]
    public async Task IT_B3_091_Rename_To_Existing_Code_Returns_409()
    {
        var before = await Snap();
        var r = await Acc(Put, OpUrl(P.Op2.OperationId), SaveOp(Pr1, "CD01", "Công đoạn CD02", 2));

        Failed(r, 409);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_092_Update_Unknown_Or_Cross_Product_Operation()
    {
        var before = await Snap();
        var missing = await Acc(Put, OpUrl(await MissingIdAsync()), SaveOp(Pr1, "CD02"));
        var notNumber = await Acc(Put, AC + "operations/abc", SaveOp(Pr1, "CD02"));
        var cross = await Acc(Put, OpUrl(P.Op9.OperationId), SaveOp(Pr1, "CD01"));

        Failed(missing, 404);
        Failed(notNumber, 404);
        Failed(cross, 400, 404);
        (await OpAsync(P.Op9.OperationId))!.ProductId.Should().Be(B.Pr2.ProductId);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_093_Discontinue_Keeps_Rates_And_Group_Count()
    {
        var rates = (await RatesOfAsync(P.Op2.OperationId)).Count;
        var r = await Acc(Post, OpUrl(P.Op2.OperationId, "discontinue"));

        r.Code.Should().Be(200, r.ToString());
        (await OpAsync(P.Op2.OperationId))!.Status.Should().Be("DISCONTINUED");
        (await RatesOfAsync(P.Op2.OperationId)).Should().HaveCount(rates);
        var groups = await Acc(Get, AC + $"operation-groups?productId={Pr1}");
        B1Json.Int(groups.Items().Single(g => B1Json.Int(g, "operationGroupId") == P.Og1.OperationGroupId), "operationCount").Should().Be(2);
    }

    [Fact]
    public async Task IT_B3_094_Invalid_State_Changes_Are_Rejected()
    {
        var before = await Snap();
        var already = await Acc(Post, OpUrl(P.Op5.OperationId, "discontinue"));
        var active = await Acc(Post, OpUrl(P.Op1.OperationId, "reactivate"));
        var missing = await Acc(Post, OpUrl(await MissingIdAsync(), "reactivate"));

        Failed(already, 400, 409);
        Failed(active, 400, 409);
        Failed(missing, 404);
        await AssertSame(before);
    }
}
