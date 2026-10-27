using System.Text.RegularExpressions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Application.Services;

/// <summary>
/// TH-16: xuất Excel phiếu nhập NVL (UC38), phiếu cấp (UC40) và phiếu xuất hàng (UC43). Lấy dữ liệu qua đúng dịch vụ chi tiết
/// đã có nên quyền xem không bị nới: phiếu không tồn tại hoặc không thuộc phạm vi người xem thì trả đúng lỗi của dịch vụ đó.
/// </summary>
public sealed class WarehouseDocumentExportService : IWarehouseDocumentExportService
{
    private static readonly Regex UnsafeFileChars = new(@"[^\w\-]+", RegexOptions.Compiled);

    private readonly IMaterialReceiptService _receipts;
    private readonly IMaterialIssueService _issues;
    private readonly IDispatchNoteService _dispatches;
    private readonly IWarehouseDocumentExcelWriter _writer;

    public WarehouseDocumentExportService(IMaterialReceiptService receipts, IMaterialIssueService issues,
        IDispatchNoteService dispatches, IWarehouseDocumentExcelWriter writer)
    {
        _receipts = receipts; _issues = issues; _dispatches = dispatches; _writer = writer;
    }

    public async Task<Result<ExcelFileDto>> ExportReceiptAsync(int receiptId, CancellationToken ct)
    {
        var found = await _receipts.GetByIdAsync(receiptId, ct);
        if (!found.IsSuccess) return Fail(found.ErrorCode!, found.Message!, found.Kind);
        var r = found.Data!;

        var info = new List<KeyValuePair<string, string>>
        {
            Pair("Mã phiếu", r.ReceiptCode),
            Pair("Ngày nhập", Date(r.ReceiptDate)),
            Pair("Nguồn", Norm(r.Source) == "SUPPLIER" ? "Nhà cung cấp" : "Khách cấp"),
            Pair("Loại phiếu", Norm(r.ReceiptType) == "SUPPLEMENTARY" ? "Phiếu bổ sung" : "Phiếu nhập mới"),
            Pair("Trạng thái", ReceiptStatus(r.Status)),
        };
        AddIf(info, "Đơn hàng", r.OrderCode);
        AddIf(info, "Đơn mua", r.PoCode);
        AddIf(info, "Phiếu gốc", r.OriginalReceiptCode);
        AddIf(info, "Người xác nhận", r.ConfirmedByName);
        AddIf(info, "Lý do trả lại", r.ReturnReason);
        AddIf(info, "Ghi chú", r.Note);

        var rows = r.Lines.Select((l, i) => (IReadOnlyList<object?>)new object?[]
        {
            i + 1, l.MaterialCode, l.MaterialName, l.Unit, l.MaterialColor, l.AcceptedQty, l.UnitPrice,
        }).ToList();
        var doc = new PrintableDocument("PHIẾU NHẬP NGUYÊN VẬT LIỆU", r.ReceiptCode, info,
            new[] { "STT", "Mã NVL", "Tên NVL", "ĐVT", "Màu", "SL đạt", "Đơn giá" }, rows,
            new[] { "Người lập phiếu", "Thủ kho", "Quản đốc" });
        return Build(doc, "PhieuNhap");
    }

    public async Task<Result<ExcelFileDto>> ExportIssueAsync(int issueId, int? receivingTeamId, CancellationToken ct)
    {
        var found = await _issues.GetByIdAsync(issueId, receivingTeamId, ct);
        if (!found.IsSuccess) return Fail(found.ErrorCode!, found.Message!, found.Kind);
        var i = found.Data!;

        var info = new List<KeyValuePair<string, string>>
        {
            Pair("Mã phiếu", i.IssueCode),
            Pair("Ngày cấp", Date(i.IssueDate)),
            Pair("Loại", Norm(i.WarehouseType) == "MATERIAL" ? "Cấp vải" : "Cấp bán thành phẩm"),
            Pair("Đơn hàng", i.OrderCode),
            Pair("Tổ nhận", i.ReceivingTeamName),
            Pair("Người cấp", i.IssuedByName),
            Pair("Trạng thái", IssueStatus(i.Status)),
        };
        AddIf(info, "Lệnh cắt", i.WorkOrderCode);
        AddIf(info, "Người nhận", i.ReceivedByName);
        AddIf(info, "Lý do từ chối", i.RejectReason);

        var rows = i.Lines.Select((l, n) => (IReadOnlyList<object?>)new object?[]
        {
            n + 1,
            l.MaterialCode ?? l.ProductCode,
            l.MaterialName ?? string.Join(" / ", new[] { l.ProductCode, l.Color, l.SizeCode }.Where(s => !string.IsNullOrEmpty(s))),
            l.Unit ?? "cái", l.MaterialColor ?? l.Color, l.IssuedQty, l.ConfirmedQty, l.DiscrepancyReason,
        }).ToList();
        var doc = new PrintableDocument("PHIẾU CẤP HÀNG CHO TỔ", i.IssueCode, info,
            new[] { "STT", "Mã", "Tên hàng", "ĐVT", "Màu", "SL cấp", "SL tổ nhận", "Lý do lệch" }, rows,
            new[] { "Người lập phiếu", "Thủ kho", "Tổ trưởng nhận" });
        return Build(doc, "PhieuCap");
    }

    public async Task<Result<ExcelFileDto>> ExportDispatchNoteAsync(int dispatchNoteId, CancellationToken ct)
    {
        var found = await _dispatches.GetByIdAsync(dispatchNoteId, ct);
        if (!found.IsSuccess) return Fail(found.ErrorCode!, found.Message!, found.Kind);
        var d = found.Data!;

        var info = new List<KeyValuePair<string, string>>
        {
            Pair("Mã phiếu", d.DispatchCode),
            Pair("Đơn hàng", d.OrderCode),
            Pair("Khách hàng", d.CustomerName),
            Pair("Ngày giao dự kiến", Date(d.DeliveryDate)),
            Pair("Địa chỉ giao", d.DeliveryAddress),
            Pair("Trạng thái", DispatchStatus(d.Status)),
        };
        AddIf(info, "Người xuất kho", d.DispatchedByName);
        AddIf(info, "Khách nhận ngày", d.CustomerReceivedDate is { } received ? Date(received) : null);
        AddIf(info, "Lý do từ chối", d.RejectReason);
        AddIf(info, "Ghi chú", d.Note);

        var rows = d.Lines.Select((l, n) => (IReadOnlyList<object?>)new object?[]
            { n + 1, l.ProductCode, l.Color, l.SizeCode, l.IssueQty, l.ReceivedQty }).ToList();
        var doc = new PrintableDocument("PHIẾU XUẤT HÀNG", d.DispatchCode, info,
            new[] { "STT", "Mã hàng", "Màu", "Size", "SL xuất", "SL khách nhận" }, rows,
            new[] { "Quản đốc", "Thủ kho", "Người nhận hàng" });
        return Build(doc, "PhieuXuat");
    }

    private Result<ExcelFileDto> Build(PrintableDocument doc, string prefix)
    {
        var name = $"{prefix}_{UnsafeFileChars.Replace(doc.Code, "_")}.xlsx";
        return Result<ExcelFileDto>.Success(new ExcelFileDto(_writer.Write(doc), name), Messages.ExcelExported);
    }

    private static Result<ExcelFileDto> Fail(string code, string message, ResultKind kind) =>
        Result<ExcelFileDto>.Failure(code, message, kind);

    private static KeyValuePair<string, string> Pair(string label, string value) => new(label, value);

    private static void AddIf(List<KeyValuePair<string, string>> info, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) info.Add(Pair(label, value));
    }

    private static string Date(DateOnly date) => date.ToString("dd/MM/yyyy");

    /// <summary>API trả mã dạng PascalCase, DB dạng UPPER_SNAKE: so khớp không phân biệt hai kiểu viết.</summary>
    private static string Norm(string code) => code.Replace("_", "").ToUpperInvariant();

    private static string ReceiptStatus(string s) => Norm(s) switch
    {
        "PENDING" => "Chờ xác nhận", "CONFIRMED" => "Đã xác nhận", "RETURNED" => "Bị trả lại", _ => s,
    };

    private static string IssueStatus(string s) => Norm(s) switch
    {
        "PENDINGRECEIPT" => "Chờ tổ nhận", "RECEIVED" => "Tổ đã nhận", "REJECTED" => "Tổ từ chối", _ => s,
    };

    private static string DispatchStatus(string s) => Norm(s) switch
    {
        "PENDING" => "Chờ xuất kho", "REJECTED" => "Thủ kho từ chối", "CANCELLED" => "Đã hủy",
        "INDELIVERY" => "Đang giao", "DELIVERED" => "Đã giao", "PARTIALLYDELIVERED" => "Giao một phần", _ => s,
    };
}
