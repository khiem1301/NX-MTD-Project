using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// MI-12 — Chi phí tháng (UC12, UC33). Kế toán import Excel 10 khoản chi phí (FIXED/VARIABLE); import
/// lại cùng tháng thì xóa và ghi lại toàn bộ dòng của tháng đó (DEV-MI-12-1 — schema không có cột
/// versioning nên không giữ "lịch sử" như SRS mô tả, xem docs/CODE_DEVIATIONS.md). Chặn khi tháng đã
/// khóa bằng pre-check (cùng kiểu PayrollService.ImportAsync) thay vì chỉ dựa vào trigger DB.
/// MEALS (hóa đơn bếp cuối tháng, UC30) được đối soát mềm với Σ DailyMeal.TotalAmount (DEV-MI-12-2).
/// </summary>
public sealed class MonthlyCostService : IMonthlyCostService
{
    private static readonly Regex PeriodMonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IMonthlyCostRepository _repo;
    private readonly IExcelMonthlyCostParser _excelParser;
    private readonly IMonthlyCostExcelWriter _excelWriter;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;
    private readonly ILogger<MonthlyCostService> _logger;

    public MonthlyCostService(IMonthlyCostRepository repo, IExcelMonthlyCostParser excelParser,
        IMonthlyCostExcelWriter excelWriter, IAuditLogService audit, IClock clock, ILogger<MonthlyCostService> logger)
    {
        _repo = repo;
        _excelParser = excelParser;
        _excelWriter = excelWriter;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    // ---------- Kế toán: tải file mẫu (UC12, DEV-MI-12-3) ----------

    public Task<Result<ExcelFileDto>> ExportTemplateAsync(CancellationToken ct)
    {
        var content = _excelWriter.WriteTemplate();
        _logger.LogInformation("Đã xuất file mẫu chi phí tháng");
        return Task.FromResult(Result<ExcelFileDto>.Success(
            new ExcelFileDto(content, "MauChiPhiThang.xlsx"), Messages.ExcelExported));
    }

    // ---------- Kế toán: xem trước / nhập chi phí tháng từ Excel (UC12) ----------

    public async Task<Result<MonthlyCostExcelPreviewDto>> PreviewImportAsync(
        Stream stream, string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<MonthlyCostExcelPreviewDto>.Failure(ErrorCodes.MonthlyCostPeriodMonthInvalid,
                Messages.MonthlyCostPeriodMonthInvalid, ResultKind.Validation);

        if (!TryParseExcel(stream, out var rawRows, out var errorCode, out var errorMessage))
            return Result<MonthlyCostExcelPreviewDto>.Failure(errorCode!, errorMessage!, ResultKind.Validation);

        if (rawRows!.Count == 0)
            return Result<MonthlyCostExcelPreviewDto>.Failure(ErrorCodes.MonthlyCostExcelEmpty,
                Messages.MonthlyCostExcelEmpty);

        var products = await LookupProductsAsync(rawRows, ct);
        var (previews, valid, error, duplicate, items) = ValidateRows(rawRows, products);
        var reconciliation = await BuildMealsReconciliationAsync(periodMonth, items, ct);

        return Result<MonthlyCostExcelPreviewDto>.Success(
            new MonthlyCostExcelPreviewDto(valid, error, duplicate, previews, reconciliation));
    }

    public async Task<Result<MonthlyCostImportResultDto>> ImportAsync(
        Stream stream, string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<MonthlyCostImportResultDto>.Failure(ErrorCodes.MonthlyCostPeriodMonthInvalid,
                Messages.MonthlyCostPeriodMonthInvalid, ResultKind.Validation);

        if (!TryParseExcel(stream, out var rawRows, out var errorCode, out var errorMessage))
            return Result<MonthlyCostImportResultDto>.Failure(errorCode!, errorMessage!, ResultKind.Validation);

        if (rawRows!.Count == 0)
            return Result<MonthlyCostImportResultDto>.Failure(ErrorCodes.MonthlyCostExcelEmpty,
                Messages.MonthlyCostExcelEmpty);

        var products = await LookupProductsAsync(rawRows, ct);
        var (_, valid, error, duplicate, items) = ValidateRows(rawRows, products);

        if (items.Count == 0)
            return Result<MonthlyCostImportResultDto>.Failure(ErrorCodes.MonthlyCostExcelEmpty,
                "Không có dòng hợp lệ nào để nhập.");

        // Pre-check tháng khóa (R7/GBR-10): trigger trg_MonthlyCost_PeriodLock cũng chặn, nhưng kiểm tra
        // trước ở đây để trả Result.Failure sạch thay vì để lộ lỗi SQL thô (cùng kiểu PayrollService).
        var period = await _repo.GetOrCreatePeriodAsync(periodMonth, ct);
        if (period.Status == RecordStatus.Locked)
            return Result<MonthlyCostImportResultDto>.Failure(ErrorCodes.MonthlyCostPeriodLocked,
                string.Format(Messages.AttendancePeriodLockedMessage, periodMonth));   // MSG21

        // "Import lại thay số tháng đó": xóa toàn bộ dòng cũ rồi ghi lại các dòng hợp lệ, một SaveChanges
        // (DEV-MI-12-1 — không có cột lịch sử/versioning trong schema để giữ "có lịch sử" như SRS nói).
        var previous = await _repo.GetItemsByPeriodIdAsync(period.PeriodId, ct);   // giá trị cũ cho AuditLog
        await _repo.RemoveItemsByPeriodIdAsync(period.PeriodId, ct);

        var now = _clock.Now;
        foreach (var item in items)
        {
            item.PeriodId = period.PeriodId;
            item.ImportedAt = now;
        }
        _repo.AddItems(items);

        var totalAmount = items.Sum(i => i.Amount);
        // Log theo tháng (RecordId = PeriodId) vì import thay cả bộ dòng chi phí của tháng, không sửa từng dòng (GBR-15).
        _audit.Add(new AuditEntry(nameof(MonthlyCost), period.PeriodId,
            previous.Count == 0 ? AuditActions.Insert : AuditActions.Update,
            previous.Count == 0 ? null : new { ItemCount = previous.Count, TotalAmount = previous.Sum(i => i.Amount) },
            new { PeriodMonth = periodMonth, ItemCount = items.Count, TotalAmount = totalAmount }));
        await _repo.SaveChangesAsync(ct);

        var reconciliation = await BuildMealsReconciliationAsync(periodMonth, items, ct);

        _logger.LogInformation(
            "Import chi phí tháng {PeriodMonth}: {Valid} dòng hợp lệ, {Error} dòng lỗi, {Duplicate} dòng trùng, " +
            "{ItemCount} khoản, tổng {TotalAmount}",
            periodMonth, valid, error, duplicate, items.Count, totalAmount);

        return Result<MonthlyCostImportResultDto>.Success(
            new MonthlyCostImportResultDto(
                period.PeriodId, periodMonth, valid, error, duplicate, items.Count, totalAmount, reconciliation),
            BuildImportMessage(valid, error, duplicate));
    }

    // ---------- Kế toán: xem chi phí đã nhập của một tháng (UC12/UC33) ----------

    public async Task<Result<MonthlyCostSummaryDto>> GetByPeriodMonthAsync(string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<MonthlyCostSummaryDto>.Failure(ErrorCodes.MonthlyCostPeriodMonthInvalid,
                Messages.MonthlyCostPeriodMonthInvalid, ResultKind.Validation);

        var period = await _repo.FindPeriodByMonthAsync(periodMonth, ct);
        if (period is null)
            return Result<MonthlyCostSummaryDto>.Failure(ErrorCodes.MonthlyCostNotFound,
                Messages.MonthlyCostNotFound, ResultKind.NotFound);

        var items = await _repo.GetItemsByPeriodIdAsync(period.PeriodId, ct);
        if (items.Count == 0)
            return Result<MonthlyCostSummaryDto>.Failure(ErrorCodes.MonthlyCostNotFound,
                Messages.MonthlyCostNotFound, ResultKind.NotFound);

        var expected = await _repo.GetMealsTotalAmountAsync(period.StartDate, period.EndDate, ct);
        var mealsItem = items.FirstOrDefault(i => i.CostCategoryCode == MonthlyCostCategory.Meals);
        var reconciliation = new MealsReconciliationDto(expected, mealsItem?.Amount,
            mealsItem is not null && mealsItem.Amount != expected);

        var totalAmount = items.Sum(i => i.Amount);
        var dtos = items.OrderBy(i => i.CostCategoryCode).ThenBy(i => i.ProductId).Select(ToItemDto).ToList();

        return Result<MonthlyCostSummaryDto>.Success(
            new MonthlyCostSummaryDto(
                period.PeriodId, periodMonth, period.Status == RecordStatus.Locked, totalAmount, dtos, reconciliation));
    }

    // ---------- Dùng chung ----------

    private static bool IsValidMonth(string periodMonth) => PeriodMonthPattern.IsMatch(periodMonth);

    private static (DateOnly Start, DateOnly End) ComputePeriodDateRange(string periodMonth)
    {
        var year = int.Parse(periodMonth[..4]);
        var month = int.Parse(periodMonth[5..7]);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return (start, end);
    }

    /// <summary>Σ DailyMeal.TotalAmount trong tháng so với dòng MEALS (nếu có) trong tập hợp đang xét —
    /// dùng được cả cho preview (chưa ghi DB, tính theo khoảng ngày suy ra trực tiếp từ periodMonth,
    /// không cần tạo AccountingPeriod) lẫn sau khi import thật.</summary>
    private async Task<MealsReconciliationDto> BuildMealsReconciliationAsync(
        string periodMonth, IReadOnlyList<MonthlyCost> items, CancellationToken ct)
    {
        var (start, end) = ComputePeriodDateRange(periodMonth);
        var expected = await _repo.GetMealsTotalAmountAsync(start, end, ct);
        var mealsItem = items.FirstOrDefault(i => i.CostCategoryCode == MonthlyCostCategory.Meals);
        var imported = mealsItem?.Amount;
        var hasDiscrepancy = imported.HasValue && imported.Value != expected;
        return new MealsReconciliationDto(expected, imported, hasDiscrepancy);
    }

    private static MonthlyCostItemDto ToItemDto(MonthlyCost m) => new(
        m.MonthlyCostId, CodeText.ToPascal(m.CostCategoryCode), CodeText.ToPascal(m.CostType), m.ProductId,
        m.Product?.ProductCode, m.Product?.ProductName, m.Amount, m.Quantity, m.Note, m.ImportedAt);

    /// <summary>MSG24: {a} dòng hợp lệ, {b} dòng lỗi, {c} dòng trùng.</summary>
    private static string BuildImportMessage(int valid, int error, int duplicate) =>
        $"{valid} dòng hợp lệ, {error} dòng lỗi, {duplicate} dòng trùng. Chỉ các dòng hợp lệ được nhập.";

    /// <summary>Parse file Excel; file sai cấu trúc trả về false kèm mã lỗi để Service dựng 400.</summary>
    private bool TryParseExcel(Stream stream, out IReadOnlyList<MonthlyCostExcelRowDto>? rows,
        out string? errorCode, out string? errorMessage)
    {
        rows = null;
        errorCode = null;
        errorMessage = null;
        try
        {
            rows = _excelParser.Parse(stream);
            return true;
        }
        catch (InvalidDataException ex)
        {
            errorCode = ErrorCodes.MonthlyCostExcelInvalid;
            errorMessage = ex.Message;
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            _logger.LogWarning(ex, "MonthlyCost Excel parse failed");
            errorCode = ErrorCodes.MonthlyCostExcelInvalid;
            errorMessage = Messages.MonthlyCostExcelInvalid;
            return false;
        }
    }

    private async Task<IReadOnlyDictionary<string, (int ProductId, string ProductName)>> LookupProductsAsync(
        IReadOnlyList<MonthlyCostExcelRowDto> rawRows, CancellationToken ct)
    {
        var codes = rawRows.Where(r => r.ParseError is null && !string.IsNullOrWhiteSpace(r.ProductCode))
            .Select(r => r.ProductCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return await _repo.GetProductsByCodesAsync(codes, ct);
    }

    /// <summary>Kiểm tra nghiệp vụ từng dòng thô: loại chi phí hợp lệ, kiểu FIXED/VARIABLE, mã hàng bắt
    /// buộc đúng chiều theo kiểu, số tiền hợp lệ, trùng trong file theo (CostCategoryCode, ProductId) —
    /// khớp UNIQUE (PeriodId, CostCategoryCode, ProductId) của schema vì cùng một lần import chung
    /// một PeriodId.</summary>
    private static (List<MonthlyCostExcelRowPreviewDto> Previews, int Valid, int Error, int Duplicate,
        List<MonthlyCost> Items)
        ValidateRows(IReadOnlyList<MonthlyCostExcelRowDto> rawRows,
            IReadOnlyDictionary<string, (int ProductId, string ProductName)> products)
    {
        var previews = new List<MonthlyCostExcelRowPreviewDto>(rawRows.Count);
        var items = new List<MonthlyCost>();
        var seenKeys = new HashSet<(string Category, int? ProductId)>();
        int valid = 0, error = 0, duplicate = 0;

        foreach (var row in rawRows)
        {
            if (row.ParseError is not null)
            {
                previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error, row.ParseError));
                error++;
                continue;
            }

            var category = row.CostCategoryCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(category))
            {
                previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error, "Thiếu loại chi phí."));
                error++;
                continue;
            }
            if (!MonthlyCostCategory.All.Contains(category))
            {
                previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error,
                    $"Loại chi phí '{category}' không hợp lệ."));
                error++;
                continue;
            }

            var costType = row.CostType?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(costType))
            {
                previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error,
                    "Thiếu kiểu chi phí (FIXED/VARIABLE)."));
                error++;
                continue;
            }
            if (!MonthlyCostType.All.Contains(costType))
            {
                previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error,
                    $"Kiểu chi phí '{costType}' không hợp lệ (cần FIXED/VARIABLE)."));
                error++;
                continue;
            }

            var productCode = row.ProductCode?.Trim();
            int? productId = null;
            string? productName = null;
            if (costType == MonthlyCostType.Variable)
            {
                if (string.IsNullOrWhiteSpace(productCode))
                {
                    previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error,
                        "Khoản biến đổi (VARIABLE) phải có mã hàng."));
                    error++;
                    continue;
                }
                if (!products.TryGetValue(productCode, out var product))
                {
                    previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error,
                        $"Mã hàng '{productCode}' không tồn tại."));
                    error++;
                    continue;
                }
                productId = product.ProductId;
                productName = product.ProductName;
            }
            else if (!string.IsNullOrWhiteSpace(productCode))
            {
                previews.Add(ToPreview(row, null, MonthlyCostRowStatus.Error,
                    "Khoản cố định (FIXED) không được có mã hàng."));
                error++;
                continue;
            }

            if (row.Amount is null)
            {
                previews.Add(ToPreview(row, productName, MonthlyCostRowStatus.Error, "Thiếu số tiền."));
                error++;
                continue;
            }
            if (row.Amount < 0)
            {
                previews.Add(ToPreview(row, productName, MonthlyCostRowStatus.Error,
                    "Số tiền phải lớn hơn hoặc bằng 0."));
                error++;
                continue;
            }

            var key = (category, productId);
            if (!seenKeys.Add(key))
            {
                previews.Add(ToPreview(row, productName, MonthlyCostRowStatus.Duplicate,
                    "Dòng trùng (cùng loại chi phí và mã hàng)."));
                duplicate++;
                continue;
            }

            items.Add(new MonthlyCost
            {
                CostCategoryCode = category, CostType = costType, ProductId = productId,
                Amount = row.Amount.Value, Quantity = row.Quantity, Note = row.Note,
            });
            previews.Add(ToPreview(row, productName, MonthlyCostRowStatus.Valid, null));
            valid++;
        }

        return (previews, valid, error, duplicate, items);
    }

    private static MonthlyCostExcelRowPreviewDto ToPreview(MonthlyCostExcelRowDto row, string? productName,
        string status, string? errorMessage) => new(
        row.RowNumber, row.CostCategoryCode, row.CostType, row.ProductCode, productName, row.Amount, row.Quantity,
        row.Note, status, errorMessage);
}
