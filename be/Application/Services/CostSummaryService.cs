using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>
/// MI-19 — UC12 "Tổng hợp chi phí", CHỈ Admin (GBR-02). Gọi thẳng <see cref="IMonthlyCostService.GetByPeriodMonthAsync"/>
/// (MI-12) để lấy chi phí đã nhập — KHÔNG copy lại logic gộp theo khoản/mã hàng của MonthlyCostService — rồi
/// cộng thêm Payroll.TotalAmount của bảng lương ĐÃ DUYỆT trong kỳ, đọc thẳng qua <see cref="IPayrollRepository"/>
/// (cùng chủ sở hữu — Minh — chỉ đọc, không cần dựng interface dùng chung mới, giống cách FinanceService đọc
/// OutputRecord/DailyTimesheet của Kiều).
/// </summary>
public sealed class CostSummaryService : ICostSummaryService
{
    private static readonly Regex PeriodMonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IMonthlyCostService _monthlyCosts;
    private readonly IPayrollRepository _payrolls;
    private readonly ICostSummaryExcelWriter _excelWriter;
    private readonly ILogger<CostSummaryService> _logger;

    public CostSummaryService(IMonthlyCostService monthlyCosts, IPayrollRepository payrolls,
        ICostSummaryExcelWriter excelWriter, ILogger<CostSummaryService> logger)
    {
        _monthlyCosts = monthlyCosts;
        _payrolls = payrolls;
        _excelWriter = excelWriter;
        _logger = logger;
    }

    public async Task<Result<CostSummaryDto>> GetAsync(string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<CostSummaryDto>.Failure(ErrorCodes.MonthlyCostPeriodMonthInvalid,
                Messages.MonthlyCostPeriodMonthInvalid, ResultKind.Validation);

        // Tái dùng thẳng MonthlyCostService (MI-12): chưa nhập chi phí tháng này thì "tổng hợp chi phí"
        // cũng chưa có gì để xem — cùng mã lỗi/thông điệp, không dựng lại.
        var costResult = await _monthlyCosts.GetByPeriodMonthAsync(periodMonth, ct);
        if (!costResult.IsSuccess)
            return Result<CostSummaryDto>.Failure(costResult.ErrorCode!, costResult.Message!, costResult.Kind);

        var cost = costResult.Data!;
        var fixedTypeText = CodeText.ToPascal(MonthlyCostType.Fixed);
        var variableTypeText = CodeText.ToPascal(MonthlyCostType.Variable);

        var fixedItems = cost.Items
            .Where(i => i.CostType == fixedTypeText)
            .Select(i => new CostFixedItemDto(i.CostCategoryCode, i.Amount))
            .ToList();

        var variableItemsRaw = cost.Items.Where(i => i.CostType == variableTypeText).ToList();
        var variableItems = variableItemsRaw
            .Select(i => new CostVariableItemDto(i.CostCategoryCode, i.ProductId!.Value, i.ProductCode!, i.ProductName!, i.Amount))
            .ToList();
        var variableByProduct = variableItemsRaw
            .GroupBy(i => (ProductId: i.ProductId!.Value, ProductCode: i.ProductCode!, ProductName: i.ProductName!))
            .Select(g => new CostByProductDto(g.Key.ProductId, g.Key.ProductCode, g.Key.ProductName, g.Sum(i => i.Amount)))
            .OrderBy(p => p.ProductCode)
            .ToList();

        var fixedTotal = fixedItems.Sum(i => i.Amount);
        var variableTotal = variableItems.Sum(i => i.Amount);

        var (payrollApproved, payrollTotal) = await GetApprovedPayrollTotalAsync(periodMonth, ct);
        var grandTotal = cost.TotalAmount + (payrollTotal ?? 0m);

        return Result<CostSummaryDto>.Success(new CostSummaryDto(
            cost.PeriodId, periodMonth, cost.IsLocked,
            fixedTotal, fixedItems, variableTotal, variableItems, variableByProduct,
            cost.TotalAmount, payrollApproved, payrollTotal, grandTotal, cost.MealsReconciliation));
    }

    public async Task<Result<ExcelFileDto>> ExportAsync(string periodMonth, CancellationToken ct)
    {
        var result = await GetAsync(periodMonth, ct);
        if (!result.IsSuccess) return Result<ExcelFileDto>.Failure(result.ErrorCode!, result.Message!, result.Kind);

        var content = _excelWriter.WriteCostSummarySheet(result.Data!);
        _logger.LogInformation("Đã xuất file Excel tổng hợp chi phí tháng {PeriodMonth}", periodMonth);
        return Result<ExcelFileDto>.Success(
            new ExcelFileDto(content, $"TongHopChiPhi_{periodMonth}.xlsx"), Messages.ExcelExported);
    }

    private static bool IsValidMonth(string periodMonth) => PeriodMonthPattern.IsMatch(periodMonth);

    /// <summary>Lương chỉ tính vào "tổng hợp chi phí" khi bảng lương tháng đã ở trạng thái APPROVED
    /// (SRS UC12: "dựa trên ... bảng lương đã duyệt"); chưa duyệt thì PayrollTotal = null, FE tự hiển thị
    /// ghi chú "chưa có bảng lương đã duyệt" thay vì chặn hẳn màn hình (khác GBR-17 của UC11, nơi thiếu
    /// một trong hai điều kiện thì chặn toàn bộ — UC12 chỉ là xem báo cáo, không phải một phép tính ràng
    /// buộc chặt như hòa vốn).</summary>
    private async Task<(bool Approved, decimal? Total)> GetApprovedPayrollTotalAsync(string periodMonth, CancellationToken ct)
    {
        var period = await _payrolls.FindPeriodByMonthAsync(periodMonth, ct);
        if (period is null) return (false, null);

        var payroll = await _payrolls.FindPayrollByPeriodIdAsync(period.PeriodId, includePayslips: false, ct);
        if (payroll is null || payroll.Status != PayrollStatus.Approved) return (false, null);

        return (true, payroll.TotalAmount);
    }
}
