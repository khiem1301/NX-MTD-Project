using Integration.Tests.Batch4;
using System.Globalization;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

/// <summary>IT_B5_089–115 (trừ ca đồng thời / fault-injection ở B5ConcurrencyTests) — confirm / sửa lần thanh toán / return.</summary>
public sealed class B5PaymentTests(ITestOutputHelper output) : B5TestBase(output)
{
    private static decimal Dc(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private string Confirm(int id) => $"{PAY}/{id}/confirm";

    private async Task<Api> ConfirmPo2(Dictionary<string, object?> body, string who = "accountant") => await Call(who, Post, Confirm(P.Po2.PurchaseOrderId), body);

    private async Task NoNewPaymentAndSame(Snap4 before)
    {
        (await PaysAsync(P.Po2.PurchaseOrderId)).Should().BeEmpty();
        await Unchanged(before);
    }

    // ------------------------------------------------------------------ confirm

    [Fact]
    public async Task IT_B5_089_Confirm_Full_Payment_Creates_Payment_And_Syncs_Flat_Columns()
    {
        var id = P.Po2.PurchaseOrderId;
        var rv = await RvAsync(id);

        var r = await ConfirmPo2(Conf(rv, actual: 9_600_000m));

        r.Code.Should().Be(200, r.ToString());
        var payments = Arr(r.Data, "payments");
        payments.Should().HaveCount(1);
        B1Json.Prop(payments[0], "canEdit")!.Value.GetBoolean().Should().BeTrue();
        var pays = await PaysAsync(id);
        pays.Should().ContainSingle();
        pays[0].Amount.Should().Be(9_600_000m);
        pays[0].PaymentDate.Should().Be(T);
        pays[0].CreatedAt.Year.Should().BeGreaterThan(2000);
        var po = await PoAsync(id);
        po.PaymentStatus.Should().Be("PAID");
        (po.PaidAmount, po.PaymentDate, po.PaymentNote, po.ActualAmount).Should().Be((9_600_000m, (DateOnly?)T, "UNC 123", (decimal?)9_600_000m));
        po.PaymentMethod.Should().Be(pays[0].PaymentMethod).And.BeOneOf(PaymentMethods.All);
        po.RowVer.Should().NotEqual(rv);
    }

    [Fact]
    public async Task IT_B5_090_Confirm_Partial_Payment()
    {
        var id = P.Po2.PurchaseOrderId;

        var r = await ConfirmPo2(Conf(await RvAsync(id), 5_000_000m));

        r.Code.Should().Be(200, r.ToString());
        var pays = await PaysAsync(id);
        pays.Should().ContainSingle().Which.Amount.Should().Be(5_000_000m);
        var po = await PoAsync(id);
        po.PaymentStatus.Should().BeOneOf("UNPAID", "PARTIALLY_PAID");
        po.PaidAmount.Should().Be(pays.Sum(p => p.Amount));
    }

    [Theory]
    [InlineData("IT_B5_091", "0")]
    [InlineData("IT_B5_091", "-1")]
    public async Task IT_B5_091_Confirm_NonPositive_Amount_Returns_400(string caseId, string amount)
    {
        var before = await Snap5();

        var r = await ConfirmPo2(Conf(await RvAsync(P.Po2.PurchaseOrderId), Dc(amount)));

        Rejected(r, 400);
        await NoNewPaymentAndSame(before);
    }

    [Theory]
    [InlineData("IT_B5_092", "0.01", 1)]
    [InlineData("IT_B5_092", "9999999999999999.99", 2)]
    [InlineData("IT_B5_092", "10000000000000000", 0)]
    [InlineData("IT_B5_092", "100.555", 2)]
    public async Task IT_B5_092_Confirm_Amount_Boundaries(string caseId, string amount, int mode)
    {
        var before = await Snap5();

        var r = await ConfirmPo2(Conf(await RvAsync(P.Po2.PurchaseOrderId), Dc(amount)));

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (mode == 1) r.Code.Should().Be(200, r.ToString());
        else if (mode == 0) { r.Code.Should().Be(400, r.ToString()); await Unchanged(before); }
        else r.Code.Should().BeOneOf(200, 400);
        await using var db = NewDb();
        (await db.PurchasePayments.AsNoTracking().AnyAsync(p => p.Amount <= 0)).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B5_093_Confirm_Over_Total_Is_Consistent()
    {
        var id = P.Po2.PurchaseOrderId;
        var before = await Snap5();

        var r = await ConfirmPo2(Conf(await RvAsync(id), 20_000_000m));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        if (r.Code == 200) (await PoAsync(id)).PaidAmount.Should().Be((await PaysAsync(id)).Sum(p => p.Amount));
        else await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_094", "BankTransfer", 200)]
    [InlineData("IT_B5_094", "Cash", 200)]
    [InlineData("IT_B5_094", "BANKTRANSFER", 0)]
    [InlineData("IT_B5_094", "Crypto", 400)]
    [InlineData("IT_B5_094", "", 400)]
    [InlineData("IT_B5_094", "XXXXXXXXXXXXXXXXXXXXX", 400)]
    public async Task IT_B5_094_PaymentMethod_Values(string caseId, string method, int expected)
    {
        var id = P.Po2.PurchaseOrderId;
        var before = await Snap5();

        var r = await ConfirmPo2(Conf(await RvAsync(id), method: method));

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (expected == 0) r.Code.Should().BeOneOf(200, 400); else r.Code.Should().Be(expected, r.ToString());
        if (r.Code == 200)
        {
            var po = await PoAsync(id);
            var pay = (await PaysAsync(id)).Single();
            po.PaymentMethod.Should().Be(pay.PaymentMethod).And.BeOneOf(PaymentMethods.All);
        }
        else await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_095", "zero")]
    [InlineData("IT_B5_095", "missing")]
    [InlineData("IT_B5_095", "future")]
    [InlineData("IT_B5_095", "beforeOrder")]
    public async Task IT_B5_095_PaymentDate_Boundaries(string caseId, string kind)
    {
        var body = Conf(await RvAsync(P.Po2.PurchaseOrderId));
        switch (kind)
        {
            case "zero": body["PaymentDate"] = "0001-01-01"; break;
            case "missing": body.Remove("PaymentDate"); break;
            case "future": body["PaymentDate"] = T.AddDays(1); break;
            default: body["PaymentDate"] = T.AddDays(-6); break;
        }

        var before = await Snap5();
        var r = await ConfirmPo2(body);

        Rejected(r, 400);
        await using var db = NewDb();
        (await db.PurchasePayments.AsNoTracking().AnyAsync(p => p.PaymentDate == new DateOnly(1, 1, 1))).Should().BeFalse();
        await NoNewPaymentAndSame(before);
    }

    [Theory]
    [InlineData("IT_B5_096", "note500", 200)]
    [InlineData("IT_B5_096", "note501", 400)]
    [InlineData("IT_B5_096", "actualNeg", 400)]
    public async Task IT_B5_096_Note_And_ActualAmount_Boundaries(string caseId, string kind, int expected)
    {
        var rv = await RvAsync(P.Po2.PurchaseOrderId);
        var body = kind switch
        {
            "note500" => Conf(rv, note: new string('ạ', 500)),
            "note501" => Conf(rv, note: new string('ạ', 501)),
            _ => Conf(rv, actual: -1m),
        };
        var before = await Snap5();

        var r = await ConfirmPo2(body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        if (expected == 200) (await PoAsync(P.Po2.PurchaseOrderId)).PaymentNote.Should().HaveLength(500);
        else await NoNewPaymentAndSame(before);
    }

    [Theory]
    [InlineData("IT_B5_097", "stale")]
    [InlineData("IT_B5_097", "missing")]
    public async Task IT_B5_097_Confirm_Stale_Or_Missing_RowVer(string caseId, string kind)
    {
        var stale = await RvAsync(P.Po2.PurchaseOrderId);
        await BumpAsync(P.Po2.PurchaseOrderId);
        var body = Conf(stale);
        if (kind == "missing") body.Remove("RowVer");
        var before = await Snap5();

        var r = await ConfirmPo2(body);

        if (kind == "stale") r.Code.Should().Be(409, r.ToString()); else Rejected(r, 400, 409);
        await NoNewPaymentAndSame(before);
    }

    [Theory]
    [InlineData("IT_B5_098", "unplaced")]
    [InlineData("IT_B5_098", "cancelled")]
    public async Task IT_B5_098_Confirm_Unplaced_Or_Cancelled_Po(string caseId, string kind)
    {
        var po = kind == "unplaced" ? P.Po1 : P.Po4;
        var before = await Snap5();

        var r = await Call("accountant", Post, Confirm(po.PurchaseOrderId), Conf(await RvAsync(po.PurchaseOrderId), 1_000m));

        Rejected(r, 400, 404, 409);
        await Unchanged(before);
    }

    // ------------------------------------------------------------------ sửa lần thanh toán

    [Fact]
    public async Task IT_B5_101_Update_Payment_Within_24h()
    {
        var id = P.Po3.PurchaseOrderId;
        var rv = await RvAsync(id);
        var created = (await PayAsync(P.Pp2.PurchasePaymentId)).CreatedAt;

        var r = await Call("accountant", Put, PayUrl(id, P.Pp2.PurchasePaymentId), Upd(rv));

        r.Code.Should().Be(200, r.ToString());
        var pp = await PayAsync(P.Pp2.PurchasePaymentId);
        pp.Amount.Should().Be(3_500_000m);
        pp.UpdatedAt.Should().NotBeNull();
        pp.EditReason.Should().Be("Nhập sai số tiền");
        pp.CreatedAt.Should().Be(created);
        var po = await PoAsync(id);
        po.PaidAmount.Should().Be(8_500_000m);
        po.RowVer.Should().NotEqual(rv);
    }

    [Fact]
    public async Task IT_B5_102_Update_Payment_After_24h_Is_Rejected()
    {
        var before = await Snap5();

        var r = await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp1.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId)));

        Rejected(r, 400, 409);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_103", 23 * 60 + 59, 200)]
    [InlineData("IT_B5_103", 24 * 60 + 1, 0)]
    public async Task IT_B5_103_24h_Window_Boundary(string caseId, int ageMinutes, int expected)
    {
        var pp = new PurchasePayment
        {
            PurchaseOrderId = P.Po3.PurchaseOrderId, PaymentDate = T, Amount = 1_000_000m, PaymentMethod = PaymentMethods.Cash,
            CreatedAt = Clock.Now.AddMinutes(-ageMinutes),
        };
        Db.PurchasePayments.Add(pp);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        var before = await Snap5();

        var r = await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, pp.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId), 1_200_000m));

        if (expected == 200)
        {
            r.Code.Should().Be(200, r.ToString());
            (await PayAsync(pp.PurchasePaymentId)).Amount.Should().Be(1_200_000m);
        }
        else
        {
            Rejected(r, 400, 409);
            await Unchanged(before);
        }
    }

    [Fact]
    public async Task IT_B5_104_Update_Payment_Of_Another_Po_Is_Rejected()
    {
        var before = await Snap5();

        var r = await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp9.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId)));

        Rejected(r, 400, 404);
        (await PayAsync(P.Pp9.PurchasePaymentId)).Amount.Should().Be(2_000_000m);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_105", "0")]
    [InlineData("IT_B5_105", "-1")]
    public async Task IT_B5_105_Update_NonPositive_Amount_Returns_400(string caseId, string amount)
    {
        var before = await Snap5();

        var r = await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp2.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId), Dc(amount)));

        Rejected(r, 400);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_106", "reasonEmpty", 400)]
    [InlineData("IT_B5_106", "reasonNull", 400)]
    [InlineData("IT_B5_106", "reason500", 200)]
    [InlineData("IT_B5_106", "reason501", 400)]
    [InlineData("IT_B5_106", "note501", 400)]
    public async Task IT_B5_106_Update_Reason_And_Note_Boundaries(string caseId, string kind, int expected)
    {
        var rv = await RvAsync(P.Po3.PurchaseOrderId);
        var body = kind switch
        {
            "reasonEmpty" => Upd(rv, reason: ""),
            "reasonNull" => Upd(rv, reason: null),
            "reason500" => Upd(rv, reason: new string('ạ', 500)),
            "reason501" => Upd(rv, reason: new string('ạ', 501)),
            _ => Upd(rv, note: new string('ạ', 501)),
        };
        var before = await Snap5();

        var r = await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp2.PurchasePaymentId), body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        if (expected == 200) (await PayAsync(P.Pp2.PurchasePaymentId)).EditReason.Should().HaveLength(500);
        else await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_107", "stale")]
    [InlineData("IT_B5_107", "missingRowVer")]
    [InlineData("IT_B5_107", "missingPayment")]
    [InlineData("IT_B5_107", "abc")]
    public async Task IT_B5_107_Update_Invalid_Targets(string caseId, string kind)
    {
        var id = P.Po3.PurchaseOrderId;
        var stale = await RvAsync(id);
        if (kind == "stale") await BumpAsync(id);
        var body = Upd(stale);
        if (kind == "missingRowVer") body.Remove("RowVer");
        var payPart = kind switch { "missingPayment" => (await MissingPayIdAsync()).ToString(), "abc" => "abc", _ => P.Pp2.PurchasePaymentId.ToString() };
        var before = await Snap5();

        var r = await Call("accountant", Put, $"{PAY}/{id}/payments/{payPart}", body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(kind switch { "stale" => 409, "missingRowVer" => r.Code, _ => 404 }, r.ToString());
        if (kind == "missingRowVer") r.Code.Should().BeOneOf(400, 409);
        await Unchanged(before);
    }

    // ------------------------------------------------------------------ return

    [Fact]
    public async Task IT_B5_109_Return_Paid_Po_Marks_Returned_And_Keeps_Payments()
    {
        var id = P.Po2.PurchaseOrderId;
        var rv = await ConfirmPo2Async();

        var r = await Call("accountant", Post, $"{PAY}/{id}/return", Reason(rv, "Sai số tài khoản"));

        r.Code.Should().Be(200, r.ToString());
        var po = await PoAsync(id);
        po.PaymentStatus.Should().Be("RETURNED");
        po.PaymentReturnReason.Should().Be("Sai số tài khoản");
        po.RowVer.Should().NotEqual(rv);
        (await PaysAsync(id)).Should().HaveCount(1);
    }

    [Theory]
    [InlineData("IT_B5_110", "empty", 400)]
    [InlineData("IT_B5_110", "null", 400)]
    [InlineData("IT_B5_110", "r500", 200)]
    [InlineData("IT_B5_110", "r501", 400)]
    public async Task IT_B5_110_Return_Reason_Boundaries(string caseId, string kind, int expected)
    {
        var rv = await ConfirmPo2Async();
        var reason = kind switch { "empty" => "", "null" => null, "r500" => new string('ạ', 500), _ => new string('ạ', 501) };
        var before = await Snap5();

        var r = await Call("accountant", Post, $"{PAY}/{P.Po2.PurchaseOrderId}/return", Reason(rv, reason));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        if (expected == 200) (await PoAsync(P.Po2.PurchaseOrderId)).PaymentReturnReason.Should().HaveLength(500);
        else await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_111_Return_Unpaid_Po_Is_Rejected()
    {
        var before = await Snap5();

        var r = await Call("accountant", Post, $"{PAY}/{P.Po2.PurchaseOrderId}/return", Reason(await RvAsync(P.Po2.PurchaseOrderId), "Trả"));

        Rejected(r, 400, 409);
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_112_Return_Stale_RowVer_Or_Missing_Id()
    {
        var rv = await ConfirmPo2Async();
        await BumpAsync(P.Po2.PurchaseOrderId);
        var before = await Snap5();

        var a = await Call("accountant", Post, $"{PAY}/{P.Po2.PurchaseOrderId}/return", Reason(rv, "Trả"));
        var b = await Call("accountant", Post, $"{PAY}/{await MissingPoIdAsync()}/return", Reason(rv, "Trả"));

        a.Code.Should().Be(409, a.ToString());
        b.Code.Should().Be(404, b.ToString());
        await Unchanged(before);
    }

    // ------------------------------------------------------------------ đọc

    [Fact]
    public async Task IT_B5_114_Detail_Lists_Payments_With_CanEdit()
    {
        var id = P.Po3.PurchaseOrderId;
        var before = await Snap5();

        var r = await Call("accountant", Get, $"{PAY}/{id}");

        r.Code.Should().Be(200, r.ToString());
        var pays = Arr(r.Data, "payments");
        pays.Should().HaveCount(2);
        pays.Single(p => B1Json.Int(p, "purchasePaymentId") == P.Pp1.PurchasePaymentId).Let(p => B1Json.Prop(p, "canEdit")!.Value.GetBoolean().Should().BeFalse());
        pays.Single(p => B1Json.Int(p, "purchasePaymentId") == P.Pp2.PurchasePaymentId).Let(p => B1Json.Prop(p, "canEdit")!.Value.GetBoolean().Should().BeTrue());
        var db = await PaysAsync(id);
        B1Json.Dec(r.Data, "paidAmount").Should().Be(db.Sum(p => p.Amount));
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_115", "Paid")]
    [InlineData("IT_B5_115", "PAID")]
    [InlineData("IT_B5_115", "Xyz")]
    public async Task IT_B5_115_PaymentStatus_Filter(string caseId, string status)
    {
        var before = await Snap5();

        var r = await Call("accountant", Get, $"{PAY}?paymentStatus={status}&pageSize=100");

        r.ShouldNotBeServerError();
        if (status == "Paid")
        {
            r.Code.Should().Be(200, r.ToString());
            var ids = IdsOf(r);
            ids.Should().Contain(P.Po6.PurchaseOrderId);
            await using var db = NewDb();
            (await db.PurchaseOrders.AsNoTracking().Where(p => ids.Contains(p.PurchaseOrderId)).Select(p => p.PaymentStatus).ToListAsync()).Should().OnlyContain(s => s == "PAID");
        }
        else r.Code.Should().BeOneOf(200, 400);
        await Unchanged(before);
    }
}
