using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using EmployeeLookup =
    System.Collections.Generic.IReadOnlyDictionary<string, (int EmployeeId, string FullName, string Status, string? GradeCode)>;

namespace NX_MTD.Application.Services;

/// <summary>
/// MI-09 — Bảng lương + khóa tháng (UC06, UC31, UC32). Hệ thống không tính lương (GBR-12): import chỉ
/// đọc lại số Kế toán đã tính trên Excel. "Duyệt" đồng thời là "chốt tháng" (khóa AccountingPeriod);
/// "mở lại" một bảng lương đã duyệt thì mở khóa lại tháng đó (xem DEV-MI-09-2).
/// </summary>
public sealed class PayrollService : IPayrollService
{
    private static readonly Regex PeriodMonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    /// <summary>Ngưỡng lệch (đồng) giữa tổng các khoản và Thực lĩnh trong file trước khi cảnh báo — file Excel
    /// làm tròn từng ô nên có thể lệch vài đồng.</summary>
    private const decimal NetPayTolerance = 1000m;

    private readonly IPayrollRepository _repo;
    private readonly IExcelPayrollParser _excelParser;
    private readonly IPayrollExcelWriter _excelWriter;
    private readonly IPayrollFileStore _fileStore;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ILogger<PayrollService> _logger;

    /// <param name="uow">Chỉ dùng để mở transaction khi nhập bảng lương mới (cần PayrollId trước khi ghi log); lưu dữ liệu
    /// vẫn qua <paramref name="repo"/> — cùng DbContext theo scope.</param>
    public PayrollService(IPayrollRepository repo, IExcelPayrollParser excelParser, IPayrollExcelWriter excelWriter,
        IPayrollFileStore fileStore, IAuditLogService audit, IUnitOfWork uow, IClock clock, ILogger<PayrollService> logger)
    {
        _repo = repo;
        _excelParser = excelParser;
        _excelWriter = excelWriter;
        _fileStore = fileStore;
        _audit = audit;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    // ---------- Kế toán: xuất dữ liệu tính lương (UC31) ----------

    public async Task<Result<ExcelFileDto>> ExportAsync(string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<ExcelFileDto>.Failure(ErrorCodes.PayrollPeriodMonthInvalid,
                Messages.PayrollPeriodMonthInvalid, ResultKind.Validation);

        var period = await _repo.GetOrCreatePeriodAsync(periodMonth, ct);
        var rows = await _repo.GetExportRowsAsync(periodMonth, ct);
        var missingPunch = await _repo.GetMissingPunchDaysAsync(period.StartDate, period.EndDate, ct);
        var daysWithoutOutput = await _repo.GetDaysWithoutOutputAsync(period.StartDate, period.EndDate, ct);

        var exportRows = rows.Select(r => new PayrollExportRowDto(
                r.EmployeeId, r.EmployeeCode, r.FullName, r.RoleName, CodeText.ToPascal(r.PayType), r.TeamName,
                r.WorkDays, r.SundayWorkDays, r.OvertimeHours, r.SundayOvertimeHours,
                r.PieceRateAmount, r.SundayPieceRateBonus, r.ScrapDeduction, r.GradeCode, r.GradeAllowance,
                missingPunch.GetValueOrDefault(r.EmployeeId), daysWithoutOutput.GetValueOrDefault(r.EmployeeId)))
            .ToList();

        var content = _excelWriter.WriteExportSheet(periodMonth, exportRows);

        _logger.LogInformation("Đã xuất dữ liệu tính lương tháng {PeriodMonth}: {Count} nhân viên",
            periodMonth, exportRows.Count);
        return Result<ExcelFileDto>.Success(new ExcelFileDto(content, $"BangLuong_{periodMonth}.xlsx"),
            Messages.ExcelExported);
    }

    // ---------- Kế toán: xem trước / nhập bảng lương từ Excel (UC31, DEV-MI-09-4) ----------

    public async Task<Result<PayrollExcelPreviewDto>> PreviewImportAsync(Stream stream, CancellationToken ct)
    {
        if (!TryParseExcel(stream, out var rawRows, out var errorCode, out var errorMessage))
            return Result<PayrollExcelPreviewDto>.Failure(errorCode!, errorMessage!, ResultKind.Validation);

        if (rawRows!.Count == 0)
            return Result<PayrollExcelPreviewDto>.Failure(ErrorCodes.PayrollExcelEmpty, Messages.PayrollExcelEmpty);

        var employees = await LookupEmployeesAsync(rawRows, ct);
        var (previews, valid, error, duplicate, _) = ValidateRows(rawRows, employees);

        return Result<PayrollExcelPreviewDto>.Success(new PayrollExcelPreviewDto(valid, error, duplicate, previews));
    }

    public async Task<Result<PayrollImportResultDto>> ImportAsync(Stream stream, string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<PayrollImportResultDto>.Failure(ErrorCodes.PayrollPeriodMonthInvalid,
                Messages.PayrollPeriodMonthInvalid, ResultKind.Validation);

        // Giữ nguyên byte file gốc: vừa để đọc, vừa để lưu cho giám đốc tải (DEV-MI-09-5).
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var content = buffer.ToArray();

        if (!TryParseExcel(new MemoryStream(content), out var rawRows, out var errorCode, out var errorMessage))
            return Result<PayrollImportResultDto>.Failure(errorCode!, errorMessage!, ResultKind.Validation);

        if (rawRows!.Count == 0)
            return Result<PayrollImportResultDto>.Failure(ErrorCodes.PayrollExcelEmpty, Messages.PayrollExcelEmpty);

        var employees = await LookupEmployeesAsync(rawRows, ct);
        var (_, valid, error, duplicate, groups) = ValidateRows(rawRows, employees);

        if (groups.Count == 0)
            return Result<PayrollImportResultDto>.Failure(ErrorCodes.PayrollExcelEmpty,
                "Không có dòng hợp lệ nào để nhập.");

        var period = await _repo.GetOrCreatePeriodAsync(periodMonth, ct);
        if (period.Status == RecordStatus.Locked)
            return Result<PayrollImportResultDto>.Failure(ErrorCodes.PayrollPeriodLocked,
                string.Format(Messages.AttendancePeriodLockedMessage, periodMonth));   // MSG21

        var payroll = await _repo.FindPayrollByPeriodIdAsync(period.PeriodId, includePayslips: false, ct);
        if (payroll is not null && payroll.Status is not (PayrollStatus.Draft or PayrollStatus.Rejected))
            return Result<PayrollImportResultDto>.Failure(ErrorCodes.PayrollInvalidStatus,
                Messages.PayrollInvalidStatusForImport);

        // Giá trị cũ cho AuditLog (GBR-15): chỉ có khi nhập lại bảng lương Nháp / Từ chối.
        object? before = null;
        if (payroll is null)
        {
            payroll = new Payroll { PeriodId = period.PeriodId, Status = PayrollStatus.Draft };
            _repo.AddPayroll(payroll);
        }
        else
        {
            before = new { payroll.Status };
            await _repo.RemovePayslipsAsync(payroll.PayrollId, ct);
            payroll.Status = PayrollStatus.Draft;
            payroll.RejectReason = null;   // dữ liệu mới thay thế, lý do từ chối cũ không còn áp dụng
        }

        var previousFiles = payroll.PayrollId == 0 ? [] : await _repo.RemoveImportFilesAsync(payroll.PayrollId, ct);
        var storedFile = await _fileStore.SaveAsync(periodMonth, content, ct);

        var totalAmount = 0m;
        foreach (var g in groups)
        {
            var additions = g.Items.Where(i => i.Group == PayslipItemGroup.Addition).Sum(i => i.Amount);
            var deductions = g.Items.Where(i => i.Group == PayslipItemGroup.Deduction).Sum(i => i.Amount);
            var insurance = g.Items.Where(i => i.Group == PayslipItemGroup.Insurance).Sum(i => i.Amount);

            var payslip = new Payslip
            {
                EmployeeId = g.EmployeeId,
                WorkDays = g.WorkDays,
                TotalAdditions = additions,
                TotalDeductions = deductions,
                Insurance = insurance,
                NetPay = g.NetPay,
                Payroll = payroll,
                PayslipItems = g.Items.Select((item, idx) => new PayslipItem
                {
                    ItemGroup = item.Group, ItemName = item.Name, Amount = item.Amount, SortOrder = (short)idx,
                }).ToList(),
            };
            _repo.AddPayslip(payslip);
            totalAmount += g.NetPay;
        }

        try
        {
            // Bảng lương mới chỉ có PayrollId sau lần lưu đầu nên log ghi sau, cùng một transaction (GBR-15).
            await _uow.InTransactionAsync(async () =>
            {
                await _repo.SaveChangesAsync(ct);
                _repo.AddImportFile(payroll.PayrollId, storedFile, _clock.Now);   // file gốc ở Attachment, tổng tiền = Σ NetPay
                _audit.Add(new AuditEntry(nameof(Payroll), payroll.PayrollId,
                    before is null ? AuditActions.Insert : AuditActions.Update, before,
                    new { payroll.Status, TotalAmount = totalAmount, PeriodMonth = periodMonth, PayslipCount = groups.Count }));
                await _repo.SaveChangesAsync(ct);
                return Result.Success();
            });
        }
        catch
        {
            await TryDeleteFileAsync(storedFile, ct);   // không để lại file mồ côi khi ghi DB lỗi
            throw;
        }

        foreach (var previousFile in previousFiles)
            await TryDeleteFileAsync(previousFile, ct);

        _logger.LogInformation(
            "Import bảng lương tháng {PeriodMonth}: {Valid} dòng hợp lệ, {Error} dòng lỗi, {Duplicate} dòng trùng, " +
            "{PayslipCount} phiếu lương, tổng {TotalAmount}",
            periodMonth, valid, error, duplicate, groups.Count, totalAmount);

        return Result<PayrollImportResultDto>.Success(
            new PayrollImportResultDto(payroll.PayrollId, periodMonth, valid, error, duplicate, groups.Count, totalAmount),
            BuildImportMessage(valid, error, duplicate));
    }

    public async Task<Result<PayrollSummaryDto>> GetByPeriodMonthAsync(string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<PayrollSummaryDto>.Failure(ErrorCodes.PayrollPeriodMonthInvalid,
                Messages.PayrollPeriodMonthInvalid, ResultKind.Validation);

        var period = await _repo.FindPeriodByMonthAsync(periodMonth, ct);
        var payroll = period is null
            ? null
            : await _repo.FindPayrollByPeriodIdAsync(period.PeriodId, includePayslips: true, ct);

        if (payroll is null)
            return Result<PayrollSummaryDto>.Failure(ErrorCodes.PayrollNotFound, Messages.PayrollNotFound,
                ResultKind.NotFound);

        return Result<PayrollSummaryDto>.Success((await ToSummariesAsync([payroll], ct))[0]);
    }

    public async Task<Result> SubmitAsync(int payrollId, CancellationToken ct)
    {
        var payroll = await _repo.FindPayrollByIdAsync(payrollId, includePayslips: true, ct);
        if (payroll is null)
            return Result.Failure(ErrorCodes.PayrollNotFound, Messages.PayrollNotFound, ResultKind.NotFound);

        if (payroll.Status != PayrollStatus.Draft)
            return Result.Failure(ErrorCodes.PayrollInvalidStatus, Messages.PayrollInvalidStatusForSubmit);

        if (payroll.Payslips.Count == 0)
            return Result.Failure(ErrorCodes.PayrollEmpty, Messages.PayrollEmpty);

        payroll.Status = PayrollStatus.PendingApproval;
        payroll.SubmittedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(Payroll), payroll.PayrollId, AuditActions.Update,
            new { Status = PayrollStatus.Draft }, new { payroll.Status }));
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("Đã gửi duyệt bảng lương {PayrollId}", payrollId);
        return Result.Success(Messages.Saved);
    }

    /// <summary>Tải lại file Excel gốc Kế toán đã nhập (DEV-MI-09-5) — giám đốc xem từ trang duyệt bảng lương.</summary>
    public async Task<Result<ExcelFileDto>> GetImportedFileAsync(int payrollId, CancellationToken ct)
    {
        var payroll = await _repo.FindPayrollByIdAsync(payrollId, includePayslips: false, ct);
        if (payroll is null)
            return Result<ExcelFileDto>.Failure(ErrorCodes.PayrollNotFound, Messages.PayrollNotFound,
                ResultKind.NotFound);

        var file = (await _repo.GetImportFilesAsync([payroll.PayrollId], ct)).GetValueOrDefault(payroll.PayrollId);
        var content = file is null ? null : await _fileStore.ReadAsync(file.FileUrl, ct);
        if (content is null)
            return Result<ExcelFileDto>.Failure(ErrorCodes.PayrollFileNotFound, Messages.PayrollFileNotFound,
                ResultKind.NotFound);

        return Result<ExcelFileDto>.Success(
            new ExcelFileDto(content, $"BangThanhToanLuong_{payroll.Period.PeriodMonth}.xlsx"));
    }

    // ---------- Admin: duyệt bảng lương (UC32) ----------

    public async Task<Result<PagedResult<PayrollSummaryDto>>> GetPayrollsAsync(PayrollQuery query, CancellationToken ct)
    {
        var page = Paging.NormalizePage(query.Page);
        var pageSize = Paging.NormalizePageSize(query.PageSize);
        // Contract 2.2: FE chỉ biết trạng thái dạng PascalCase (giống Status trong PayrollSummaryDto trả về);
        // DB/PayrollRepository lưu/so sánh theo mã UPPER_SNAKE nên phải đổi chiều ngược lại ở đây.
        var (items, total) = await _repo.GetPayrollsAsync(ToRawStatus(query.Status), page, pageSize, ct);
        var dtos = await ToSummariesAsync(items, ct);

        return Result<PagedResult<PayrollSummaryDto>>.Success(
            new PagedResult<PayrollSummaryDto>(dtos, page, pageSize, total), dtos.Count == 0 ? Messages.NoData : null);
    }

    public async Task<Result<PayrollDetailDto>> GetDetailAsync(int payrollId, CancellationToken ct)
    {
        var payroll = await _repo.FindPayrollByIdAsync(payrollId, includePayslips: true, ct);
        if (payroll is null)
            return Result<PayrollDetailDto>.Failure(ErrorCodes.PayrollNotFound, Messages.PayrollNotFound,
                ResultKind.NotFound);

        var file = (await _repo.GetImportFilesAsync([payroll.PayrollId], ct)).GetValueOrDefault(payroll.PayrollId);
        var reasons = await _repo.GetReopenReasonsAsync([payroll.PayrollId], ct);
        return Result<PayrollDetailDto>.Success(ToDetailDto(payroll, file, reasons.GetValueOrDefault(payroll.PayrollId)));
    }

    public async Task<Result> ApproveAsync(int payrollId, CancellationToken ct)
    {
        var payroll = await _repo.FindPayrollByIdAsync(payrollId, includePayslips: false, ct);
        if (payroll is null)
            return Result.Failure(ErrorCodes.PayrollNotFound, Messages.PayrollNotFound, ResultKind.NotFound);

        if (payroll.Status != PayrollStatus.PendingApproval)
            return Result.Failure(ErrorCodes.PayrollInvalidStatus, Messages.PayrollInvalidStatusForApprove);

        // "Duyệt thì bảng lương bị khóa" = chốt tháng (UC31 cuối luồng): khóa luôn AccountingPeriod.
        // Một lần SaveChangesAsync ghi cả hai bảng trong cùng transaction (Rules mục 3.5/3.7).
        payroll.Status = PayrollStatus.Approved;
        payroll.ApprovedAt = _clock.Now;
        payroll.Period.Status = RecordStatus.Locked;
        payroll.Period.LockedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(Payroll), payroll.PayrollId, AuditActions.Approve,
            new { Status = PayrollStatus.PendingApproval }, new { payroll.Status }));
        _audit.Add(new AuditEntry(nameof(AccountingPeriod), payroll.PeriodId, AuditActions.Lock,
            new { Status = RecordStatus.Open }, new { payroll.Period.Status }));
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("Đã duyệt bảng lương {PayrollId}, khóa tháng {PeriodMonth}",
            payrollId, payroll.Period.PeriodMonth);
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> RejectAsync(int payrollId, RejectPayrollDto dto, CancellationToken ct)
    {
        var payroll = await _repo.FindPayrollByIdAsync(payrollId, includePayslips: false, ct);
        if (payroll is null)
            return Result.Failure(ErrorCodes.PayrollNotFound, Messages.PayrollNotFound, ResultKind.NotFound);

        if (payroll.Status != PayrollStatus.PendingApproval)
            return Result.Failure(ErrorCodes.PayrollInvalidStatus, Messages.PayrollInvalidStatusForReject);

        payroll.Status = PayrollStatus.Rejected;
        payroll.RejectReason = dto.Reason;
        _audit.Add(new AuditEntry(nameof(Payroll), payroll.PayrollId, AuditActions.Reject,
            new { Status = PayrollStatus.PendingApproval }, new { payroll.Status }, dto.Reason));
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("Đã từ chối bảng lương {PayrollId}", payrollId);
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> ReopenAsync(int payrollId, ReopenPayrollDto dto, CancellationToken ct)
    {
        var payroll = await _repo.FindPayrollByIdAsync(payrollId, includePayslips: false, ct);
        if (payroll is null)
            return Result.Failure(ErrorCodes.PayrollNotFound, Messages.PayrollNotFound, ResultKind.NotFound);

        if (payroll.Status != PayrollStatus.Approved)
            return Result.Failure(ErrorCodes.PayrollInvalidStatus, Messages.PayrollInvalidStatusForReopen);

        // DEV-MI-09-2: SRS chỉ nói "mở lại về Nháp, bắt buộc lý do"; khóa tháng gắn liền với duyệt
        // (xem ApproveAsync) nên suy luận mở lại cũng mở khóa tháng — xem docs/CODE_DEVIATIONS.md.
        payroll.Status = PayrollStatus.Draft;
        payroll.ApprovedAt = null;
        payroll.Period.Status = RecordStatus.Open;
        payroll.Period.LockedAt = null;
        _audit.Add(new AuditEntry(nameof(Payroll), payroll.PayrollId, AuditActions.Unlock,
            new { Status = PayrollStatus.Approved }, new { payroll.Status }, dto.Reason));   // lý do mở lại đọc từ AuditLog
        _audit.Add(new AuditEntry(nameof(AccountingPeriod), payroll.PeriodId, AuditActions.Unlock,
            new { Status = RecordStatus.Locked }, new { payroll.Period.Status }, dto.Reason));
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("Đã mở lại bảng lương {PayrollId} về Nháp, mở khóa tháng {PeriodMonth}",
            payrollId, payroll.Period.PeriodMonth);
        return Result.Success(Messages.Saved);
    }

    // ---------- Công nhân: xem phiếu lương của mình (UC06) ----------

    public async Task<Result<IReadOnlyList<MyPayslipSummaryDto>>> GetMyPayslipsAsync(int employeeId, CancellationToken ct)
    {
        var rows = await _repo.GetMyPayslipSummariesAsync(employeeId, ct);
        var dtos = rows.Select(r => new MyPayslipSummaryDto(r.PeriodMonth, r.NetPay, r.ApprovedAt)).ToList();

        return Result<IReadOnlyList<MyPayslipSummaryDto>>.Success(dtos, dtos.Count == 0 ? Messages.NoData : null);
    }

    public async Task<Result<MyPayslipDetailDto>> GetMyPayslipAsync(int employeeId, string periodMonth, CancellationToken ct)
    {
        if (!IsValidMonth(periodMonth))
            return Result<MyPayslipDetailDto>.Failure(ErrorCodes.PayrollPeriodMonthInvalid,
                Messages.PayrollPeriodMonthInvalid, ResultKind.Validation);

        var found = await _repo.FindMyPayslipAsync(employeeId, periodMonth, ct);
        if (found is null)
            return Result<MyPayslipDetailDto>.Failure(ErrorCodes.PayslipNotFound, Messages.PayslipNotFound,
                ResultKind.NotFound);

        var (payslip, approvedAt) = found.Value;
        var items = OrderForDisplay(payslip.PayslipItems);

        return Result<MyPayslipDetailDto>.Success(new MyPayslipDetailDto(
            periodMonth, payslip.WorkDays, payslip.TotalAdditions, payslip.TotalDeductions, payslip.Insurance,
            payslip.NetPay, approvedAt, items));
    }

    // ---------- Dùng chung ----------

    private static bool IsValidMonth(string periodMonth) => PeriodMonthPattern.IsMatch(periodMonth);

    /// <summary>"PendingApproval" (FE gửi lại đúng giá trị PascalCase đã nhận) → "PENDING_APPROVAL" (DB).
    /// Giá trị lạ/không khớp giữ nguyên để lọc ra danh sách rỗng thay vì ném lỗi.</summary>
    private static string? ToRawStatus(string? status) => status switch
    {
        null or "" => null,
        nameof(PayrollStatus.Draft) => PayrollStatus.Draft,
        "PendingApproval" => PayrollStatus.PendingApproval,
        nameof(PayrollStatus.Approved) => PayrollStatus.Approved,
        nameof(PayrollStatus.Rejected) => PayrollStatus.Rejected,
        _ => status,
    };

    private async Task<List<PayrollSummaryDto>> ToSummariesAsync(IReadOnlyCollection<Payroll> payrolls, CancellationToken ct)
    {
        var ids = payrolls.Select(p => p.PayrollId).ToArray();
        var files = await _repo.GetImportFilesAsync(ids, ct);
        var reasons = await _repo.GetReopenReasonsAsync(ids, ct);
        return payrolls.Select(p =>
        {
            var file = files.GetValueOrDefault(p.PayrollId);
            return new PayrollSummaryDto(
                p.PayrollId, p.PeriodId, p.Period.PeriodMonth, CodeText.ToPascal(p.Status),
                file is not null, file?.ImportedAt,
                p.Payslips.Sum(s => s.NetPay), p.Payslips.Count, p.SubmittedAt, p.ApprovedAt, p.RejectReason,
                reasons.GetValueOrDefault(p.PayrollId));
        }).ToList();
    }

    private static PayslipItemDto ToItemDto(PayslipItem i)
    {
        var view = PayslipLayout.Describe(i.ItemGroup, i.ItemName);
        return new PayslipItemDto(CodeText.ToPascal(i.ItemGroup), view.DisplayName, i.Amount, i.SortOrder,
            view.Section, view.Kind.ToString(), view.TextValue);
    }

    /// <summary>Khoản của phiếu theo thứ tự nhóm trên mẫu phiếu lương, trong nhóm theo thứ tự cột trong file.</summary>
    private static List<PayslipItemDto> OrderForDisplay(IEnumerable<PayslipItem> items) =>
        items.OrderBy(i => i.SortOrder).Select(ToItemDto)
            .OrderBy(i => PayslipLayout.SectionOrder(i.Section))
            .ToList();   // OrderBy ổn định nên giữ nguyên thứ tự cột trong nhóm

    private static PayslipDto ToPayslipDto(Payslip s) => new(
        s.PayslipId, s.EmployeeId, s.Employee.EmployeeCode, s.Employee.FullName, s.Employee.Team?.TeamName,
        s.WorkDays, s.TotalAdditions, s.TotalDeductions, s.Insurance, s.NetPay,
        OrderForDisplay(s.PayslipItems));

    private static PayrollDetailDto ToDetailDto(Payroll p, PayrollImportFile? file, string? reopenReason) => new(
        p.PayrollId, p.PeriodId, p.Period.PeriodMonth, CodeText.ToPascal(p.Status),
        file is not null, p.Payslips.Sum(s => s.NetPay),
        p.SubmittedAt, p.ApprovedAt, p.RejectReason, reopenReason,
        p.Payslips.OrderBy(s => s.Employee.EmployeeCode).Select(ToPayslipDto).ToList());

    /// <summary>MSG24: {a} dòng hợp lệ, {b} dòng lỗi, {c} dòng trùng.</summary>
    private static string BuildImportMessage(int valid, int error, int duplicate) =>
        $"{valid} dòng hợp lệ, {error} dòng lỗi, {duplicate} dòng trùng. Chỉ các dòng hợp lệ được nhập.";

    /// <summary>Parse file Excel; file sai cấu trúc trả về false kèm mã lỗi để Service dựng 400.</summary>
    private bool TryParseExcel(Stream stream, out IReadOnlyList<PayrollExcelRowDto>? rows,
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
            errorCode = ErrorCodes.PayrollExcelInvalid;
            errorMessage = ex.Message;
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            _logger.LogWarning(ex, "Payroll Excel parse failed");
            errorCode = ErrorCodes.PayrollExcelInvalid;
            errorMessage = Messages.PayrollExcelInvalid;
            return false;
        }
    }

    private async Task<EmployeeLookup> LookupEmployeesAsync(IReadOnlyList<PayrollExcelRowDto> rawRows, CancellationToken ct)
    {
        var codes = rawRows.Where(r => r.ParseError is null && !string.IsNullOrWhiteSpace(r.AttendanceCode))
            .Select(r => r.AttendanceCode!)
            .Distinct()
            .ToList();
        return await _repo.GetEmployeesByAttendanceCodesAsync(codes, ct);
    }

    private async Task TryDeleteFileAsync(string storedPath, CancellationToken ct)
    {
        try
        {
            await _fileStore.DeleteAsync(storedPath, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Không xóa được file bảng lương {StoredPath}", storedPath);
        }
    }

    private sealed record EmployeeGroup(
        int EmployeeId, string AttendanceCode, decimal? WorkDays, decimal NetPay,
        List<(string Group, string Name, decimal Amount)> Items);

    /// <summary>Kiểm tra nghiệp vụ từng dòng nhân viên đọc từ file: tra theo mã chấm công, nhân viên còn làm,
    /// không trùng SLĐ trong file, có khoản lương. Dòng hợp lệ được gộp lại để ghi; sai lệch Thực lĩnh và bậc thợ chỉ
    /// cảnh báo (không chặn) vì file Excel làm tròn từng ô và bậc có thể đổi trong tháng.</summary>
    private static (List<PayrollExcelRowPreviewDto> Previews, int Valid, int Error, int Duplicate, List<EmployeeGroup> Groups)
        ValidateRows(IReadOnlyList<PayrollExcelRowDto> rawRows, EmployeeLookup employees)
    {
        var previews = new List<PayrollExcelRowPreviewDto>(rawRows.Count);
        var groups = new List<EmployeeGroup>();
        var seenCodes = new HashSet<string>();
        int valid = 0, error = 0, duplicate = 0;

        foreach (var row in rawRows)
        {
            if (row.ParseError is not null)
            {
                previews.Add(ToPreview(row, null, PayrollRowStatus.Error, row.ParseError));
                error++;
                continue;
            }

            var code = row.AttendanceCode!;
            if (!employees.TryGetValue(code, out var employee))
            {
                previews.Add(ToPreview(row, null, PayrollRowStatus.Error,
                    $"Mã chấm công {code} ({row.FullName}) không có trong hệ thống."));
                error++;
                continue;
            }

            if (employee.Status != RecordStatus.Active)
            {
                previews.Add(ToPreview(row, employee.FullName, PayrollRowStatus.Error,
                    $"Nhân viên mã chấm công {code} đã nghỉ việc."));
                error++;
                continue;
            }

            if (row.NetPay is null)
            {
                previews.Add(ToPreview(row, employee.FullName, PayrollRowStatus.Error, "Thiếu thực lĩnh."));
                error++;
                continue;
            }

            if (row.WorkDays is < 0 or > 31)
            {
                previews.Add(ToPreview(row, employee.FullName, PayrollRowStatus.Error, "Số công không hợp lệ (0-31)."));
                error++;
                continue;
            }

            if (row.Items.Count == 0)
            {
                previews.Add(ToPreview(row, employee.FullName, PayrollRowStatus.Error,
                    "Dòng không có khoản lương nào lớn hơn 0."));
                error++;
                continue;
            }

            if (!seenCodes.Add(code))
            {
                previews.Add(ToPreview(row, employee.FullName, PayrollRowStatus.Duplicate,
                    "Dòng trùng (mã chấm công đã có ở dòng/sheet trước, bỏ qua dòng này)."));
                duplicate++;
                continue;
            }

            groups.Add(new EmployeeGroup(employee.EmployeeId, code, row.WorkDays, row.NetPay.Value,
                row.Items.Select(i => (i.Group, i.Name, i.Amount)).ToList()));
            previews.Add(ToPreview(row, employee.FullName, PayrollRowStatus.Valid,
                BuildWarnings(row, employee.GradeCode)));
            valid++;
        }

        return (previews, valid, error, duplicate, groups);
    }

    /// <summary>Cảnh báo không chặn: tổng các khoản (cộng − trừ − BHXH) lệch Thực lĩnh trong file hoặc bậc thợ trong
    /// file khác bậc hiện tại của nhân viên. Null khi không có gì đáng chú ý.</summary>
    private static string? BuildWarnings(PayrollExcelRowDto row, string? systemGrade)
    {
        var warnings = new List<string>();

        var sum = row.Items.Where(i => i.Group == PayslipItemGroup.Addition).Sum(i => i.Amount)
                  - row.Items.Where(i => i.Group == PayslipItemGroup.Deduction).Sum(i => i.Amount)
                  - row.Items.Where(i => i.Group == PayslipItemGroup.Insurance).Sum(i => i.Amount);
        if (Math.Abs(sum - row.NetPay!.Value) > NetPayTolerance)
            warnings.Add(
                $"Tổng các khoản ({FormatMoney(sum)}) lệch Thực lĩnh trong file ({FormatMoney(row.NetPay.Value)}) — " +
                "kiểm tra lại file hoặc cột chưa được nhận diện.");

        if (row.GradeCode is not null && systemGrade is not null
            && !string.Equals(row.GradeCode, systemGrade, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Bậc thợ trong file ({row.GradeCode}) khác bậc hiện tại trong hệ thống ({systemGrade}).");

        return warnings.Count == 0 ? null : string.Join(" ", warnings);
    }

    private static string FormatMoney(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static PayrollExcelRowPreviewDto ToPreview(PayrollExcelRowDto row, string? employeeName, string status,
        string? message) => new(
        row.SheetName, row.RowNumber, row.AttendanceCode, employeeName ?? row.FullName, row.WorkDays, row.NetPay,
        row.Items.Count, status, message);
}
