using NX_MTD.Application.Common;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Tests;

public class AuditTextTests
{
    [Fact]
    public void Insert_NamesObjectAndCode()
    {
        var text = AuditText.Summary("CustomerOrder", 12, AuditActions.Insert, null, """{"OrderCode":"DH-0012","Status":"NEW"}""", null);
        Assert.Equal("Tạo mới đơn hàng DH-0012", text);
    }

    [Fact]
    public void Update_ListsOnlyChangedFieldsInVietnamese()
    {
        var text = AuditText.Summary("CustomerOrder", 12, AuditActions.Update,
            """{"OrderCode":"DH-0012","Status":"NEW","DueDate":"2026-11-30","Note":null}""",
            """{"OrderCode":"DH-0012","Status":"STARTED","DueDate":"2026-12-05","Note":null}""", null);
        Assert.Equal("Cập nhật đơn hàng DH-0012: Trạng thái Mới → Đang sản xuất; Ngày giao 30/11/2026 → 05/12/2026", text);
    }

    [Fact]
    public void Product_ExtraImagesAndMainImage_AreLabelledInVietnamese()
    {
        var text = AuditText.Summary("Product", 3, AuditActions.Update,
            """{"ProductCode":"AO-01","SampleImageUrl":null,"ExtraImages":0}""",
            """{"ProductCode":"AO-01","SampleImageUrl":"PRODUCT/2026/11/a.jpg","ExtraImages":5}""", null);
        Assert.Equal("Cập nhật mã hàng AO-01: Ảnh chính (trống) → PRODUCT/2026/11/a.jpg; Số ảnh phụ 0 → 5", text);
    }

    [Fact]
    public void NoCode_FallsBackToId_ReasonAppended()
    {
        var text = AuditText.Summary("PurchaseOrder", 7, AuditActions.Reject, null, """{"GoodsStatus":"REJECTED"}""", "Sai giá");
        Assert.Equal("Từ chối đơn mua NVL #7 (lý do: Sai giá)", text);
    }

    [Fact]
    public void BrokenJson_StillReturnsGeneralSentence()
    {
        Assert.Equal("Xóa xe #3", AuditText.Summary("Vehicle", 3, AuditActions.Delete, "không phải json", null, null));
    }

    [Fact]
    public void TooManyChanges_AreTruncated()
    {
        var text = AuditText.Summary("Employee", 1, AuditActions.Update, """{"A":"1","B":"1","C":"1","D":"1","E":"1"}""",
            """{"A":"2","B":"2","C":"2","D":"2","E":"2"}""", null);
        Assert.EndsWith("…", text);
    }
}
