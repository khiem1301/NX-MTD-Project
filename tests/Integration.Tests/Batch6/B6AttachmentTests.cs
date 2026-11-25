using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_100–118 — C/Attachments: upload, IDOR, xóa mềm.</summary>
public sealed class B6AttachmentTests(ITestOutputHelper output) : B6TestBase(output)
{
    private const string Mrt = AttachmentOwnerTypes.MaterialReceipt;

    private async Task<Api> Up(string? who, string ownerType, object ownerId, string? purpose, byte[]? content, string name = "img_ok.jpg", string ct = "image/jpeg") =>
        await Upload(who, ownerType, ownerId, purpose, content, name, ct);

    private static string AttUrl(long id) => $"{ATT}/{id}";

    [Fact]
    public async Task IT_B6_100_Upload_Valid_Photo_Creates_Current_Attachment()
    {
        var r = await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, "Photo", Bytes(1_048_576));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "ownerType")!.Replace("_", "").Should().BeEquivalentTo(Mrt.Replace("_", ""));
        B1Json.Int(r.Data, "ownerId").Should().Be(R.Mr2.MaterialReceiptId);
        B1Json.Str(r.Data, "fileName").Should().Be("img_ok.jpg");
        B1Json.Str(r.Data, "contentType").Should().Be("image/jpeg");
        B1Json.Prop(r.Data, "fileUrl").Should().BeNull("không lộ FileUrl");
        var a = await AttByIdAsync(B1Json.Prop(r.Data, "attachmentId")!.Value.GetInt64());
        a.IsCurrent.Should().BeTrue();
        a.RemovedAt.Should().BeNull();
        a.FileUrl.Should().NotBeNullOrWhiteSpace();
        NearVnNow(a.UploadedAt, "UploadedAt");
    }

    [Fact]
    public async Task IT_B6_101_Upload_Without_File_Returns_400()
    {
        var before = await Snap6();

        var r = await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, "PHOTO", null);

        Graceful(r, 400);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_102_Upload_Empty_File_Returns_400()
    {
        var before = await Snap6();

        var r = await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, "PHOTO", [], "zero.jpg");

        Graceful(r, 400);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_103", 5_241_856)]
    [InlineData("IT_B6_103", 5_242_881)]
    public async Task IT_B6_103_Five_Megabyte_Boundary(string caseId, int size)
    {
        var before = await Snap6();

        var r = await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, "PHOTO", Bytes(size));

        if (size < 5 * 1024 * 1024) r.Code.Should().Be(200, r.ToString());
        else
        {
            Graceful(r, 400);
            await Unchanged6(before);
        }
    }

    [Fact]
    public async Task IT_B6_104_Over_Request_Size_Limit_Is_Rejected_Not_500()
    {
        var before = await Snap6();

        var r = await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, "PHOTO", Bytes(6 * 1024 * 1024 + 1));

        Graceful(r, 400, 413);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_105", "exe")]
    [InlineData("IT_B6_105", "txt")]
    public async Task IT_B6_105_Non_Image_Content_Is_Rejected(string caseId, string kind)
    {
        var before = await Snap6();
        var bytes = kind == "exe" ? new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00 } : "ghi chú"u8.ToArray();

        var r = kind == "exe"
            ? await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, "PHOTO", bytes, "fake.jpg", "image/jpeg")
            : await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, "PHOTO", bytes, "note.txt", "text/plain");

        Graceful(r, 400);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_106", "Xyz")]
    [InlineData("IT_B6_106", "")]
    [InlineData("IT_B6_106", "ABCDEFGHIJKLMNOPQRSTUVWXYZABCDE")]
    public async Task IT_B6_106_Invalid_OwnerType_Returns_400(string caseId, string ownerType)
    {
        var before = await Snap6();

        var r = await Up("warehouse", ownerType, R.Mr2.MaterialReceiptId, "PHOTO", Bytes(2048));

        Graceful(r, 400);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_107", "missing")]
    [InlineData("IT_B6_107", "0")]
    [InlineData("IT_B6_107", "-1")]
    [InlineData("IT_B6_107", "abc")]
    public async Task IT_B6_107_Invalid_OwnerId_Is_Rejected(string caseId, string kind)
    {
        var before = await Snap6();
        object ownerId = kind == "missing" ? await MissingMrIdAsync() : kind;

        var r = await Up("warehouse", Mrt, ownerId, "PHOTO", Bytes(2048));

        if (kind == "missing") Graceful(r, 400, 404); else Graceful(r, 400);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_108", "Photo")]
    [InlineData("IT_B6_108", "none")]
    [InlineData("IT_B6_108", "PHOTO")]
    [InlineData("IT_B6_108", "long")]
    public async Task IT_B6_108_Purpose_Is_Stored_Consistently(string caseId, string kind)
    {
        var purpose = kind switch { "none" => null, "long" => new string('P', 31), _ => kind };
        var before = await Snap6();

        var r = await Up("warehouse", Mrt, R.Mr2.MaterialReceiptId, purpose, Bytes(2048));

        if (kind == "long")
        {
            Graceful(r, 400);
            await Unchanged6(before);
            return;
        }

        r.Code.Should().Be(200, r.ToString());
        var saved = (await AttByIdAsync(B1Json.Prop(r.Data, "attachmentId")!.Value.GetInt64())).Purpose;
        Note($"purpose gửi '{purpose}' -> DB '{saved}'");
        saved.Should().Be(AttachmentPurposes.Photo, "DB viết HOA theo default PHOTO");
    }

    [Fact]
    public async Task IT_B6_109_New_Upload_Retires_Previous_Current_Photo()
    {
        var first = await SeedUploadAsync(Mrt, R.Mr1.MaterialReceiptId);

        var r = await Up("warehouse", Mrt, R.Mr1.MaterialReceiptId, "PHOTO", Bytes(4096));

        r.Code.Should().Be(200, r.ToString());
        var second = B1Json.Prop(r.Data, "attachmentId")!.Value.GetInt64();
        (await AttByIdAsync(second)).IsCurrent.Should().BeTrue();
        var old = await AttByIdAsync(first);
        old.IsCurrent.Should().BeFalse();
        NearVnNow(old.RemovedAt, "RemovedAt");
    }

    [Fact]
    public async Task IT_B6_110_List_Returns_Current_Only()
    {
        var at1 = await SeedUploadAsync(Mrt, R.Mr1.MaterialReceiptId);
        var before = await Snap6();

        var r = await Call("warehouse", Get, $"{ATT}?ownerType={Mrt}&ownerId={R.Mr1.MaterialReceiptId}");

        r.Code.Should().Be(200, r.ToString());
        var ids = r.Items().Select(x => B1Json.Prop(x, "attachmentId")!.Value.GetInt64()).ToList();
        ids.Should().Contain(at1).And.NotContain(R.At3.AttachmentId);
        var expected = (await AttAsync(Mrt, R.Mr1.MaterialReceiptId)).Where(a => a.IsCurrent).Select(a => a.AttachmentId);
        ids.Should().BeEquivalentTo(expected);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_111_Content_Returns_File_With_Private_Cache()
    {
        var bytes = Bytes(3072);
        var up = await Up("warehouse", Mrt, R.Mr1.MaterialReceiptId, "PHOTO", bytes);
        var id = B1Json.Prop(up.Data, "attachmentId")!.Value.GetInt64();

        var res = await RawAsync($"{AttUrl(id)}/content", "warehouse");

        res.Code.Should().Be(200);
        res.ContentType.Should().Be("image/jpeg");
        res.CacheControl.Should().Contain("private").And.Contain("max-age=3600");
        res.Bytes.Should().Equal(bytes);
    }

    [Theory]
    [InlineData("IT_B6_112", "missing")]
    [InlineData("IT_B6_112", "abc")]
    [InlineData("IT_B6_112", "removed")]
    public async Task IT_B6_112_Content_Of_Unknown_Or_Removed_Attachment(string caseId, string kind)
    {
        var id = kind switch { "missing" => "999999999999", "abc" => "abc", _ => R.At3.AttachmentId.ToString() };

        var r = await Call("warehouse", Get, $"{ATT}/{id}/content");

        if (kind == "removed") Graceful(r, 200, 404); else Graceful(r, 404);
        if (r.Code == 404 && kind != "abc") r.IsJson.Should().BeTrue("lỗi trả JSON, không trả file rỗng");
    }

    [Fact]
    public async Task IT_B6_113_Worker_Cannot_Read_Payment_Proof_Of_Purchase_Order()
    {
        var before = await Snap6();

        var list = await Call("worker", Get, $"{ATT}?ownerType={AttachmentOwnerTypes.PurchaseOrder}&ownerId={P.Po3.PurchaseOrderId}");
        var content = await Call("worker", Get, $"{AttUrl(R.At2.AttachmentId)}/content");

        Graceful(list, 403, 404);
        Graceful(content, 403, 404);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_114_Other_Roles_Cannot_Delete_Attachments()
    {
        var at1 = await SeedUploadAsync(Mrt, R.Mr1.MaterialReceiptId);
        var before = await Snap6();

        var a = await Call("worker", Del, AttUrl(at1));
        var b = await Call("teamleader", Del, AttUrl(R.At2.AttachmentId));

        Graceful(a, 403, 404);
        Graceful(b, 403, 404);
        (await AttByIdAsync(at1)).IsCurrent.Should().BeTrue();
        (await AttByIdAsync(R.At2.AttachmentId)).IsCurrent.Should().BeTrue();
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_115_Worker_Cannot_Attach_To_Confirmed_Receipt()
    {
        var before = await Snap6();

        var r = await Up("worker", Mrt, R.Mr3.MaterialReceiptId, "PHOTO", Bytes(2048));

        Graceful(r, 400, 403);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_116_Delete_Is_Soft_Delete()
    {
        var at1 = await SeedUploadAsync(Mrt, R.Mr1.MaterialReceiptId);

        var r = await Call("warehouse", Del, AttUrl(at1));

        r.Code.Should().Be(200, r.ToString());
        var a = await AttByIdAsync(at1);
        a.IsCurrent.Should().BeFalse();
        NearVnNow(a.RemovedAt, "RemovedAt");
    }

    [Theory]
    [InlineData("IT_B6_117", "again")]
    [InlineData("IT_B6_117", "missing")]
    [InlineData("IT_B6_117", "abc")]
    public async Task IT_B6_117_Delete_Removed_Or_Unknown_Attachment(string caseId, string kind)
    {
        var at1 = await SeedUploadAsync(Mrt, R.Mr1.MaterialReceiptId);
        (await Call("warehouse", Del, AttUrl(at1))).Code.Should().Be(200);
        var removedAt = (await AttByIdAsync(at1)).RemovedAt;
        var url = kind switch { "again" => AttUrl(at1), "missing" => $"{ATT}/999999999999", _ => $"{ATT}/abc" };

        var r = await Call("warehouse", Del, url);

        if (kind == "again") Graceful(r, 404, 409); else Graceful(r, 404);
        (await AttByIdAsync(at1)).RemovedAt.Should().Be(removedAt, "RemovedAt không bị ghi đè");
    }

    [Theory]
    [InlineData("IT_B6_118", "9223372036854775807")]
    [InlineData("IT_B6_118", "9223372036854775808")]
    public async Task IT_B6_118_Long_Id_Boundaries_Return_404(string caseId, string id)
    {
        var before = await Snap6();

        var r = await Call("warehouse", Get, $"{ATT}/{id}/content");

        Graceful(r, 404);
        await Unchanged6(before);
    }
}
