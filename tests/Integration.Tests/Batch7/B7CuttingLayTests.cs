using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>IT_B7_071–095 — TL/cutting-lays (ca đồng thời 073, 088, 093 nằm ở B7ConcurrencyTests).</summary>
public sealed class B7CuttingLayTests(ITestOutputHelper output) : B7TestBase(output)
{
    private object[] Ol3(int qty) => [LL(K.Ol3.OrderLineId, qty)];

    [Fact]
    public async Task IT_B7_071_Create_Lay_Takes_Team_From_Token_And_Moves_WorkOrder_To_Cutting()
    {
        var max = await MaxLayIdAsync();

        var r = await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, Ol3(60), [LF(B.Ma1.MaterialId, 45.50m)], "AFTERNOON", T, "Bàn 1"));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "laySequence").Should().Be(1);
        B1Json.Str(r.Data, "sizeSummary").Should().Contain("M:60");
        var lay = (await LaysAfterAsync(max)).Should().ContainSingle().Subject;
        lay.CuttingTeamId.Should().Be(S0.TCut01.TeamId);
        lay.LaySequence.Should().Be(1);
        lay.Status.Should().Be(CuttingLayStatuses.Recorded);
        NearVnNow(lay.CreatedAt, "CreatedAt");
        (await LayLinesAsync(lay.CuttingLayId)).Should().ContainSingle().Which.ActualQty.Should().Be(60);
        (await LayFabricsAsync(lay.CuttingLayId)).Should().ContainSingle().Which.UsedM.Should().Be(45.50m);
        (await WoAsync(K.Wo1.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Cutting);
    }

    [Fact]
    public async Task IT_B7_072_Second_Lay_In_Same_Shift_Gets_Next_Sequence()
    {
        var max = await MaxLayIdAsync();
        var r = await Req("tl1", Post, CLT, LayBody(K.Wo2.WorkOrderId, Ol3(5), null, "MORNING"));
        r.Code.Should().Be(200, r.ToString());
        (await LaysAfterAsync(max)).Should().ContainSingle().Which.LaySequence.Should().Be(2);
    }

    [Fact]
    public async Task IT_B7_074_Sequence_Is_Per_Team()
    {
        var max = await MaxLayIdAsync();
        var a = await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, Ol3(5), null, "AFTERNOON"));
        var b = await Req("tl2", Post, CLT, LayBody(K.Wo3.WorkOrderId, [LL(K.Ol4.OrderLineId, 1, K.Pcm2.ProductComponentMaterialId)], null, "AFTERNOON"));
        a.Code.Should().Be(200, a.ToString());
        b.Code.Should().Be(200, b.ToString());
        var lays = await LaysAfterAsync(max);
        lays.Should().HaveCount(2).And.OnlyContain(l => l.LaySequence == 1);
        lays.Select(l => l.CuttingTeamId).Should().BeEquivalentTo([S0.TCut01.TeamId, K.TCut02.TeamId]);
    }

    [Theory]
    [InlineData("IT_B7_075", "NIGHT")]
    [InlineData("IT_B7_075", "morning")]
    [InlineData("IT_B7_075", "")]
    [InlineData("IT_B7_075", "MORNINGAAAA")]
    public async Task ShiftCode_Boundary(string caseId, string shift)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxLayIdAsync();
        var r = await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, Ol3(5), null, shift));
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(shift == "morning" ? new[] { 200, 400 } : new[] { 400 }, r.ToString());
        NoLeak(r);
        foreach (var lay in await LaysAfterAsync(max)) ShiftCodes.All.Should().Contain(lay.ShiftCode);
        if (r.Code != 200) (await LaysAfterAsync(max)).Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B7_076_Duplicate_Lines_Are_Rejected_With_Rollback()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, [LL(K.Ol3.OrderLineId, 5), LL(K.Ol3.OrderLineId, 5)])), 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_077_Duplicate_Fabrics_Are_Rejected()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, Ol3(5), [LF(B.Ma1.MaterialId, 5m), LF(B.Ma1.MaterialId, 6m)])), 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_078_Cut_Over_Plan_Is_Rejected()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo2.WorkOrderId, Ol3(11), null, "MORNING")), 400, 409);
        await Same7(before);
        (await CutSumAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId)).Should().Be(40);
    }

    [Fact]
    public async Task IT_B7_079_Cut_Exactly_Plan_Is_Accepted()
    {
        var r = await Req("tl1", Post, CLT, LayBody(K.Wo2.WorkOrderId, Ol3(10), null, "MORNING"));
        r.Code.Should().Be(200, r.ToString());
        (await CutSumAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId)).Should().Be(50);
    }

    [Theory]
    [InlineData("IT_B7_080", 0L)]
    [InlineData("IT_B7_080", -1L)]
    [InlineData("IT_B7_080", 2147483648L)]
    public async Task ActualQty_Boundary(string caseId, long qty)
    {
        caseId.Should().StartWith("IT_B7_");
        var before = await S7();
        var body = LayBody(K.Wo1.WorkOrderId);
        body["Lines"] = new[] { new { OrderLineId = K.Ol3.OrderLineId, ProductComponentMaterialId = (int?)null, ActualQty = qty } };
        Rejected(await Req("tl1", Post, CLT, body), 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_081_Lines_Not_In_WorkOrder_Are_Rejected()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo2.WorkOrderId, [LL(K.Ol4.OrderLineId, 1)], null, "MORNING")), 400, 404);
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo2.WorkOrderId, [LL(D.Ol9.OrderLineId, 1)], null, "MORNING")), 400, 404);
        await Same7(before);
    }

    [Theory]
    [InlineData("IT_B7_082", 9999999.99, true)]
    [InlineData("IT_B7_082", 10000000.00, false)]
    [InlineData("IT_B7_082", 0.0, false)]
    [InlineData("IT_B7_082", -1.0, false)]
    [InlineData("IT_B7_082", 12.345, true)]
    [InlineData("IT_B7_082", null, true)]
    public async Task UsedM_Boundary(string caseId, double? usedM, bool mayBeAccepted)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxLayIdAsync();
        var r = await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, Ol3(5), [LF(B.Ma1.MaterialId, (decimal?)usedM)]));
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(mayBeAccepted ? new[] { 200, 400 } : new[] { 400 }, r.ToString());
        NoLeak(r);
        var created = await LaysAfterAsync(max);
        if (r.Code != 200) { created.Should().BeEmpty(); return; }
        var fabric = (await LayFabricsAsync(created.Single().CuttingLayId)).Single();
        if (fabric.UsedM is not null) fabric.UsedM.Should().BeGreaterThan(0m);
    }

    [Theory]
    [InlineData("IT_B7_083", "empty")]
    [InlineData("IT_B7_083", "foreign")]
    [InlineData("IT_B7_083", "missing")]
    public async Task Fabrics_Must_Match_Norm(string caseId, string kind)
    {
        caseId.Should().StartWith("IT_B7_");
        var before = await S7();
        object[] fabrics = kind switch
        {
            "empty" => [],
            "foreign" => [LF(B.Ma2.MaterialId, 5m)],
            _ => [LF(await MissingIdAsync(), 5m)],
        };
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, Ol3(5), fabrics)), kind == "missing" ? new[] { 400, 404 } : new[] { 400 });
        await Same7(before);
    }

    [Theory]
    [InlineData("IT_B7_084", "min")]
    [InlineData("IT_B7_084", "missing")]
    [InlineData("IT_B7_084", "tomorrow")]
    [InlineData("IT_B7_084", "beforeWorkOrder")]
    public async Task WorkDate_Boundary(string caseId, string kind)
    {
        caseId.Should().StartWith("IT_B7_");
        var before = await S7();
        var body = LayBody(K.Wo1.WorkOrderId, Ol3(5));
        switch (kind)
        {
            case "min": body["WorkDate"] = "0001-01-01"; break;
            case "missing": body.Remove("WorkDate"); break;
            case "tomorrow": body["WorkDate"] = T.AddDays(1); break;
            default: body["WorkDate"] = T.AddDays(-30); break;
        }

        Rejected(await Req("tl1", Post, CLT, body), 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_085_Completed_Or_Cancelled_WorkOrder_Rejects_Lays()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo4.WorkOrderId, Ol3(1))), 400, 409);
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo5.WorkOrderId, Ol3(1))), 400, 409);
        await Same7(before);
    }

    [Theory]
    [InlineData("IT_B7_086", "empty", 400)]
    [InlineData("IT_B7_086", "note500", 200)]
    [InlineData("IT_B7_086", "note501", 400)]
    public async Task Lines_And_Note_Boundary(string caseId, string kind, int expected)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxLayIdAsync();
        var body = LayBody(K.Wo1.WorkOrderId, Ol3(5));
        if (kind == "empty") body["Lines"] = Array.Empty<object>();
        if (kind == "note500") body["Note"] = Vn(500).Trim().PadRight(500, 'a');
        if (kind == "note501") body["Note"] = Vn(501).Trim().PadRight(501, 'a');

        var r = await Req("tl1", Post, CLT, body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        var created = await LaysAfterAsync(max);
        if (expected == 200) created.Should().ContainSingle().Which.Note!.Length.Should().Be(500);
        else created.Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B7_087_Mass_Assignment_Is_Ignored()
    {
        var max = await MaxLayIdAsync();
        var body = LayBody(K.Wo1.WorkOrderId, Ol3(5));
        body["CuttingTeamId"] = K.TCut02.TeamId;
        body["LaySequence"] = 99;
        body["Status"] = "LOCKED";

        var r = await Req("tl1", Post, CLT, body);

        r.Code.Should().Be(200, r.ToString());
        var lay = (await LaysAfterAsync(max)).Should().ContainSingle().Subject;
        lay.CuttingTeamId.Should().Be(S0.TCut01.TeamId);
        lay.LaySequence.Should().NotBe(99);
        lay.Status.Should().Be(CuttingLayStatuses.Recorded);
    }

    [Fact]
    public async Task IT_B7_089_Update_Recorded_Lay_Replaces_Lines_And_Fabrics()
    {
        var body = LayBody(K.Wo2.WorkOrderId, Ol3(45), [LF(B.Ma1.MaterialId, 32.00m)], "MORNING", T, "Mới");

        var r = await Req("tl1", Put, $"{CLT}/{K.Cl1.CuttingLayId}", body);

        r.Code.Should().Be(200, r.ToString());
        var lay = await LayAsync(K.Cl1.CuttingLayId);
        lay.LaySequence.Should().Be(1);
        lay.CuttingTeamId.Should().Be(S0.TCut01.TeamId);
        lay.Note.Should().Be("Mới");
        (await LayLinesAsync(K.Cl1.CuttingLayId)).Should().ContainSingle().Which.ActualQty.Should().Be(45);
        (await LayFabricsAsync(K.Cl1.CuttingLayId)).Should().ContainSingle().Which.UsedM.Should().Be(32.00m);
    }

    [Fact]
    public async Task IT_B7_090_Locked_Lay_Cannot_Be_Updated()
    {
        var before = await S7();
        Rejected(await Req("tl1", Put, $"{CLT}/{K.ClLk.CuttingLayId}", LayBody(K.Wo2.WorkOrderId, Ol3(1), null, "AFTERNOON", T.AddDays(-1))), 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_091_Cannot_Cut_Less_Than_Already_Handed_Over()
    {
        var before = await S7();
        Rejected(await Req("tl1", Put, $"{CLT}/{K.Cl1.CuttingLayId}", LayBody(K.Wo2.WorkOrderId, Ol3(35), null, "MORNING")), 400, 409);
        await Same7(before);
        (await LayLinesAsync(K.Cl1.CuttingLayId)).Single().ActualQty.Should().Be(40);
    }

    [Fact]
    public async Task IT_B7_092_Changing_WorkOrder_Or_Shift_Keeps_Keys_Unique()
    {
        var moved = await Req("tl1", Put, $"{CLT}/{K.Cl1.CuttingLayId}", LayBody(K.Wo1.WorkOrderId, Ol3(5), null, "MORNING"));
        Rejected(moved, 200, 400, 409);
        var shifted = await Req("tl1", Put, $"{CLT}/{K.Cl1.CuttingLayId}", LayBody(K.Wo2.WorkOrderId, Ol3(40), null, "AFTERNOON"));
        Rejected(shifted, 200, 400, 409);
        await using var db = NewDb();
        var dup = await db.CuttingLays.AsNoTracking().GroupBy(l => new { l.CuttingTeamId, l.WorkDate, l.ShiftCode, l.LaySequence }).Where(g => g.Count() > 1).CountAsync();
        dup.Should().Be(0);
    }

    [Fact]
    public async Task IT_B7_094_List_Filters_By_Date_And_Status()
    {
        var before = await S7();
        var ok = await Req("tl1", Get, $"{CLT}?WorkDate={T:yyyy-MM-dd}&Status=RECORDED&PageSize=100");
        var min = await Req("tl1", Get, $"{CLT}?WorkDate=0001-01-01");
        var bad = await Req("tl1", Get, $"{CLT}?WorkDate=abc");
        ok.Code.Should().Be(200, ok.ToString());
        IdSet(ok, "cuttingLayId").Should().Contain(K.Cl1.CuttingLayId).And.NotContain(K.ClLk.CuttingLayId).And.NotContain(K.Cl9.CuttingLayId);
        ok.Items().Select(i => B1Json.Str(i, "status")).Should().OnlyContain(s => s == CuttingLayStatuses.Recorded);
        min.Code.Should().Be(200, min.ToString());
        min.Items().Should().BeEmpty();
        Rejected(bad, 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_095_Missing_Or_NonInteger_Lay_Id_Returns_404()
    {
        var before = await S7();
        Rejected(await Req("tl1", Put, $"{CLT}/{await MissingLayIdAsync()}", LayBody(K.Wo2.WorkOrderId, Ol3(1))), 404);
        Rejected(await Req("tl1", Get, $"{CLT}/abc"), 404);
        await Same7(before);
    }
}
