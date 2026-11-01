using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>MI-12 — Chi phí tháng (UC12, UC33): Kế toán tải file mẫu, xem trước rồi nhập Excel chi phí
/// 10 khoản (FIXED/VARIABLE); import lại cùng tháng thay toàn bộ số cũ. Chặn khi tháng đã khóa (MSG21).</summary>
public interface IMonthlyCostService
{
    /// <summary>Tải file Excel mẫu để điền chi phí tháng (MSG27).</summary>
    Task<Result<ExcelFileDto>> ExportTemplateAsync(CancellationToken ct);

    /// <summary>Xem trước file Excel chi phí tháng (chưa ghi DB) — MSG24, kèm đối soát MEALS nếu có.</summary>
    Task<Result<MonthlyCostExcelPreviewDto>> PreviewImportAsync(Stream stream, string periodMonth, CancellationToken ct);

    /// <summary>Nhập chi phí tháng từ Excel: thay toàn bộ dòng cũ của tháng bằng các dòng hợp lệ (MSG24).</summary>
    Task<Result<MonthlyCostImportResultDto>> ImportAsync(Stream stream, string periodMonth, CancellationToken ct);

    /// <summary>Chi phí đã nhập của một tháng, kèm đối soát MEALS.</summary>
    Task<Result<MonthlyCostSummaryDto>> GetByPeriodMonthAsync(string periodMonth, CancellationToken ct);
}
