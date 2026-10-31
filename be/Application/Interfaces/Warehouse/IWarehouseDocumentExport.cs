using NX_MTD.Application.DTOs;
using NX_MTD.Application.Common;

namespace NX_MTD.Application.Interfaces;

/// <summary>TH-16: dựng file Excel in được cho một chứng từ kho.</summary>
public interface IWarehouseDocumentExcelWriter
{
    byte[] Write(PrintableDocument document);
}

/// <summary>
/// TH-16: xuất Excel phiếu nhập NVL, phiếu cấp, phiếu xuất hàng. Dựa trên các dịch vụ chi tiết hiện có nên giữ nguyên
/// phạm vi xem của từng vai trò (tổ trưởng chỉ in được phiếu cấp của tổ mình).
/// </summary>
public interface IWarehouseDocumentExportService
{
    Task<Result<ExcelFileDto>> ExportReceiptAsync(int receiptId, CancellationToken ct);
    Task<Result<ExcelFileDto>> ExportIssueAsync(int issueId, int? receivingTeamId, CancellationToken ct);
    Task<Result<ExcelFileDto>> ExportDispatchNoteAsync(int dispatchNoteId, CancellationToken ct);
}
