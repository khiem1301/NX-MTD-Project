using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4 [Đ7] — ca đồng thời (connection độc lập, COMMIT, tự dọn): mỗi ca chạy <see cref="B4ConcurrentTestBase.Rounds"/> vòng.</summary>
public sealed class B4ConcurrencyTests(ITestOutputHelper output) : B4ConcurrentTestBase(output)
{
    private object[] KeepLines(List<NX_MTD.Domain.Entities.OrderLine> lines) =>
        [Ln(B.Pr1.ProductId, "Đen", "M", 100, 25_000m, lines[0].OrderLineId), Ln(B.Pr1.ProductId, "Đen", "L", 100, 25_000m, lines[1].OrderLineId)];

    private async Task<Api[]> Race2(Func<Task<Api>> a, Func<Task<Api>> b) => await RaceAsync(a, b);

    [Fact]
    public async Task IT_B4_030_Concurrent_Create_Same_OrderCode_One_Wins()
    {
        var token = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var code = Code();
            var results = await Race2(() => PostAsync(A + "customer-orders", token, OrderBody(code)), () => PostAsync(A + "customer-orders", token, OrderBody(code)));

            OneWinner(results, 409);
            (await CountOrdersByCodeAsync(code)).Should().Be(1, $"vòng {i}");
            await using var db = NewDb();
            var id = (await db.CustomerOrders.AsNoTracking().SingleAsync(o => o.OrderCode == code)).OrderId;
            (await LinesAsync(id)).Should().HaveCount(2, "không có dòng mồ côi từ request thua");
        }
    }

    [Fact]
    public async Task IT_B4_031_Concurrent_Create_With_Same_Suggested_Code_One_Wins()
    {
        var token = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var a = await GetAsync(A + "customer-orders/next-code", token);
            var b = await GetAsync(A + "customer-orders/next-code", token);
            var code = a.Data.ValueKind == System.Text.Json.JsonValueKind.String ? a.Data.GetString()! : B1Json.Str(a.Data, "code") ?? B1Json.Str(a.Data, "orderCode")!;
            b.Code.Should().Be(200);

            var results = await Race2(() => PostAsync(A + "customer-orders", token, OrderBody(code, 1)), () => PostAsync(A + "customer-orders", token, OrderBody(code, 1)));

            OneWinner(results, 409);
            (await CountOrdersByCodeAsync(code)).Should().Be(1, $"vòng {i}");
        }
    }

    [Fact]
    public async Task IT_B4_045_Concurrent_Update_Same_RowVer_One_Wins()
    {
        var token = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var (od, lines) = await NewOrderAsync();
            var rv = (await OrderAsync(od.OrderId)).RowVer;
            var d1 = Today.AddDays(40); var d2 = Today.AddDays(50);
            var b1 = await UpdateBodyAsync(od, KeepLines(lines), d1, rv);
            var b2 = await UpdateBodyAsync(od, KeepLines(lines), d2, rv);

            var results = await Race2(() => PutAsync(A + $"customer-orders/{od.OrderId}", token, b1), () => PutAsync(A + $"customer-orders/{od.OrderId}", token, b2));

            OneWinner(results, 409);
            var winner = Array.FindIndex(results, IsSuccess) == 0 ? d1 : d2;
            (await OrderAsync(od.OrderId)).DueDate.Should().Be(winner, $"vòng {i}: chỉ dữ liệu của request thắng được lưu");
        }
    }

    [Fact]
    public async Task IT_B4_061_Concurrent_StartProduction_And_Cancel_Leave_One_Final_State()
    {
        var token = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var (od, _) = await NewOrderAsync(ready: true);
            var rv = (await OrderAsync(od.OrderId)).RowVer;

            var results = await Race2(() => PostAsync(A + $"customer-orders/{od.OrderId}/start-production", token, new { RowVer = rv }),
                () => PostAsync(A + $"customer-orders/{od.OrderId}/cancel", token, new { Reason = "Khách hủy", RowVer = rv }));

            OneWinner(results, 409);
            var status = (await OrderAsync(od.OrderId)).Status;
            status.Should().Be(IsSuccess(results[0]) ? "IN_PRODUCTION" : "CANCELLED", $"vòng {i}");
        }
    }

    [Fact]
    public async Task IT_B4_094_Concurrent_Bulk_Same_Pair_Is_Idempotent()
    {
        var token = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (_, lines) = await NewOrderAsync();
            var body = new { OrderLineIds = new[] { lines[0].OrderLineId }, TeamIds = new[] { S0.TSew01.TeamId } };

            var results = await Race2(() => PostAsync(M + "order-line-teams/bulk", token, body), () => PostAsync(M + "order-line-teams/bulk", token, body));

            AllowedOutcomes(results);
            (await TeamIdsAsync(lines[0].OrderLineId)).Should().Equal(S0.TSew01.TeamId);
        }
    }

    [Fact]
    public async Task IT_B4_095_Concurrent_Set_Teams_Never_Mixes_Both_Sets()
    {
        var token = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (_, lines) = await NewOrderAsync();
            var id = lines[0].OrderLineId;

            var results = await Race2(() => PutAsync(M + "order-line-teams", token, new { OrderLineId = id, TeamIds = new[] { S0.TSew01.TeamId } }),
                () => PutAsync(M + "order-line-teams", token, new { OrderLineId = id, TeamIds = new[] { S0.TSew02.TeamId } }));

            AllowedOutcomes(results);
            var teams = await TeamIdsAsync(id);
            teams.Should().HaveCount(1, $"vòng {i}: chỉ một trong hai tập tổ");
            teams.Single().Should().BeOneOf(S0.TSew01.TeamId, S0.TSew02.TeamId);
        }
    }

    [Fact]
    public async Task IT_B4_096_Concurrent_Clear_And_Bulk_Add_Stay_Consistent()
    {
        var token = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (_, lines) = await NewOrderAsync();
            var id = lines[0].OrderLineId;
            (await PutAsync(M + "order-line-teams", token, new { OrderLineId = id, TeamIds = new[] { S0.TSew01.TeamId } })).Code.Should().Be(200);

            var results = await Race2(() => PutAsync(M + "order-line-teams", token, new { OrderLineId = id, TeamIds = Array.Empty<int>() }),
                () => PostAsync(M + "order-line-teams/bulk", token, new { OrderLineIds = new[] { id }, TeamIds = new[] { S0.TSew02.TeamId } }));

            AssertRaceNoServerError(results);
            var teams = await TeamIdsAsync(id);
            (teams.Count == 0 || teams.SequenceEqual(new[] { S0.TSew02.TeamId }) || teams.SequenceEqual(new[] { S0.TSew01.TeamId, S0.TSew02.TeamId }.OrderBy(x => x)))
                .Should().BeTrue($"vòng {i}: trạng thái cuối = một thứ tự tuần tự hóa, thực tế [{string.Join(",", teams)}]");
            await using var db = NewDb();
            (await db.OrderLineTeams.AsNoTracking().CountAsync(t => !db.OrderLines.Any(l => l.OrderLineId == t.OrderLineId))).Should().Be(0);
        }
    }

    [Fact]
    public async Task IT_B4_119_Concurrent_Save_Same_New_Marker_Creates_One()
    {
        var token = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (od, _) = await NewOrderAsync();
            var body = new { Markers = new[] { MarkerIn("S1", 1, 6.00m, 1.50m) } };
            var url = M + $"order-markers/{od.OrderId}/products/{B.Pr1.ProductId}";

            var results = await Race2(() => PutAsync(url, token, body), () => PutAsync(url, token, body));

            AllowedOutcomes(results);
            var markers = await MarkersAsync(od.OrderId, B.Pr1.ProductId);
            markers.Should().ContainSingle(m => m.MarkerCode == "S1", $"vòng {i}");
            (await RatiosAsync(markers.Single().ProductMarkerId)).Should().HaveCount(2, "ratio không bị nhân đôi");
        }
    }

    [Fact]
    public async Task IT_B4_132_Concurrent_Create_Same_Order_Material_One_Wins()
    {
        var token = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var (od, _) = await NewOrderAsync();
            object Body() => new { OrderId = od.OrderId, MaterialId = B.Ma2.MaterialId, RequiredQty = 10m, CustomerQty = 0m, Note = (string?)null };

            var results = await Race2(() => PostAsync(A + "order-materials", token, Body()), () => PostAsync(A + "order-materials", token, Body()));

            OneWinner(results, 409);
            (await CountOmAsync(od.OrderId, B.Ma2.MaterialId)).Should().Be(1, $"vòng {i}");
        }
    }

    [Fact]
    public async Task IT_B4_153_Concurrent_Delivery_Schedules_Keep_One_Contiguous_Set()
    {
        var token = await TkAdm();
        var (od, _) = await NewOrderAsync();
        for (var i = 0; i < Rounds; i++)
        {
            var s1 = new[] { Today.AddDays(10), Today.AddDays(20) };
            var s2 = new[] { Today.AddDays(30), Today.AddDays(40), Today.AddDays(50) };
            var url = A + $"order-materials/customer-delivery/{od.OrderId}";

            var results = await Race2(() => PutAsync(url, token, new { DeliveryCount = s1.Length, DeliveryDates = s1 }),
                () => PutAsync(url, token, new { DeliveryCount = s2.Length, DeliveryDates = s2 }));

            AllowedOutcomes(results);
            var rows = await DsdAsync(od.OrderId);
            rows.Select(r => (int)r.SeqNo).Should().Equal(Enumerable.Range(1, rows.Count), $"vòng {i}: SeqNo liên tục 1..n");
            var dates = rows.Select(r => r.DeliveryDate).ToArray();
            (dates.SequenceEqual(s1) || dates.SequenceEqual(s2)).Should().BeTrue($"vòng {i}: đúng một bộ ngày, thực tế {string.Join(",", dates)}");
        }
    }

    [Fact]
    public async Task IT_B4_165_Concurrent_Import_Into_Same_Order_Has_No_Duplicate_Rows()
    {
        var token = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var (od, _) = await NewOrderAsync();
            var rows = new[]
            {
                OmRow(1, B.Ma1.MaterialCode, 100m, 0m), OmRow(2, B.Ma2.MaterialCode, 50m, 50m), OmRow(3, B.Ma3.MaterialCode, 80m, 20m),
            };
            var url = A + $"order-materials/import-data?orderId={od.OrderId}";

            var results = await Race2(() => PostAsync(url, token, rows), () => PostAsync(url, token, rows));

            AllowedOutcomes(results);
            foreach (var m in new[] { B.Ma1, B.Ma2, B.Ma3 }) (await CountOmAsync(od.OrderId, m.MaterialId)).Should().Be(1, $"vòng {i}");
        }
    }

    private static object OmRow(int row, string code, decimal required, decimal customer) =>
        new { RowNumber = row, MaterialCode = code, RequiredQty = (decimal?)required, CustomerQty = (decimal?)customer, Note = (string?)null, ParseError = (string?)null, MaterialName = (string?)null, Unit = (string?)null };
}

