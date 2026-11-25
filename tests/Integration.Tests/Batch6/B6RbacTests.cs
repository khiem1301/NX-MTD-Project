using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

public sealed class B6RbacTests(ITestOutputHelper output) : B6TestBase(output)
{
    private string AttList(int id) => $"{ATT}?ownerType={AttachmentOwnerTypes.MaterialReceipt}&ownerId={id}";

    [Fact]
    public async Task IT_B6_001_Anonymous_Gets_401_On_All_Prefixes()
    {
        var before = await Snap6();
        foreach (var url in new[] { MRM, INC, SDM, AttList(R.Mr1.MaterialReceiptId) })
        {
            var (res, json) = await SendAsAsync(null, Get, url);
            using (res) await AssertErrorAsync(res, 401);
            if (json is { } j) B1Json.Bool(j, "success").Should().NotBe(true);
        }

        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_002", "expired")]
    [InlineData("IT_B6_002", "forged")]
    public async Task IT_B6_002_Expired_Or_Forged_Token_Gets_401(string caseId, string kind)
    {
        var token = kind == "expired" ? await TkExpired() : await TkForged();
        var before = await Snap6();

        var r = await SendAsync(Post, $"{MRM}/{R.Mr1.MaterialReceiptId}/confirm", token, null);

        r.Code.Should().Be(401, r.ToString());
        (await MrAsync(R.Mr1.MaterialReceiptId))!.Status.Should().Be(ReceiptStatuses.Pending);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_003_Admin_Cannot_Use_WarehouseKeeper_Prefix_Reads() =>
        await Deny6("admin", [403], (Get, MRW, null), (Get, INC, null), (Get, SDW, null));

    [Fact]
    public async Task IT_B6_004_Admin_Cannot_Use_WarehouseKeeper_Prefix_Writes()
    {
        await Deny6("admin", [403],
            (Post, MRW, CustBody()), (Post, MRW + "/supplementary", SuppBody()), (Put, $"{MRW}/{R.Mr1.MaterialReceiptId}", CustBody()));
    }

    [Fact]
    public async Task IT_B6_005_Manager_Cannot_Use_WarehouseKeeper_Prefix()
    {
        await Deny6("manager", [403], (Get, MRW, null), (Get, INC, null), (Get, SDW, null));
        await Deny6("manager", [403],
            (Post, MRW, CustBody()), (Post, MRW + "/supplementary", SuppBody()), (Put, $"{MRW}/{R.Mr1.MaterialReceiptId}", CustBody()));
    }

    [Fact]
    public async Task IT_B6_006_WarehouseKeeper_Cannot_Use_Manager_Prefix()
    {
        await Deny6("warehouse", [403],
            (Post, $"{MRM}/{R.Mr1.MaterialReceiptId}/confirm", null), (Post, $"{MRM}/{R.Mr1.MaterialReceiptId}/return", new { Reason = "x" }),
            (Post, $"{SDM}/{R.Sd1.RequestId}/resolve", ResolveBody()), (Get, MRM, null));
        (await MrAsync(R.Mr1.MaterialReceiptId))!.Status.Should().Be(ReceiptStatuses.Pending);
    }

    [Fact]
    public async Task IT_B6_007_WarehouseKeeper_Has_No_Confirm_Or_Resolve_Route() =>
        await Deny6("warehouse", [404, 405],
            (Post, $"{MRW}/{R.Mr1.MaterialReceiptId}/confirm", null), (Post, $"{SDW}/{R.Sd1.RequestId}/resolve", ResolveBody()));

    [Fact]
    public async Task IT_B6_008_Admin_Can_Use_Manager_Prefix_And_Confirms_As_Admin()
    {
        var list = await Call("admin", Get, MRM);
        list.Code.Should().Be(200, list.ToString());

        var r = await Confirm(R.Mr1.MaterialReceiptId, "admin");

        r.Code.Should().Be(200, r.ToString());
        var mr = (await MrAsync(R.Mr1.MaterialReceiptId))!;
        mr.Status.Should().Be(ReceiptStatuses.Confirmed);
        mr.ConfirmedBy.Should().Be(S0.Adm.EmployeeId);
    }

    [Theory]
    [InlineData("IT_B6_009", "accountant")]
    [InlineData("IT_B6_009", "teamleader")]
    [InlineData("IT_B6_009", "worker")]
    public async Task IT_B6_009_Other_Roles_Get_403(string caseId, string who) =>
        await Deny6(who, [403], (Get, MRM, null), (Get, MRW, null), (Get, SDM, null), (Get, INC, null));

    [Theory]
    [InlineData("IT_B6_010", "accountant")]
    [InlineData("IT_B6_010", "teamleader")]
    [InlineData("IT_B6_010", "worker")]
    public async Task IT_B6_010_Attachments_List_Open_To_Any_Authenticated_Role(string caseId, string who)
    {
        var before = await Snap6();

        var r = await Call(who, Get, AttList(R.Mr1.MaterialReceiptId));

        r.Code.Should().Be(200, r.ToString());
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_011_No_Delete_Route_For_Receipts()
    {
        var id = R.Mr1.MaterialReceiptId;
        await Deny6("warehouse", [404, 405], (Del, $"{MRW}/{id}", null));
        await Deny6("manager", [404, 405], (Del, $"{MRM}/{id}", null));
        (await MrAsync(id)).Should().NotBeNull();
        (await MrlAsync(id)).Should().HaveCount(1);
    }

    [Fact]
    public async Task IT_B6_012_Route_Casing_And_Kebab_Variants()
    {
        var before = await Snap6();

        var upper = await Call("manager", Get, "/api/v1/MANAGER/MATERIAL-RECEIPTS");
        var kebab = await Call("manager", Get, "/api/v1/manager/MaterialReceipts");

        upper.Code.Should().Be(200, upper.ToString());
        kebab.Code.Should().BeOneOf(200, 404);
        Note($"PascalCase route -> {kebab.Code}");
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_094_Admin_Cannot_Export_Excel_On_WarehouseKeeper_Prefix() =>
        await Deny6("admin", [403], (Get, $"{MRW}/{R.Mr1.MaterialReceiptId}/excel", null));
}
