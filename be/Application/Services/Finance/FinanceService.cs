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

    /// <summary>Đơn hàng và mã hàng đã có doanh thu, để bộ lọc gợi ý thay vì bắt nhập Id.</summary>
    public async Task<Result<RevenueFilterOptionsDto>> GetRevenueFilterOptionsAsync(CancellationToken ct)
    {
        var rows = await _repo.GetRevenueRowsAsync(null, null, null, ct);
        var dtos = await ToRevenueDtosAsync(rows, ct);
        var orders = dtos.GroupBy(d => d.OrderId).Select(g => new RevenueOrderOptionDto(g.Key, g.First().OrderCode))
            .OrderBy(o => o.OrderCode).ToList();
        var products = dtos.GroupBy(d => d.ProductId)
            .Select(g => new RevenueProductOptionDto(g.Key, g.First().ProductCode))
            .OrderBy(p => p.ProductCode).ToList();
        return Result<RevenueFilterOptionsDto>.Success(new RevenueFilterOptionsDto(orders, products));
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
            var productCode = products.GetValueOrDefault(r.ProductId, "");
            return new RevenueSummaryDto(r.PeriodMonth ?? "", r.OrderId, orderCode, r.ProductId, productCode,
                r.FinishedQty ?? 0, r.Revenue ?? 0m);
        }).ToList();
    }

    // ================= Hòa vốn + giá trị thặng dư (UC11) — CHỈ Admin =================

    public async Task<Result<BreakEvenCalculationResultDto>> CalculateAsync(
        BreakEvenCalculateDto dto, CancellationToken ct)
    {
        if (!IsValidMonth(dto.PeriodMonth))
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenPeriodMonthInvalid,
                Messages.BreakEvenPeriodMonthInvalid, ResultKind.Validation);

        var period = await FindReadyPeriodAsync(dto.PeriodMonth, ct);
        if (period is null)
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenNotReady,
                Messages.BreakEvenNotReady);

        // Bước 1-2: phạm vi = mọi ProductId có ít nhất 1 dòng vw_MonthlyRevenue trong tháng, gộp theo
        // (PeriodMonth, ProductId) — tổng hợp lại xuyên OrderId (một mã hàng có thể nằm ở nhiều đơn).
        var revenueRows = await _repo.GetRevenueRowsAsync(dto.PeriodMonth, null, null, ct);
        if (revenueRows.Count == 0)
            return Result<BreakEvenCalculationResultDto>.Failure(ErrorCodes.BreakEvenNoOutput,
                Messages.BreakEvenNoOutput);

        var (entities, warnings, products, calculatedAt, totalFixedCost) = await BuildEntitiesAsync(period, revenueRows, ct);
        await SaveResultsAsync(period, dto.PeriodMonth, entities, calculatedAt, ct);

        var items = entities.Select(e => ToDto(e, dto.PeriodMonth, products, warnings, totalFixedCost)).ToList();
        var atRisk = items.Where(i => i.AtRiskWarning is not null).Select(i => i.AtRiskWarning!).Distinct().ToList();

        _logger.LogInformation("Tính hòa vốn tháng {PeriodMonth}: {Count} mã hàng × 2 cách, {AtRisk} cảnh báo",
            dto.PeriodMonth, items.Count / AllocationBasis.All.Length, atRisk.Count);

        return Result<BreakEvenCalculationResultDto>.Success(
            new BreakEvenCalculationResultDto(period.PeriodId, dto.PeriodMonth, calculatedAt, items, atRisk),
            string.Format(Messages.BreakEvenCalculated, dto.PeriodMonth));
    }

    /// <summary>DEV-ALL-169: dữ liệu đầu vào của tháng cho popup "Tính hòa vốn" (chỉ đọc).</summary>
    public async Task<Result<BreakEvenInputsDto>> GetBreakEvenInputsAsync(string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<BreakEvenInputsDto>.Failure(ErrorCodes.BreakEvenPeriodMonthInvalid,
                Messages.BreakEvenPeriodMonthInvalid, ResultKind.Validation);

        var period = await _repo.FindPeriodByMonthAsync(periodMonth, ct);
        var revenueRows = await _repo.GetRevenueRowsAsync(periodMonth, null, null, ct);
        var productCount = revenueRows.Select(r => r.ProductId).Distinct().Count();
        var finishedQty = revenueRows.Sum(r => r.FinishedQty ?? 0);
        var revenue = revenueRows.Sum(r => r.Revenue ?? 0m);
        if (period is null)
            return Result<BreakEvenInputsDto>.Success(
                new BreakEvenInputsDto(false, false, false, false, 0, 0, 0, productCount, finishedQty, revenue, 0, false));

        var payrollApproved = await _repo.IsPayrollApprovedAsync(period.PeriodId, ct);
        var costs = await _repo.GetMonthlyCostsAsync(period.PeriodId, ct);
        var fixedTotal = costs.Where(c => c.CostType == MonthlyCostType.Fixed).Sum(c => c.Amount);
        var variable = costs.Where(c => c.CostType == MonthlyCostType.Variable).ToList();
        var labourHours = (await ComputeLaborHoursByProductAsync(period, ct)).Values.Sum();
        var canCalculate = payrollApproved && costs.Count > 0 && productCount > 0;
        return Result<BreakEvenInputsDto>.Success(new BreakEvenInputsDto(true, period.Status == RecordStatus.Locked, payrollApproved, costs.Count > 0, fixedTotal,
            variable.Sum(c => c.Amount), variable.Where(c => c.ProductId.HasValue).Select(c => c.ProductId).Distinct().Count(),
            productCount, finishedQty, revenue, Math.Round(labourHours, 1, MidpointRounding.AwayFromZero), canCalculate));
    }

    /// <summary>Lưu kết quả hòa vốn kèm AuditLog.</summary>
    private async Task SaveResultsAsync(AccountingPeriod period, string periodMonth, List<BreakEvenResult> entities,
        DateTime calculatedAt, CancellationToken ct)
    {
        _repo.AddBreakEvenResults(entities);
        // Kết quả hòa vốn là dữ liệu mật (GBR-02): log chỉ ghi cách tính + số mã hàng, không ghi doanh thu / đơn giá CMT (D26).
        _audit.Add(new AuditEntry(nameof(BreakEvenResult), period.PeriodId, AuditActions.Insert, null,
            new
            {
                PeriodMonth = periodMonth, AllocationBasis = string.Join(", ", AllocationBasis.All), CalculatedAt = calculatedAt,
                ProductCount = entities.Select(e => e.ProductId).Distinct().Count(), AtRiskCount = entities.Count(e => e.IsAtRisk),
            }));
        await _repo.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Cổng điều kiện GBR-17/MSG25: bảng lương tháng đã duyệt VÀ chi phí tháng đã nhập. Không tự tạo kỳ
    /// kế toán nếu chưa có — khác MonthlyCostService/PayrollService (đây không phải hành động "import").
    /// Trả về null khi chưa đủ điều kiện.
    /// </summary>
    private async Task<AccountingPeriod?> FindReadyPeriodAsync(string periodMonth, CancellationToken ct)
    {
        var period = await _repo.FindPeriodByMonthAsync(periodMonth, ct);
        if (period is null) return null;

        var payrollApproved = await _repo.IsPayrollApprovedAsync(period.PeriodId, ct);
        var hasMonthlyCost = await _repo.HasMonthlyCostAsync(period.PeriodId, ct);
        return payrollApproved && hasMonthlyCost ? period : null;
    }

    /// <summary>
    /// Tính kết quả hòa vốn cho từng mã hàng có doanh thu trong tháng (bước 1-6 của UC11), cả hai cách chia định phí
    /// (DEV-ALL-169): mỗi mã hàng 2 dòng cùng CalculatedAt, khác AllocationBasis.
    /// </summary>
    private async Task<(List<BreakEvenResult> Entities, Dictionary<(int, string), string> Warnings,
        IReadOnlyDictionary<int, string> Products, DateTime CalculatedAt, decimal TotalFixedCost)>
        BuildEntitiesAsync(AccountingPeriod period, IReadOnlyList<VwMonthlyRevenue> revenueRows, CancellationToken ct)
    {
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

        var labourShare = LabourShares(productIds, await ComputeLaborHoursByProductAsync(period, ct), outputShare);
        var calculatedAt = await NextCalculatedAtAsync(period.PeriodId, ct);

        var entities = new List<BreakEvenResult>(productIds.Count * AllocationBasis.All.Length);
        var warnings = new Dictionary<(int, string), string>();

        foreach (var productId in productIds.OrderBy(id => ProductCodeOf(products, id)))
        {
            var (finishedQty, revenue) = byProduct[productId];
            foreach (var basis in AllocationBasis.All)
            {
                var share = basis == AllocationBasis.LabourHours ? labourShare[productId] : outputShare[productId];
                var (entity, warning) = ComputeBreakEven(
                    period.PeriodId, productId, basis, finishedQty, revenue,
                    variableByProduct.GetValueOrDefault(productId), totalFixedCost, share, calculatedAt,
                    ProductCodeOf(products, productId));
                entities.Add(entity);
                if (warning is not null) warnings[(productId, basis)] = warning;
            }
        }
        return (entities, warnings, products, calculatedAt, totalFixedCost);
    }

    /// <summary>
    /// CalculatedAt chỉ chính xác đến giây (datetime2(0)) và nằm trong khóa UNIQUE (PeriodId, CalculatedAt, ProductId):
    /// tính lại trong cùng một giây với lần trước sẽ bị 409 "trùng". Luôn lấy mốc lớn hơn lần tính gần nhất.
    /// </summary>
    private async Task<DateTime> NextCalculatedAtAsync(int periodId, CancellationToken ct)
    {
        var now = _clock.Now;
        var calculatedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, now.Kind);
        var previousRuns = await _repo.GetResultsAsync(periodId, null, ct);
        if (previousRuns.Count > 0)
        {
            var lastRun = previousRuns.Max(r => r.CalculatedAt);
            if (calculatedAt <= lastRun) calculatedAt = lastRun.AddSeconds(1);
        }
        return calculatedAt;
    }

    /// <summary>
    /// Tỉ lệ chia định phí theo giờ công. DEV-MI-13-1: mã hàng không quy đổi được giờ công hợp lệ trong kỳ (không có
    /// OutputRecord, hoặc công nhân không có DailyTimesheet khớp ngày) tạm dùng tỉ lệ sản lượng. DEV-ALL-169: sau đó chuẩn
    /// hóa để tổng các tỉ lệ đúng bằng 100%, tức tổng định phí chia ra đúng bằng định phí tháng.
    /// </summary>
    private static Dictionary<int, decimal> LabourShares(
        IReadOnlyList<int> productIds, IReadOnlyDictionary<int, decimal> hoursByProduct, IReadOnlyDictionary<int, decimal> outputShare)
    {
        var totalHours = productIds.Sum(id => hoursByProduct.GetValueOrDefault(id));
        var raw = productIds.ToDictionary(id => id, id =>
        {
            var hours = hoursByProduct.GetValueOrDefault(id);
            return hours > 0 && totalHours > 0 ? hours / totalHours : outputShare[id];
        });
        var sum = raw.Values.Sum();
        return raw.ToDictionary(p => p.Key, p => sum > 0 ? p.Value / sum : 0m);
    }

    /// <summary>Tính hòa vốn và giá trị thặng dư cho một mã hàng; trả thêm câu cảnh báo MSG26 khi nguy cơ lỗ.</summary>
    private static (BreakEvenResult Entity, string? Warning) ComputeBreakEven(
        int periodId, int productId, string allocationBasis, int finishedQty, decimal revenue, decimal variableCost,
        decimal totalFixedCost, decimal share, DateTime calculatedAt, string productCode)
    {
        var cmtUnitPrice = finishedQty > 0 ? Math.Round(revenue / finishedQty, 2, MidpointRounding.AwayFromZero) : 0m;
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

        var entity = new BreakEvenResult
        {
            PeriodId = periodId, ProductId = productId, AllocationBasis = allocationBasis,
            FinishedQty = finishedQty, CmtUnitPrice = cmtUnitPrice, Revenue = revenue, VariableCost = variableCost,
            AllocatedFixedCost = allocatedFixedCost, BreakEvenQty = breakEvenQty, PercentReached = percentReached,
            SurplusValue = surplusValue, IsAtRisk = isAtRisk, CalculatedAt = calculatedAt,
        };

        // MSG26: khi không tính được điểm hòa vốn (đơn giá ≤ biến phí/sp), dùng câu thay thế ngắn
        // gọn thay vì "{x}%" không xác định (xem báo cáo task, không phải MSG mới).
        return (entity, isAtRisk ? AtRiskWarning(productCode, percentReached) : null);
    }

    private static string AtRiskWarning(string productCode, decimal? percentReached) =>
        percentReached.HasValue
            ? string.Format(Messages.BreakEvenAtRiskWarning, productCode, percentReached.Value)
            : string.Format(Messages.BreakEvenAtRiskWarningNoBreakEven, productCode);

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

        // Tỉ lệ chia định phí của mỗi dòng = định phí của dòng / tổng định phí cùng lần tính, cùng cách chia.
        var fixedByRun = rows.GroupBy(r => (r.CalculatedAt, r.AllocationBasis))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.AllocatedFixedCost));
        var dtos = rows.Select(r => ToDto(r, periodMonth, fixedByRun[(r.CalculatedAt, r.AllocationBasis)])).ToList();
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
        IReadOnlyDictionary<int, string> products, int productId) =>
        products.TryGetValue(productId, out var p) ? p : "";

    /// <summary>Tỉ lệ (%) định phí tháng chia cho một dòng; định phí tháng bằng 0 thì 0%.</summary>
    private static decimal SharePct(decimal allocatedFixedCost, decimal totalFixedCost) =>
        totalFixedCost > 0 ? Math.Round(allocatedFixedCost / totalFixedCost * 100m, 2, MidpointRounding.AwayFromZero) : 0m;

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
        IReadOnlyDictionary<int, string> products,
        IReadOnlyDictionary<(int, string), string> warnings, decimal totalFixedCost)
    {
        var productCode = products.GetValueOrDefault(r.ProductId, "");
        return new BreakEvenResultDto(r.BreakEvenResultId, r.PeriodId, periodMonth, r.ProductId, productCode,
            CodeText.ToPascal(r.AllocationBasis), r.FinishedQty, r.CmtUnitPrice, r.Revenue,
            r.VariableCost, r.AllocatedFixedCost, r.BreakEvenQty, r.PercentReached, r.SurplusValue, r.IsAtRisk,
            r.CalculatedAt, warnings.GetValueOrDefault((r.ProductId, r.AllocationBasis)),
            SharePct(r.AllocatedFixedCost, totalFixedCost));
    }

    /// <summary>Dùng khi đọc lại từ DB (Product đã Include sẵn) — không cần dictionary tra cứu riêng.</summary>
    private static BreakEvenResultDto ToDto(BreakEvenResult r, string periodMonth, decimal totalFixedCost)
    {
        string? warning = null;
        if (r.IsAtRisk)
        {
            warning = r.PercentReached.HasValue
                ? string.Format(Messages.BreakEvenAtRiskWarning, r.Product.ProductCode, r.PercentReached.Value)
                : string.Format(Messages.BreakEvenAtRiskWarningNoBreakEven, r.Product.ProductCode);
        }

        return new BreakEvenResultDto(r.BreakEvenResultId, r.PeriodId, periodMonth, r.ProductId, r.Product.ProductCode, CodeText.ToPascal(r.AllocationBasis), r.FinishedQty, r.CmtUnitPrice, r.Revenue,
            r.VariableCost, r.AllocatedFixedCost, r.BreakEvenQty, r.PercentReached, r.SurplusValue, r.IsAtRisk,
            r.CalculatedAt, warning, SharePct(r.AllocatedFixedCost, totalFixedCost));
    }
}
