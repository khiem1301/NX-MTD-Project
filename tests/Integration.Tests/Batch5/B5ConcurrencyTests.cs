using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

/// <summary>IT_B5 — ca đồng thời / fault-injection (connection độc lập, COMMIT, tự dọn): mỗi ca chạy <see cref="B4ConcurrentTestBase.Rounds"/> vòng.</summary>
public sealed class B5ConcurrencyTests(ITestOutputHelper output) : B5ConcurrentTestBase(output)
{
    private async Task<Api[]> Race2(Func<Task<Api>> a, Func<Task<Api>> b) => await RaceAsync(a, b);

    [Fact]
    public async Task IT_B5_016_Duplicate_Supplier_Name_Phone_Email_And_Race()
    {
        var admin = await TkAdm();
        var ok = await PostAsync(A + "suppliers", admin, new { Address = "KCN Tân Tạo", MaterialTypes = "Vải", SupplierName = "Dup-" + Guid.NewGuid().ToString("N")[..8], Phone = "09" + Random.Shared.Next(10_000_000, 99_999_999), Email = NextEmail() });
        ok.Code.Should().Be(201, ok.ToString());

        var byName = await PostAsync(A + "suppliers", admin, new { Address = "KCN Tân Tạo", MaterialTypes = "Vải", SupplierName = B.Su1.SupplierName });
        var byPhone = await PostAsync(A + "suppliers", admin, new { Address = "KCN Tân Tạo", MaterialTypes = "Vải", SupplierName = "P-" + Guid.NewGuid().ToString("N")[..8], Phone = B.Su1.Phone });
        var byEmail = await PostAsync(A + "suppliers", admin, new { Address = "KCN Tân Tạo", MaterialTypes = "Vải", SupplierName = "E-" + Guid.NewGuid().ToString("N")[..8], Email = B.Su1.Email });
        new[] { byName, byPhone, byEmail }.Should().OnlyContain(r => r.Code == 409, Summary([byName, byPhone, byEmail]));

        for (var i = 0; i < Rounds; i++)
        {
            var name = "Race-" + Guid.NewGuid().ToString("N")[..10];
            var results = await Race2(() => PostAsync(A + "suppliers", admin, new { Address = "KCN Tân Tạo", MaterialTypes = "Vải", SupplierName = name }), () => PostAsync(A + "suppliers", admin, new { SupplierName = name }));

            OneWinner(results, 409);
            await using var db = NewDb();
            (await db.Suppliers.AsNoTracking().CountAsync(s => s.SupplierName == name)).Should().Be(1, $"vòng {i}");
        }
    }

    [Fact]
    public async Task IT_B5_046_Concurrent_Creates_Get_Distinct_PoCodes()
    {
        var token = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var tag = "C46-" + Guid.NewGuid().ToString("N")[..8];
            var results = await RaceAsync(Enumerable.Range(0, 5).Select<int, Func<Task<Api>>>(_ => () => PostAsync(PO, token, Mk(note: tag))).ToArray());

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 201 || r.Code == 409, Summary(results));
            var codes = results.Where(IsSuccess).Select(r => B1Json.Str(r.Data, "poCode")).ToList();
            codes.Should().OnlyHaveUniqueItems();
            await using var db = NewDb();
            var pos = await db.PurchaseOrders.AsNoTracking().Where(p => p.Note == tag).ToListAsync();
            pos.Should().HaveCount(codes.Count, $"vòng {i}");
            foreach (var po in pos)
            {
                (await PolsAsync(po.PurchaseOrderId)).Should().HaveCount(1);
                (await PoDsdAsync(po.PurchaseOrderId)).Should().HaveCount(1);
            }
        }
    }

    [Fact]
    public async Task IT_B5_047_Fault_In_Schedule_Insert_Rolls_Back_Create()
    {
        var token = await TkMgr();
        var tag = "C47-" + Guid.NewGuid().ToString("N")[..8];
        var before = await CountPosAsync();

        await WithTriggerAsync("DeliveryScheduleDate", "trg_it_b5_dsd_fault", async () =>
        {
            var r = await PostAsync(PO, token, Mk(2, tag));
            Out.WriteLine($"[?] fault create -> {r.Code}");
            FailedGracefully(r);
        });

        await using var db = NewDb();
        (await db.PurchaseOrders.AsNoTracking().CountAsync(p => p.Note == tag)).Should().Be(0);
        (await CountPosAsync()).Should().Be(before);
    }

    [Fact]
    public async Task IT_B5_063_Concurrent_Place_Same_RowVer_One_Wins()
    {
        var token = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var po = await NewPoAsync(pending: true);
            var lines = await PolsAsync(po.PurchaseOrderId);
            var rv = po.RowVer;
            var prices = new[] { 49_000m, 51_000m };

            var results = await Race2(() => PostAsync($"{TRK}/{po.PurchaseOrderId}/place", token, PlaceBody(rv, lines, prices[0])),
                () => PostAsync($"{TRK}/{po.PurchaseOrderId}/place", token, PlaceBody(rv, lines, prices[1])));

            OneWinner(results, 409);
            var winner = Array.FindIndex(results, IsSuccess);
            (await PolsAsync(po.PurchaseOrderId))[0].UnitPrice.Should().Be(prices[winner], $"vòng {i}");
            var dsd = await PoDsdAsync(po.PurchaseOrderId);
            dsd.Select(d => d.SeqNo).Should().Equal(1, 2);
            (await PoAsync(po.PurchaseOrderId)).GoodsStatus.Should().Be("ORDERED");
        }
    }

    [Fact]
    public async Task IT_B5_072_Fault_In_Schedule_Insert_Rolls_Back_Place()
    {
        var token = await TkMgr();
        var po = await NewPoAsync(pending: true);
        var lines = await PolsAsync(po.PurchaseOrderId);
        var dsdBefore = (await PoDsdAsync(po.PurchaseOrderId)).Select(d => (d.SeqNo, d.DeliveryDate)).ToList();

        await WithTriggerAsync("DeliveryScheduleDate", "trg_it_b5_dsd_fault", async () =>
        {
            var r = await PostAsync($"{TRK}/{po.PurchaseOrderId}/place", token, PlaceBody(po.RowVer, lines, 40_000m, 3));
            Out.WriteLine($"[?] fault place -> {r.Code}");
            FailedGracefully(r);
        });

        var after = await PoAsync(po.PurchaseOrderId);
        after.GoodsStatus.Should().Be(PurchaseOrderStatuses.PendingFinalisation);
        after.RowVer.Should().Equal(po.RowVer);
        (await PolsAsync(po.PurchaseOrderId))[0].UnitPrice.Should().Be(50_000m);
        (await PoDsdAsync(po.PurchaseOrderId)).Select(d => (d.SeqNo, d.DeliveryDate)).Should().Equal(dsdBefore);
    }

    [Fact]
    public async Task IT_B5_080_Concurrent_Place_And_Cancel_One_Wins()
    {
        var token = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var po = await NewPoAsync(pending: true);
            var lines = await PolsAsync(po.PurchaseOrderId);

            var results = await Race2(() => PostAsync($"{TRK}/{po.PurchaseOrderId}/place", token, PlaceBody(po.RowVer, lines, 49_000m)),
                () => PostAsync($"{TRK}/{po.PurchaseOrderId}/cancel", token, Reason(po.RowVer, "NCC hết hàng")));

            OneWinner(results, 409);
            var status = (await PoAsync(po.PurchaseOrderId)).GoodsStatus;
            status.Should().Be(IsSuccess(results[0]) ? "ORDERED" : status, $"vòng {i}");
            status.Should().NotBe(PurchaseOrderStatuses.PendingFinalisation);
        }
    }

    [Fact]
    public async Task IT_B5_088_Concurrent_Reprice_And_Confirm_One_Wins()
    {
        var manager = await TkMgr();
        var accountant = await TkAcc();
        for (var i = 0; i < Rounds; i++)
        {
            var po = await NewPoAsync();
            var line = (await PolsAsync(po.PurchaseOrderId))[0];

            var results = await Race2(
                () => PostAsync($"{TRK}/{po.PurchaseOrderId}/reprice", manager, new Dictionary<string, object?>
                    { ["Note"] = "Giảm", ["Lines"] = new[] { new { PurchaseOrderLineId = line.PurchaseOrderLineId, UnitPrice = 40_000m } }, ["RowVer"] = po.RowVer }),
                () => PostAsync($"{PAY}/{po.PurchaseOrderId}/confirm", accountant, Conf(po.RowVer)));

            OneWinner(results, 409);
            var pays = await PaysAsync(po.PurchaseOrderId);
            var price = (await PolsAsync(po.PurchaseOrderId))[0].UnitPrice;
            if (IsSuccess(results[0])) { price.Should().Be(40_000m); pays.Should().BeEmpty($"vòng {i}"); }
            else { price.Should().Be(48_000m); pays.Should().HaveCount(1, $"vòng {i}"); }
        }
    }

    [Fact]
    public async Task IT_B5_099_Concurrent_Confirm_Creates_Single_Payment()
    {
        var token = await TkAcc();
        for (var i = 0; i < Rounds; i++)
        {
            var po = await NewPoAsync();

            var results = await Race2(() => PostAsync($"{PAY}/{po.PurchaseOrderId}/confirm", token, Conf(po.RowVer)),
                () => PostAsync($"{PAY}/{po.PurchaseOrderId}/confirm", token, Conf(po.RowVer)));

            OneWinner(results, 409);
            (await PaysAsync(po.PurchaseOrderId)).Should().HaveCount(1, $"vòng {i}: không có thanh toán kép");
        }
    }

    [Fact]
    public async Task IT_B5_100_Fault_In_PurchaseOrder_Update_Rolls_Back_Confirm()
    {
        var token = await TkAcc();
        var po = await NewPoAsync();

        await WithTriggerAsync("PurchaseOrder", "trg_it_b5_po_fault", async () =>
        {
            var r = await PostAsync($"{PAY}/{po.PurchaseOrderId}/confirm", token, Conf(po.RowVer));
            Out.WriteLine($"[?] fault confirm -> {r.Code}");
            FailedGracefully(r);
        });

        (await PaysAsync(po.PurchaseOrderId)).Should().BeEmpty("không có PurchasePayment mồ côi");
        var after = await PoAsync(po.PurchaseOrderId);
        after.PaymentStatus.Should().Be("UNPAID");
        (after.PaidAmount, after.PaymentDate, after.PaymentMethod).Should().Be(((decimal?)null, (DateOnly?)null, (string?)null));
    }

    [Fact]
    public async Task IT_B5_108_Concurrent_Payment_Update_One_Wins()
    {
        var token = await TkAcc();
        for (var i = 0; i < Rounds; i++)
        {
            var po = await NewPoAsync(paid: true);
            var pp = (await PaysAsync(po.PurchaseOrderId))[0];
            var amounts = new[] { 3_100_000m, 3_200_000m };

            var results = await Race2(() => PutAsync($"{PAY}/{po.PurchaseOrderId}/payments/{pp.PurchasePaymentId}", token, Upd(po.RowVer, amounts[0])),
                () => PutAsync($"{PAY}/{po.PurchaseOrderId}/payments/{pp.PurchasePaymentId}", token, Upd(po.RowVer, amounts[1])));

            OneWinner(results, 409);
            var winner = Array.FindIndex(results, IsSuccess);
            var pays = await PaysAsync(po.PurchaseOrderId);
            pays.Single().Amount.Should().Be(amounts[winner], $"vòng {i}");
            (await PoAsync(po.PurchaseOrderId)).PaidAmount.Should().Be(pays.Sum(p => p.Amount));
        }
    }

    [Fact]
    public async Task IT_B5_113_Concurrent_Return_And_Payment_Update_One_Wins()
    {
        var token = await TkAcc();
        for (var i = 0; i < Rounds; i++)
        {
            var po = await NewPoAsync(paid: true);
            var pp = (await PaysAsync(po.PurchaseOrderId))[0];

            var results = await Race2(() => PostAsync($"{PAY}/{po.PurchaseOrderId}/return", token, Reason(po.RowVer, "Sai tài khoản")),
                () => PutAsync($"{PAY}/{po.PurchaseOrderId}/payments/{pp.PurchasePaymentId}", token, Upd(po.RowVer, 3_100_000m)));

            OneWinner(results, 409);
            var after = await PoAsync(po.PurchaseOrderId);
            var pays = await PaysAsync(po.PurchaseOrderId);
            if (IsSuccess(results[0])) { after.PaymentStatus.Should().Be("RETURNED"); pays.Single().Amount.Should().Be(3_000_000m); }
            else { after.PaymentStatus.Should().NotBe("RETURNED"); pays.Single().Amount.Should().Be(3_100_000m); }
        }
    }

    [Fact]
    public async Task IT_B5_121_Repeated_Races_Never_500_Or_Leak_Constraint_Names()
    {
        var manager = await TkMgr();
        var accountant = await TkAcc();
        for (var i = 0; i < 10; i++)
        {
            var bad = await PostAsync(PO, manager, new Dictionary<string, object?>(Mk()) { ["Lines"] = new[] { PL(B.Ma1.MaterialId, 0m, 1m) } });
            bad.Code.Should().Be(400, bad.ToString());
            bad.Body.Should().NotContainAny("UQ_", "CK_", "trg_", "SqlException");

            var creates = await RaceAsync(Enumerable.Range(0, 3).Select<int, Func<Task<Api>>>(_ => () => PostAsync(PO, manager, Mk())).ToArray());
            AssertRaceNoServerError(creates);
            creates.Where(IsSuccess).Select(r => B1Json.Str(r.Data, "poCode")).Should().OnlyHaveUniqueItems();

            var pending = await NewPoAsync(pending: true);
            var lines = await PolsAsync(pending.PurchaseOrderId);
            var places = await Race2(() => PostAsync($"{TRK}/{pending.PurchaseOrderId}/place", manager, PlaceBody(pending.RowVer, lines, 49_000m)),
                () => PostAsync($"{TRK}/{pending.PurchaseOrderId}/place", manager, PlaceBody(pending.RowVer, lines, 51_000m)));
            AssertRaceNoServerError(places);

            var ordered = await NewPoAsync();
            var confirms = await Race2(() => PostAsync($"{PAY}/{ordered.PurchaseOrderId}/confirm", accountant, Conf(ordered.RowVer)),
                () => PostAsync($"{PAY}/{ordered.PurchaseOrderId}/confirm", accountant, Conf(ordered.RowVer)));
            AssertRaceNoServerError(confirms);
            (await PaysAsync(ordered.PurchaseOrderId)).Count.Should().BeLessThanOrEqualTo(1);
        }

        await using var db = NewDb();
        (await db.PurchaseOrderLines.AsNoTracking().AnyAsync(l => l.OrderedQty <= 0)).Should().BeFalse();
        var codes = await db.PurchaseOrders.AsNoTracking().Select(p => p.PoCode).ToListAsync();
        codes.Should().OnlyHaveUniqueItems();
    }
}
