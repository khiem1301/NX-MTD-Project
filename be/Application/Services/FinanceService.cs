using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// MI-13 — Doanh thu (UC27, Kế toán + Admin) và Hòa vốn + giá trị thặng dư (UC11, chỉ Admin, GBR-17).
/// Doanh thu suy ra từ vw_MonthlyRevenue (đọc, không đổi view). Hòa vốn: mỗi lần "Tính toán" ghi thêm một
/// bộ dòng BreakEvenResult mới (CalculatedAt dùng chung cho cả bộ), không ghi đè lần trước — lịch sử được
/// giữ nguyên nhờ UNIQUE (PeriodId, CalculatedAt, ProductId) đã có sẵn trong schema.
///
/// Công thức LABOUR_HOURS (phân bổ định phí theo giờ công quy đổi) là engineering judgment call cho một
/// khoảng trống "chưa chốt" của chính dự án (AI Context mục 12) — xem docs/CODE_DEVIATIONS.md DEV-MI-13-1.
/// </summary>
public sealed class FinanceService : IFinanceService
{
    private static readonly Regex PeriodMonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IFinanceRepository _repo;
    private readonly IFinanceExcelWriter _excelWriter;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;
    private readonly ILogger<FinanceService> _logger;

    public FinanceService(IFinanceRepository repo, IFinanceExcelWriter excelWriter, IAuditLogService audit,
        IClock clock, ILogger<FinanceService> logger)
    {
        _repo = repo;
        _excelWriter = excelWriter;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    // ================= Doanh thu (UC27) — Kế toán + Admin, KHÔNG có CmtUnitPrice (GBR-02) =================

    public async Task<Result<PagedResult<RevenueSummaryDto>>> GetRevenueAsync(RevenueQuery query, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(query.PeriodMonth) && !IsValidMonth(query.PeriodMonth))
            return Result<PagedResult<RevenueSummaryDto>>.Failure(ErrorCodes.RevenuePeriodMonthInvalid,
                Messages.RevenuePeriodMonthInvalid, ResultKind.Validation);

        var rows = await _repo.GetRevenueRowsAsync(query.PeriodMonth, query.OrderId, query.ProductId, ct);
        var page = Paging.NormalizePage(query.Page);
        var pageSize = Paging.NormalizePageSize(query.PageSize);

        var total = rows.Count;
        var pageRows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var dtos = await ToRevenueDtosAsync(pageRows, ct);

        return Result<PagedResult<RevenueSummaryDto>>.Success(
            new PagedResult<RevenueSummaryDto>(dtos, page, pageSize, total), total == 0 ? Messages.NoData : null);
    }

    public async Task<Result<ExcelFileDto>> ExportRevenueAsync(RevenueQuery query, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(query.PeriodMonth) && !IsValidMonth(query.PeriodMonth))
            return Result<ExcelFileDto>.Failure(ErrorCodes.RevenuePeriodMonthInvalid,
                Messages.RevenuePeriodMonthInvalid, ResultKind.Validation);

        var rows = await _repo.GetRevenueRowsAsync(query.PeriodMonth, query.OrderId, query.ProductId, ct);
        var dtos = await ToRevenueDtosAsync(rows, ct);
        var content = _excelWriter.WriteRevenueSheet(dtos);

        _logger.LogInformation("Đã xuất file Excel doanh thu ({Count} dòng)", dtos.Count);
        return Result<ExcelFileDto>.Success(new ExcelFileDto(content, "DoanhThu.xlsx"), Messages.ExcelExported);
    }

    private async Task<List<RevenueSummaryDto>> ToRevenueDtosAsync(
        IReadOnlyList<VwMonthlyRevenue> rows, CancellationToken ct)
    {
        var orderIds = rows.Select(r => r.OrderId).Distinct().ToList();
        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var orderCodes = await _repo.GetOrderCodesAsync(orderIds, ct);
        var products = await _repo.GetProductsAsync(productIds, ct);

        return rows.Select(r =>
        {
            var orderCode = orderCodes.GetValueOrDefault(r.OrderId, "");
            var (productCode, productName) = products.GetValueOrDefault(r.ProductId, ("", ""));
            return new RevenueSummaryDto(r.PeriodMonth ?? "", r.OrderId, orderCode, r.ProductId, productCode,
                productName, r.FinishedQty ?? 0, r.Revenue ?? 0m);
        }).ToList();
    }

    // ================= Hòa vốn + giá trị thặng dư (UC11) — CHỈ Admin =================

    public async Task<Result<BreakEvenCalculationResultDto>> CalculateAsync(
        BreakEvenCalculateDto dto, CancellationToken ct)
    {
        if (!IsValidMonth(dto.PeriodMonth))
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenPeriodMonthInvalid,
                Messages.BreakEvenPeriodMonthInvalid, ResultKind.Validation);

        var allocationBasis = ToRawAllocationBasis(dto.AllocationBasis);
        if (allocationBasis is null)
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenInvalidAllocationBasis,
                Messages.BreakEvenInvalidAllocationBasis, ResultKind.Validation);

        // Cổng điều kiện GBR-17/MSG25: bảng lương tháng đã duyệt VÀ chi phí tháng đã nhập. Không tự tạo kỳ
        // kế toán nếu chưa có — khác MonthlyCostService/PayrollService (đây không phải hành động "import").
        var period = await _repo.FindPeriodByMonthAsync(dto.PeriodMonth, ct);
        if (period is null)
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenNotReady,
                Messages.BreakEvenNotReady);

        var payrollApproved = await _repo.IsPayrollApprovedAsync(period.PeriodId, ct);
        var hasMonthlyCost = await _repo.HasMonthlyCostAsync(period.PeriodId, ct);
        if (!payrollApproved || !hasMonthlyCost)
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenNotReady,
                Messages.BreakEvenNotReady);

        // Bước 1-2: phạm vi = mọi ProductId có ít nhất 1 dòng vw_MonthlyRevenue trong tháng, gộp theo
        // (PeriodMonth, ProductId) — tổng hợp lại xuyên OrderId (một mã hàng có thể nằm ở nhiều đơn).
        var revenueRows = await _repo.GetRevenueRowsAsync(dto.PeriodMonth, null, null, ct);
        if (revenueRows.Count == 0)
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenNoOutput,
                Messages.BreakEvenNoOutput);

        var byProduct = revenueRows
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => (FinishedQty: g.Sum(x => x.FinishedQty ?? 0), Revenue: g.Sum(x => x.Revenue ?? 0m)));
        var productIds = byProduct.Keys.ToList();
        var products = await _repo.GetProductsAsync(productIds, ct);

        // Bước 4-5: chi phí tháng — biến phí theo mã hàng, định phí cộng dồn toàn kỳ.
        var costs = await _repo.GetMonthlyCostsAsync(period.PeriodId, ct);
        var totalFixedCost = costs.Where(c => c.CostType == MonthlyCostType.Fixed).Sum(c => c.Amount);
        var variableByProduct = costs
            .Where(c => c.CostType == MonthlyCostType.Variable && c.ProductId.HasValue)
            .GroupBy(c => c.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        // Bước 6: tỉ lệ phân bổ — OUTPUT luôn tính sẵn (cũng dùng làm fallback cho LABOUR_HOURS, DEV-MI-13-1).
        var totalFinishedQty = byProduct.Values.Sum(v => v.FinishedQty);
        var outputShare = productIds.ToDictionary(
            id => id, id => totalFinishedQty > 0 ? (decimal)byProduct[id].FinishedQty / totalFinishedQty : 0m);

        var laborHoursByProduct = allocationBasis == AllocationBasis.LabourHours
            ? await ComputeLaborHoursByProductAsync(period, ct)
            : new Dictionary<int, decimal>();
        var totalLaborHours = productIds.Sum(id => laborHoursByProduct.GetValueOrDefault(id));

        // CalculatedAt chỉ chính xác đến giây (datetime2(0)) và nằm trong khóa UNIQUE (PeriodId, CalculatedAt, ProductId):
        // tính lại trong cùng một giây với lần trước sẽ bị 409 "trùng". Luôn lấy mốc lớn hơn lần tính gần nhất.
        var now = _clock.Now;
        var calculatedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, now.Kind);
        var previousRuns = await _repo.GetResultsAsync(period.PeriodId, null, ct);
        if (previousRuns.Count > 0)
        {
            var lastRun = previousRuns.Max(r => r.CalculatedAt);
            if (calculatedAt <= lastRun) calculatedAt = lastRun.AddSeconds(1);
        }

        var entities = new List<BreakEvenResult>(productIds.Count);
        var warningsByProductId = new Dictionary<int, string>();

        foreach (var productId in productIds.OrderBy(id => ProductCodeOf(products, id)))
        {
            var (finishedQty, revenue) = byProduct[productId];
            var cmtUnitPrice = finishedQty > 0 ? Math.Round(revenue / finishedQty, 2, MidpointRounding.AwayFromZero) : 0m;
            var variableCost = variableByProduct.GetValueOrDefault(productId);

            decimal share;
            if (allocationBasis == AllocationBasis.LabourHours)
            {
                var hours = laborHoursByProduct.GetValueOrDefault(productId);
                if (hours > 0 && totalLaborHours > 0)
                {
                    share = hours / totalLaborHours;
                }
                else
                {
                    // DEV-MI-13-1: mã hàng này không quy đổi được giờ công hợp lệ trong kỳ (không có
                    // OutputRecord nào, hoặc công nhân không có DailyTimesheet khớp ngày) → dùng tỉ lệ
                    // OUTPUT thay thế riêng cho mã hàng này, các mã hàng khác vẫn theo LABOUR_HOURS.
                    share = outputShare[productId];
                }
            }
            else
            {
                share = outputShare[productId];
            }

            var allocatedFixedCost = Math.Round(totalFixedCost * share, 2, MidpointRounding.AwayFromZero);
            var variableCostPerUnit = finishedQty > 0 ? variableCost / finishedQty : 0m;

            decimal? breakEvenQty = cmtUnitPrice <= variableCostPerUnit
                ? null
                : Math.Round(allocatedFixedCost / (cmtUnitPrice - variableCostPerUnit), 2, MidpointRounding.AwayFromZero);

            // Guard chia cho 0: AllocatedFixedCost = 0 (không gánh định phí) cho ra BreakEvenQty = 0, nghĩa
            // là đã đạt hòa vốn ngay từ sản phẩm đầu tiên → coi như 100% thay vì chia FinishedQty/0.
            decimal? percentReached = breakEvenQty switch
            {
                null => null,
                0m => 100m,
                _ => Math.Round(finishedQty / breakEvenQty.Value * 100m, 2, MidpointRounding.AwayFromZero),
            };

            var surplusValue = Math.Round(revenue - variableCost - allocatedFixedCost, 2, MidpointRounding.AwayFromZero);
            var isAtRisk = surplusValue < 0;

            entities.Add(new BreakEvenResult
            {
                PeriodId = period.PeriodId, ProductId = productId, AllocationBasis = allocationBasis,
                FinishedQty = finishedQty, CmtUnitPrice = cmtUnitPrice, Revenue = revenue, VariableCost = variableCost,
                AllocatedFixedCost = allocatedFixedCost, BreakEvenQty = breakEvenQty, PercentReached = percentReached,
                SurplusValue = surplusValue, IsAtRisk = isAtRisk, CalculatedAt = calculatedAt,
            });

            if (isAtRisk)
            {
                var productCode = ProductCodeOf(products, productId);
                // MSG26: khi không tính được điểm hòa vốn (đơn giá ≤ biến phí/sp), dùng câu thay thế ngắn
                // gọn thay vì "{x}%" không xác định (xem báo cáo task, không phải MSG mới).
                warningsByProductId[productId] = percentReached.HasValue
                    ? string.Format(Messages.BreakEvenAtRiskWarning, productCode, percentReached.Value)
                    : string.Format(Messages.BreakEvenAtRiskWarningNoBreakEven, productCode);
            }
        }

        _repo.AddBreakEvenResults(entities);
        // Kết quả hòa vốn là dữ liệu mật (GBR-02): log chỉ ghi cách tính + số mã hàng, không ghi doanh thu / đơn giá CMT (D26).
        _audit.Add(new AuditEntry(nameof(BreakEvenResult), period.PeriodId, AuditActions.Insert, null,
            new
            {
                PeriodMonth = dto.PeriodMonth, AllocationBasis = allocationBasis, CalculatedAt = calculatedAt,
                ProductCount = entities.Count, AtRiskCount = entities.Count(e => e.IsAtRisk),
            }));
        await _repo.SaveChangesAsync(ct);

        var items = entities.Select(e => ToDto(e, dto.PeriodMonth, products, warningsByProductId)).ToList();
        var warnings = items.Where(i => i.AtRiskWarning is not null).Select(i => i.AtRiskWarning!).ToList();

        _logger.LogInformation(
            "Tính hòa vốn tháng {PeriodMonth} ({AllocationBasis}): {Count} mã hàng, {AtRisk} nguy cơ lỗ",
            dto.PeriodMonth, allocationBasis, items.Count, warnings.Count);

        return Result<BreakEvenCalculationResultDto>.Success(
            new BreakEvenCalculationResultDto(period.PeriodId, dto.PeriodMonth, CodeText.ToPascal(allocationBasis),
                calculatedAt, items, warnings),
            string.Format(Messages.BreakEvenCalculated, dto.PeriodMonth));
    }

    public async Task<Result<BreakEvenListResultDto>> GetBreakEvenAsync(
        string periodMonth, int? productId, bool history, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<BreakEvenListResultDto>.Failure(ErrorCodes.BreakEvenPeriodMonthInvalid,
                Messages.BreakEvenPeriodMonthInvalid, ResultKind.Validation);

        var period = await _repo.FindPeriodByMonthAsync(periodMonth, ct);
        if (period is null)
            return Result<BreakEvenListResultDto>.Failure(ErrorCodes.BreakEvenNotFound,
                Messages.BreakEvenNotFound, ResultKind.NotFound);

        var rows = await _repo.GetResultsAsync(period.PeriodId, productId, ct);
        if (rows.Count == 0)
            return Result<BreakEvenListResultDto>.Failure(ErrorCodes.BreakEvenNotFound,
                Messages.BreakEvenNotFound, ResultKind.NotFound);

        if (!history)
        {
            var latest = rows.Max(r => r.CalculatedAt);
            rows = rows.Where(r => r.CalculatedAt == latest).ToList();
        }

        var dtos = rows.Select(r => ToDto(r, periodMonth)).ToList();
        return Result<BreakEvenListResultDto>.Success(new BreakEvenListResultDto(period.PeriodId, periodMonth, dtos));
    }

    public async Task<Result<ExcelFileDto>> ExportBreakEvenAsync(
        string periodMonth, int? productId, bool history, CancellationToken ct)
    {
        var result = await GetBreakEvenAsync(periodMonth, productId, history, ct);
        if (!result.IsSuccess)
            return Result<ExcelFileDto>.Failure(result.ErrorCode!, result.Message!, result.Kind);

        var content = _excelWriter.WriteBreakEvenSheet(result.Data!.Items);
        _logger.LogInformation("Đã xuất file Excel hòa vốn tháng {PeriodMonth} ({Count} dòng)",
            periodMonth, result.Data.Items.Count);
        return Result<ExcelFileDto>.Success(new ExcelFileDto(content, $"HoaVon_{periodMonth}.xlsx"), Messages.ExcelExported);
    }

    // ---------- Dùng chung ----------

    private static bool IsValidMonth(string periodMonth) => PeriodMonthPattern.IsMatch(periodMonth);

    private static string ProductCodeOf(
        IReadOnlyDictionary<int, (string ProductCode, string ProductName)> products, int productId) =>
        products.TryGetValue(productId, out var p) ? p.ProductCode : "";

    /// <summary>"Output"/"LabourHours" (FE, PascalCase theo Coding Rules 2.2) → "OUTPUT"/"LABOUR_HOURS" (DB).
    /// Cũng chấp nhận luôn giá trị UPPER_SNAKE gốc (gọi trực tiếp qua Swagger/test). Giá trị lạ trả về null
    /// để Service báo lỗi validation thay vì lưu rác.</summary>
    private static string? ToRawAllocationBasis(string? value) => value switch
    {
        "Output" or "OUTPUT" => AllocationBasis.Output,
        "LabourHours" or "LABOUR_HOURS" => AllocationBasis.LabourHours,
        _ => null,
    };

    /// <summary>DEV-MI-13-1: quy đổi giờ công (DailyTimesheet.WorkedHours) sang theo mã hàng bằng tỉ lệ
    /// Quantity trong ngày của từng OutputRecord (OrderLine → ProductId) — xem docs/CODE_DEVIATIONS.md.</summary>
    private async Task<Dictionary<int, decimal>> ComputeLaborHoursByProductAsync(
        AccountingPeriod period, CancellationToken ct)
    {
        var outputRows = await _repo.GetOutputForLabourAsync(period.StartDate, period.EndDate, ct);
        var workerIds = outputRows.Select(o => o.WorkerId).Distinct().ToList();
        var workedHours = await _repo.GetWorkedHoursAsync(workerIds, period.StartDate, period.EndDate, ct);

        var laborHoursByProduct = new Dictionary<int, decimal>();
        foreach (var dayGroup in outputRows.GroupBy(o => (o.WorkerId, o.WorkDate)))
        {
            var totalQtyForDay = dayGroup.Sum(x => x.Quantity);
            if (totalQtyForDay <= 0) continue;
            if (!workedHours.TryGetValue(dayGroup.Key, out var hours) || hours <= 0) continue;

            foreach (var row in dayGroup)
            {
                var attributed = hours * row.Quantity / totalQtyForDay;
                laborHoursByProduct[row.ProductId] = laborHoursByProduct.GetValueOrDefault(row.ProductId) + attributed;
            }
        }

        return laborHoursByProduct;
    }

    private static BreakEvenResultDto ToDto(BreakEvenResult r, string periodMonth,
        IReadOnlyDictionary<int, (string ProductCode, string ProductName)> products,
        IReadOnlyDictionary<int, string> warningsByProductId)
    {
        var (productCode, productName) = products.GetValueOrDefault(r.ProductId, ("", ""));
        return new BreakEvenResultDto(r.BreakEvenResultId, r.PeriodId, periodMonth, r.ProductId, productCode,
            productName, CodeText.ToPascal(r.AllocationBasis), r.FinishedQty, r.CmtUnitPrice, r.Revenue,
            r.VariableCost, r.AllocatedFixedCost, r.BreakEvenQty, r.PercentReached, r.SurplusValue, r.IsAtRisk,
            r.CalculatedAt, warningsByProductId.GetValueOrDefault(r.ProductId));
    }

    /// <summary>Dùng khi đọc lại từ DB (Product đã Include sẵn) — không cần dictionary tra cứu riêng.</summary>
    private static BreakEvenResultDto ToDto(BreakEvenResult r, string periodMonth)
    {
        string? warning = null;
        if (r.IsAtRisk)
        {
            warning = r.PercentReached.HasValue
                ? string.Format(Messages.BreakEvenAtRiskWarning, r.Product.ProductCode, r.PercentReached.Value)
                : string.Format(Messages.BreakEvenAtRiskWarningNoBreakEven, r.Product.ProductCode);
        }

        return new BreakEvenResultDto(r.BreakEvenResultId, r.PeriodId, periodMonth, r.ProductId, r.Product.ProductCode,
            r.Product.ProductName, CodeText.ToPascal(r.AllocationBasis), r.FinishedQty, r.CmtUnitPrice, r.Revenue,
            r.VariableCost, r.AllocatedFixedCost, r.BreakEvenQty, r.PercentReached, r.SurplusValue, r.IsAtRisk,
            r.CalculatedAt, warning);
    }
}
