using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Services;

namespace Application.Tests;

/// <summary>TH-16: xuất Excel phiếu nhập / cấp / xuất hàng để in. Dùng writer ClosedXML thật để kiểm nội dung file.</summary>
public class WarehouseDocumentExportTests
{
    private readonly IMaterialReceiptService _receipts = Substitute.For<IMaterialReceiptService>();
    private readonly IMaterialIssueService _issues = Substitute.For<IMaterialIssueService>();
    private readonly IDispatchNoteService _dispatches = Substitute.For<IDispatchNoteService>();

    private WarehouseDocumentExportService Service() =>
        new(_receipts, _issues, _dispatches, new ClosedXmlWarehouseDocumentWriter());

    private static IXLWorksheet Open(ExcelFileDto file)
    {
        var workbook = new XLWorkbook(new MemoryStream(file.Content));
        return workbook.Worksheet(1);
    }

    private static string[] TextOf(IXLWorksheet sheet) =>
        sheet.CellsUsed().Select(c => c.GetFormattedString()).ToArray();

    private static MaterialReceiptDto Receipt(string status = "Confirmed") => new(
        MaterialReceiptId: 5, ReceiptCode: "PN-2026-0005", ReceiptType: "New", OrderId: 1, OrderCode: "DH001", Source: "Supplier",
        PurchaseOrderId: 3, PoCode: "PO-003", OriginalReceiptId: null, OriginalReceiptCode: null, ShortageRequestId: null,
        ShortDeliveryRequestId: null, ReceiptDate: new DateOnly(2026, 10, 20), Status: status, ReturnReason: null, Note: "Giao buổi sáng",
        SubmittedAt: new DateTime(2026, 10, 20, 8, 0, 0), ConfirmedAt: null, ConfirmedByName: "Nguyễn Quản Đốc",
        Lines: new[]
        {
            new MaterialReceiptLineDto(1, 10, 7, null, 2, "VAI01", "Vải cotton", "m", "Đen", 15000m, null, 95m),
        });

    [Fact]
    public async Task Receipt_export_contains_header_lines_and_signers_and_a_safe_file_name()
    {
        _receipts.GetByIdAsync(5, default).Returns(Result<MaterialReceiptDto>.Success(Receipt()));

        var result = await Service().ExportReceiptAsync(5, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be("PhieuNhap_PN-2026-0005.xlsx");
        var text = TextOf(Open(result.Data));
        text.Should().Contain(new[]
        {
            "PHIẾU NHẬP NGUYÊN VẬT LIỆU", "PN-2026-0005", "20/10/2026", "Nhà cung cấp", "Đã xác nhận", "DH001", "PO-003",
            "Nguyễn Quản Đốc", "Giao buổi sáng", "VAI01", "Vải cotton", "Đen", "95", "Thủ kho", "Quản đốc",
        });
    }

    [Fact]
    public async Task Receipt_export_hides_optional_rows_that_have_no_value()
    {
        _receipts.GetByIdAsync(5, default).Returns(Result<MaterialReceiptDto>.Success(
            Receipt() with { Note = null, PoCode = null, ConfirmedByName = null }));

        var text = TextOf(Open((await Service().ExportReceiptAsync(5, default)).Data!));

        text.Should().NotContain(new[] { "Ghi chú:", "Đơn mua:", "Người xác nhận:", "Lý do trả lại:" });
    }

    [Fact]
    public async Task Export_returns_the_services_own_failure_so_view_scope_is_not_widened()
    {
        _receipts.GetByIdAsync(404, default).Returns(
            Result<MaterialReceiptDto>.Failure("RECEIPT_NOT_FOUND", "Không thấy phiếu.", ResultKind.NotFound));

        var result = await Service().ExportReceiptAsync(404, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RECEIPT_NOT_FOUND");
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    private static MaterialIssueDto Issue() => new(
        MaterialIssueId: 8, IssueCode: "PC-2026-0008", WarehouseType: "Material", OrderId: 1, OrderCode: "DH001",
        WorkOrderId: 4, WorkOrderCode: "LC-0004", RequestId: null, ReceivingTeamId: 2, ReceivingTeamName: "Tổ cắt",
        IssueDate: new DateOnly(2026, 10, 21), Status: "PendingReceipt", IssuedByName: "Thủ kho A", ReceivedByName: null,
        ReceivedAt: null, RejectReason: null, CreatedAt: new DateTime(2026, 10, 21, 9, 0, 0),
        Lines: new[]
        {
            new MaterialIssueLineDto(1, 2, "VAI01", "Vải cotton", "m", "Đen", null, null, null, null, null, null, 120m, null, null),
            new MaterialIssueLineDto(2, null, null, null, null, null, 9, null, null, "AO01", "Trắng", "M", 30m, 28m, "Rách 2 cái"),
        });

    [Fact]
    public async Task Issue_export_prints_fabric_and_semi_finished_lines()
    {
        _issues.GetByIdAsync(8, null, default).Returns(Result<MaterialIssueDto>.Success(Issue()));

        var result = await Service().ExportIssueAsync(8, null, default);

        result.Data!.FileName.Should().Be("PhieuCap_PC-2026-0008.xlsx");
        TextOf(Open(result.Data)).Should().Contain(new[]
        {
            "PHIẾU CẤP HÀNG CHO TỔ", "Cấp vải", "Tổ cắt", "Chờ tổ nhận", "LC-0004", "Vải cotton", "AO01 / Trắng / M", "28", "Rách 2 cái",
            "Tổ trưởng nhận",
        });
    }

    [Fact]
    public async Task Issue_export_passes_the_callers_team_so_a_leader_cannot_print_another_teams_issue()
    {
        _issues.GetByIdAsync(8, 3, default).Returns(
            Result<MaterialIssueDto>.Failure("ISSUE_NOT_FOUND", "Không thấy phiếu.", ResultKind.NotFound));

        var result = await Service().ExportIssueAsync(8, 3, default);

        result.IsSuccess.Should().BeFalse();
        await _issues.Received(1).GetByIdAsync(8, 3, default);
    }

    [Fact]
    public async Task Dispatch_export_prints_customer_address_lines_and_received_quantities()
    {
        var note = new DispatchNoteDto(2, "PX-2026-0002", 1, "DH001", "Công ty ABC", new DateOnly(2026, 10, 25), "12 Lê Lợi, Hà Nội",
            "Giao giờ hành chính", "PartiallyDelivered", new DateTime(2026, 10, 22), null, "Thủ kho A", new DateTime(2026, 10, 23),
            new DateOnly(2026, 10, 24), new[]
            {
                new DispatchNoteLineDto(1, 9, "AO01", "Trắng", "M", 50, 48),
            });
        _dispatches.GetByIdAsync(2, default).Returns(Result<DispatchNoteDto>.Success(note));

        var result = await Service().ExportDispatchNoteAsync(2, default);

        result.Data!.FileName.Should().Be("PhieuXuat_PX-2026-0002.xlsx");
        TextOf(Open(result.Data)).Should().Contain(new[]
        {
            "PHIẾU XUẤT HÀNG", "Công ty ABC", "12 Lê Lợi, Hà Nội", "Giao một phần", "Thủ kho A", "24/10/2026", "AO01", "50", "48",
            "Người nhận hàng",
        });
    }

    [Fact]
    public async Task File_name_drops_characters_that_are_unsafe_on_disk()
    {
        _receipts.GetByIdAsync(5, default).Returns(Result<MaterialReceiptDto>.Success(Receipt() with { ReceiptCode = "PN/2026:0005" }));

        var result = await Service().ExportReceiptAsync(5, default);

        result.Data!.FileName.Should().Be("PhieuNhap_PN_2026_0005.xlsx");
    }
}
