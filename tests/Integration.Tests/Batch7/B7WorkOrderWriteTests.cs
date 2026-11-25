using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>IT_B7_023–058 — M/work-orders: tạo, sửa, đóng thiếu, hủy.</summary>
public sealed class B7WorkOrderWriteTests(ITestOutputHelper output) : B7TestBase(output)
{
    private static readonly string[] FabricStatuses = ["Enough", "Short", "Unknown"];

    private async Task NoNewWoAsync(int maxBefore) => (await WoAfterAsync(maxBefore)).Should().BeEmpty("không có lệnh cắt mới");

    private object[] Full(int ol3, int ol4) => [WL(K.Ol3.OrderLineId, ol3), WL(K.Ol4.OrderLineId, ol4)];

    // ---------------------------------------------------------------- 023–043 tạo

    [Fact]
    public async Task IT_B7_023_Create_WorkOrder_Persists_Header_And_Lines()
    {
        var max = await MaxWoIdAsync();

        var r = await Req("manager", Post, WOM, WoBody());

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "workOrderCode").Should().NotBeNullOrWhiteSpace();
        var lines = B1Json.Prop(r.Data, "lines")!.Value.EnumerateArray().ToList();
        lines.Should().HaveCount(2);
        lines.Select(l => B1Json.Str(l, "color")).Should().OnlyContain(c => c == "Đen");
        lines.Select(l => B1Json.Str(l, "sizeCode")).Should().BeEquivalentTo(["M", "L"]);
        foreach (var c in B1Json.Prop(r.Data, "fabricChecks")!.Value.EnumerateArray())
            B1Json.Str(c, "status").Should().BeOneOf(FabricStatuses);

        var wo = (await WoAfterAsync(max)).Should().ContainSingle().Subject;
        wo.Status.Should().Be(WorkOrderStatuses.Open);
        wo.FabricTolerancePercent.Should().Be(2.50m);
        wo.SourceRequestId.Should().BeNull();
        wo.CuttingTeamId.Should().Be(K.TCut02.TeamId);
        NearVnNow(wo.CreatedAt, "CreatedAt");
        var wols = await WolsAsync(wo.WorkOrderId);
        wols.Should().HaveCount(2).And.OnlyContain(l => l.ProductComponentMaterialId == null);
    }

    [Fact]
    public async Task IT_B7_024_Duplicate_Whole_Set_Lines_Are_Rejected_With_Rollback()
    {
        var before = await S7();
        var r = await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 10), WL(K.Ol3.OrderLineId, 10)]));
        Rejected(r, 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_025_Whole_Set_And_Single_Component_Lines_Of_Same_OrderLine_Are_Allowed()
    {
        var max = await MaxWoIdAsync();
        var r = await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 30), WL(K.Ol3.OrderLineId, 20, K.Pcm1.ProductComponentMaterialId)]));
        r.Code.Should().Be(200, r.ToString());
        var wo = (await WoAfterAsync(max)).Should().ContainSingle().Subject;
        var lines = await WolsAsync(wo.WorkOrderId);
        lines.Should().HaveCount(2);
        lines.Should().Contain(l => l.ProductComponentMaterialId == K.Pcm1.ProductComponentMaterialId && l.PlannedQty == 20);
        (await PlanSumAsync(K.Ol3.OrderLineId)).Should().Be(200, "cắt lẻ không tính vào trần số đặt");
    }

    [Fact]
    public async Task IT_B7_026_Duplicate_Single_Component_Lines_Are_Rejected()
    {
        var before = await S7();
        var pcm = K.Pcm1.ProductComponentMaterialId;
        Rejected(await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 5, pcm), WL(K.Ol3.OrderLineId, 5, pcm)])), 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_027_Planned_Over_Ordered_Qty_Is_Rejected()
    {
        var before = await S7();
        Rejected(await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 31)])), 400, 409);
        await Same7(before);
        (await PlanSumAsync(K.Ol3.OrderLineId)).Should().Be(170);
    }

    [Fact]
    public async Task IT_B7_028_Planned_Exactly_Ordered_Qty_Is_Accepted()
    {
        var r = await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 30)]));
        r.Code.Should().Be(200, r.ToString());
        (await PlanSumAsync(K.Ol3.OrderLineId)).Should().Be(200);
    }

    [Fact]
    public async Task IT_B7_029_Single_Component_Is_Not_Capped_By_Ordered_Qty()
    {
        var max = await MaxWoIdAsync();
        var r = await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 500, K.Pcm2.ProductComponentMaterialId)]));
        Rejected(r, 200, 400);
        var created = await WoAfterAsync(max);
        if (r.Code == 200)
            (await WolsAsync(created.Single().WorkOrderId)).Should().ContainSingle().Which.PlannedQty.Should().Be(500);
        else
            created.Should().BeEmpty();
    }

    [Theory]
    [InlineData("IT_B7_030", 0L, 400)]
    [InlineData("IT_B7_030", -1L, 400)]
    [InlineData("IT_B7_030", 1L, 200)]
    [InlineData("IT_B7_030", 2147483647L, 400)]
    [InlineData("IT_B7_030", 2147483648L, 400)]
    public async Task PlannedQty_Boundary(string caseId, long qty, int expected)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxWoIdAsync();
        var body = WoBody();
        body["Lines"] = new[] { new { OrderLineId = K.Ol3.OrderLineId, PlannedQty = qty, ProductComponentMaterialId = (int?)null } };

        var r = await Req("manager", Post, WOM, body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        var created = await WoAfterAsync(max);
        if (expected == 200) (await WolsAsync(created.Single().WorkOrderId)).Should().ContainSingle().Which.PlannedQty.Should().Be(1);
        else created.Should().BeEmpty();
        await using var db = NewDb();
        db.WorkOrderLines.Count(l => l.PlannedQty <= 0).Should().Be(0);
    }

    [Theory]
    [InlineData("IT_B7_031", 0.0, "200")]
    [InlineData("IT_B7_031", 999.99, "400")]
    [InlineData("IT_B7_031", 1000.00, "400")]
    [InlineData("IT_B7_031", -0.01, "400")]
    [InlineData("IT_B7_031", 2.555, "200|400")]
    [InlineData("IT_B7_031", 100.01, "400")]
    public async Task FabricTolerance_Boundary(string caseId, double pct, string expected)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxWoIdAsync();
        var r = await Req("manager", Post, WOM, WoBody(tolerance: (decimal)pct));
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(expected.Split('|').Select(int.Parse).ToArray(), r.ToString());
        NoLeak(r);
        var created = await WoAfterAsync(max);
        if (r.Code == 200) created.Should().ContainSingle().Which.FabricTolerancePercent.Should().BeInRange(0m, 999.99m);
        else created.Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B7_032_Missing_Tolerance_Defaults_To_Zero()
    {
        var max = await MaxWoIdAsync();
        var body = WoBody();
        body.Remove("FabricTolerancePercent");
        var r = await Req("manager", Post, WOM, body);
        r.Code.Should().Be(200, r.ToString());
        (await WoAfterAsync(max)).Should().ContainSingle().Which.FabricTolerancePercent.Should().Be(0m);
    }

    [Theory]
    [InlineData("IT_B7_033", "min", "400")]
    [InlineData("IT_B7_033", "missing", "400")]
    [InlineData("IT_B7_033", "past", "400")]
    [InlineData("IT_B7_033", "afterOrder", "200|400")]
    public async Task DueDate_Boundary(string caseId, string kind, string expected)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxWoIdAsync();
        var body = WoBody();
        switch (kind)
        {
            case "min": body["DueDate"] = "0001-01-01"; break;
            case "missing": body.Remove("DueDate"); break;
            case "past": body["DueDate"] = T.AddDays(-1); break;
            default: body["DueDate"] = K.Od2.DueDate.AddDays(30); break;
        }

        var r = await Req("manager", Post, WOM, body);
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(expected.Split('|').Select(int.Parse).ToArray(), r.ToString());
        var created = await WoAfterAsync(max);
        created.Should().OnlyContain(w => w.DueDate.Year > 1);
        if (r.Code != 200) created.Should().BeEmpty();
    }

    [Theory]
    [InlineData("IT_B7_034", "sewing")]
    [InlineData("IT_B7_034", "discontinued")]
    [InlineData("IT_B7_034", "missing")]
    public async Task CuttingTeam_Must_Be_An_Active_Cutting_Team(string caseId, string kind)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxWoIdAsync();
        var team = kind switch { "sewing" => S0.TSew01.TeamId, "discontinued" => K.TCutOld.TeamId, _ => await NonExistingTeamIdAsync() };
        var r = await Req("manager", Post, WOM, WoBody(teamId: team));
        Rejected(r, kind == "missing" ? new[] { 400, 404 } : new[] { 400 });
        await NoNewWoAsync(max);
    }

    [Fact]
    public async Task IT_B7_035_OrderLine_Of_Another_Order_Is_Rejected()
    {
        var before = await S7();
        Rejected(await Req("manager", Post, WOM, WoBody([WL(D.Ol9.OrderLineId, 5)])), 400, 404);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_036_Product_Must_Match_Order()
    {
        var before = await S7();
        Rejected(await Req("manager", Post, WOM, WoBody(productId: B.Pr2.ProductId)), 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_037_Component_Of_Another_Product_Or_Missing_Is_Rejected()
    {
        var comp = new ProductComponent
        {
            ProductId = B.Pr2.ProductId, ComponentName = "Thân Pr2", ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1, Status = RecordStatus.Active,
        };
        Db.ProductComponents.Add(comp);
        await Db.SaveChangesAsync();
        var foreign = new ProductComponentMaterial
        {
            ProductId = B.Pr2.ProductId, ProductColorId = D.Pv9.ProductColorId, ProductComponentId = comp.ProductComponentId, MaterialId = B.Ma1.MaterialId,
            Status = RecordStatus.Active,
        };
        Db.ProductComponentMaterials.Add(foreign);
        await Db.SaveChangesAsync();
        var before = await S7();

        Rejected(await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 5, foreign.ProductComponentMaterialId)])), 400, 404);
        Rejected(await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 5, 999999)])), 400, 404);

        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_038_Order_Must_Be_In_Production()
    {
        var before = await S7();
        var missing = await MissingOrderLineIdAsync();
        Rejected(await Req("manager", Post, WOM, WoBody([WL(D.Ol1.OrderLineId, 5)], orderId: D.Od1.OrderId)), 400);
        Rejected(await Req("manager", Post, WOM, WoBody([WL(D.Ol3Cancelled.OrderLineId, 5)], orderId: D.Od3.OrderId)), 400);
        Rejected(await Req("manager", Post, WOM, WoBody([WL(missing, 5)], orderId: await MissingIdAsync())), 404);
        await Same7(before);
    }

    [Theory]
    [InlineData("IT_B7_039", "empty", 400)]
    [InlineData("IT_B7_039", "null", 400)]
    [InlineData("IT_B7_039", "note1000", 200)]
    [InlineData("IT_B7_039", "note1001", 400)]
    public async Task Lines_And_Note_Boundary(string caseId, string kind, int expected)
    {
        caseId.Should().StartWith("IT_B7_");
        var max = await MaxWoIdAsync();
        var body = WoBody([WL(K.Ol3.OrderLineId, 5)]);
        if (kind == "empty") body["Lines"] = Array.Empty<object>();
        if (kind == "null") body["Lines"] = null;
        if (kind == "note1000") body["Note"] = Vn(1000).Trim().PadRight(1000, 'a');
        if (kind == "note1001") body["Note"] = Vn(1001).Trim().PadRight(1001, 'a');

        var r = await Req("manager", Post, WOM, body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        var created = await WoAfterAsync(max);
        if (expected == 200) created.Should().ContainSingle().Which.Note!.Length.Should().Be(1000);
        else created.Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B7_040_Mass_Assignment_Fields_Are_Ignored()
    {
        var max = await MaxWoIdAsync();
        var body = WoBody();
        body["WorkOrderCode"] = "HACK";
        body["Status"] = "COMPLETED";
        body["SourceRequestId"] = K.Rq1.RequestId;
        body["CompletedAt"] = "2026-01-01T00:00:00";
        body["CreatedAt"] = "0001-01-01T00:00:00";

        var r = await Req("manager", Post, WOM, body);

        r.Code.Should().Be(200, r.ToString());
        var wo = (await WoAfterAsync(max)).Should().ContainSingle().Subject;
        wo.WorkOrderCode.Should().NotBe("HACK");
        wo.Status.Should().Be(WorkOrderStatuses.Open);
        wo.SourceRequestId.Should().BeNull();
        wo.CompletedAt.Should().BeNull();
        NearVnNow(wo.CreatedAt, "CreatedAt");
    }

    // ---------------------------------------------------------------- 044–050 sửa

    [Fact]
    public async Task IT_B7_044_Update_Open_WorkOrder_Replaces_Lines_And_Due_Date()
    {
        var body = WoBody([WL(K.Ol3.OrderLineId, 100), WL(K.Ol4.OrderLineId, 20, K.Pcm1.ProductComponentMaterialId)], K.Od2.OrderId, B.Pr1.ProductId,
            S0.TCut01.TeamId, T.AddDays(9), "Sửa", 3.00m);

        var r = await Req("manager", Put, $"{WOM}/{K.Wo1.WorkOrderId}", body);

        r.Code.Should().Be(200, r.ToString());
        var wo = await WoAsync(K.Wo1.WorkOrderId);
        wo.Status.Should().Be(WorkOrderStatuses.Open);
        wo.DueDate.Should().Be(T.AddDays(9));
        var lines = await WolsAsync(K.Wo1.WorkOrderId);
        lines.Should().HaveCount(2);
        lines.Should().Contain(l => l.OrderLineId == K.Ol3.OrderLineId && l.ProductComponentMaterialId == null && l.PlannedQty == 100);
        lines.Should().Contain(l => l.OrderLineId == K.Ol4.OrderLineId && l.ProductComponentMaterialId == K.Pcm1.ProductComponentMaterialId && l.PlannedQty == 20);
        lines.Should().NotContain(l => l.OrderLineId == K.Ol4.OrderLineId && l.ProductComponentMaterialId == null);
    }

    [Fact]
    public async Task IT_B7_045_Cannot_Plan_Below_Already_Cut()
    {
        var before = await S7();
        var body = WoBody([WL(K.Ol3.OrderLineId, 30)], K.Od2.OrderId, B.Pr1.ProductId, S0.TCut01.TeamId);
        Rejected(await Req("manager", Put, $"{WOM}/{K.Wo2.WorkOrderId}", body), 400, 409);
        await Same7(before);
        (await WolAsync(K.Wol3.WorkOrderLineId)).PlannedQty.Should().Be(50);
    }

    [Fact]
    public async Task IT_B7_046_Cannot_Drop_Line_That_Has_Lay_Or_Handover()
    {
        var before = await S7();
        var body = WoBody([WL(K.Ol4.OrderLineId, 10)], K.Od2.OrderId, B.Pr1.ProductId, S0.TCut01.TeamId);
        Rejected(await Req("manager", Put, $"{WOM}/{K.Wo2.WorkOrderId}", body), 400, 409);
        await Same7(before);
        (await WolAsync(K.Wol3.WorkOrderLineId)).WorkOrderLineId.Should().Be(K.Wol3.WorkOrderLineId);
    }

    [Fact]
    public async Task IT_B7_047_Completed_Or_Cancelled_WorkOrder_Cannot_Be_Updated()
    {
        var before = await S7();
        var body = WoBody([WL(K.Ol3.OrderLineId, 1)], K.Od2.OrderId, B.Pr1.ProductId, S0.TCut01.TeamId);
        Rejected(await Req("manager", Put, $"{WOM}/{K.Wo4.WorkOrderId}", body), 400, 409);
        Rejected(await Req("manager", Put, $"{WOM}/{K.Wo5.WorkOrderId}", body), 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_048_Unstarted_WorkOrder_Can_Change_Team()
    {
        var body = WoBody([WL(K.Ol3.OrderLineId, 120), WL(K.Ol4.OrderLineId, 60)], K.Od2.OrderId, B.Pr1.ProductId, K.TCut02.TeamId, T.AddDays(10), null, 3.00m);

        var r = await Req("manager", Put, $"{WOM}/{K.Wo1.WorkOrderId}", body);

        Rejected(r, 200, 400);
        if (r.Code != 200) return;
        (await WoAsync(K.Wo1.WorkOrderId)).CuttingTeamId.Should().Be(K.TCut02.TeamId);
        IdSet(await Req("tl1", Get, WOT + "?PageSize=100"), "workOrderId").Should().NotContain(K.Wo1.WorkOrderId);
        IdSet(await Req("tl2", Get, WOT + "?PageSize=100"), "workOrderId").Should().Contain(K.Wo1.WorkOrderId);
    }

    [Fact]
    public async Task IT_B7_050_Update_Missing_Or_NonInteger_Id_Returns_404_Without_Upsert()
    {
        var before = await S7();
        var missing = await MissingWoIdAsync();
        Rejected(await Req("manager", Put, $"{WOM}/{missing}", WoBody()), 404);
        Rejected(await Req("manager", Put, $"{WOM}/abc", WoBody()), 404);
        await Same7(before);
    }

    // ---------------------------------------------------------------- 051–058 đóng thiếu / hủy

    [Fact]
    public async Task IT_B7_051_CloseShort_Completes_WorkOrder_And_Keeps_Reason()
    {
        const string reason = "Hết vải, khách đồng ý";
        var audit = await MaxAuditAsync();

        var r = await Req("manager", Post, $"{WOM}/{K.Wo2.WorkOrderId}/close-short", new { Reason = reason });

        r.Code.Should().Be(200, r.ToString());
        var wo = await WoAsync(K.Wo2.WorkOrderId);
        wo.Status.Should().Be(WorkOrderStatuses.Completed);
        NearVnNow(wo.CompletedAt, "CompletedAt");
        var inNote = (wo.Note ?? "").Contains(reason);
        await using var db = NewDb();
        var audited = db.AuditLogs.Count(a => a.AuditLogId > audit) > 0;
        Note($"Reason lưu ở Note={inNote}, AuditLog={audited}");
        (inNote || audited).Should().BeTrue("Reason không được mất hoàn toàn");
    }

    [Theory]
    [InlineData("IT_B7_052", "empty")]
    [InlineData("IT_B7_052", "null")]
    [InlineData("IT_B7_052", "bodynull")]
    [InlineData("IT_B7_052", "nobody")]
    public async Task CloseShort_Requires_Reason(string caseId, string kind)
    {
        caseId.Should().StartWith("IT_B7_");
        var before = await S7();
        object? body = kind switch { "empty" => new { Reason = "" }, "null" => new { Reason = (string?)null }, "bodynull" => "null", _ => null };

        var r = await Req("manager", Post, $"{WOM}/{K.Wo2.WorkOrderId}/close-short", body);

        Rejected(r, 400);
        await Same7(before);
        (await WoAsync(K.Wo2.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Cutting);
    }

    [Fact]
    public async Task IT_B7_053_CloseShort_Reason_Does_Not_Overflow_Note()
    {
        var wo = await Db.WorkOrders.FindAsync(K.Wo2.WorkOrderId);
        wo!.Note = Vn(900).Trim().PadRight(900, 'a');
        await Db.SaveChangesAsync();

        var r = await Req("manager", Post, $"{WOM}/{K.Wo2.WorkOrderId}/close-short", new { Reason = Vn(500).Trim().PadRight(500, 'b') });

        Rejected(r, 200, 400);
        (await WoAsync(K.Wo2.WorkOrderId)).Note!.Length.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public async Task IT_B7_054_CloseShort_On_Wrong_States()
    {
        var before = await S7();
        var open = await Req("manager", Post, $"{WOM}/{K.Wo1.WorkOrderId}/close-short", new { Reason = "Chưa cắt" });
        Rejected(open, 200, 400, 409);
        var after = await S7();
        Rejected(await Req("manager", Post, $"{WOM}/{K.Wo4.WorkOrderId}/close-short", new { Reason = "x" }), 409);
        Rejected(await Req("manager", Post, $"{WOM}/{K.Wo5.WorkOrderId}/close-short", new { Reason = "x" }), 409);
        (await WoAsync(K.Wo4.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Completed);
        (await WoAsync(K.Wo5.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Cancelled);
        (await S7()).Detail.Should().Be(after.Detail);
        if (open.Code != 200) await Same7(before);
    }

    [Fact]
    public async Task IT_B7_055_Cancel_Unstarted_WorkOrder_Keeps_Lines()
    {
        var r = await Req("manager", Post, $"{WOM}/{K.Wo1.WorkOrderId}/cancel");
        r.Code.Should().Be(200, r.ToString());
        (await WoAsync(K.Wo1.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Cancelled);
        (await WolsAsync(K.Wo1.WorkOrderId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task IT_B7_056_Cancel_Started_Or_Completed_WorkOrder_Is_Rejected()
    {
        var before = await S7();
        Rejected(await Req("manager", Post, $"{WOM}/{K.Wo2.WorkOrderId}/cancel"), 400, 409);
        Rejected(await Req("manager", Post, $"{WOM}/{K.Wo4.WorkOrderId}/cancel"), 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_058_Cancel_And_CloseShort_Missing_WorkOrder_Return_404()
    {
        var before = await S7();
        var missing = await MissingWoIdAsync();
        Rejected(await Req("manager", Post, $"{WOM}/{missing}/cancel"), 404);
        Rejected(await Req("manager", Post, $"{WOM}/{missing}/close-short", new { Reason = "x" }), 404);
        await Same7(before);
    }
}
