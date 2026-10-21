using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class PayrollServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0);
    private const string Month = "2026-10";

    private readonly IPayrollRepository _repo = Substitute.For<IPayrollRepository>();
    private readonly IExcelPayrollParser _excel = Substitute.For<IExcelPayrollParser>();
    private readonly IPayrollExcelWriter _writer = Substitute.For<IPayrollExcelWriter>();
    private readonly IPayrollFileStore _files = Substitute.For<IPayrollFileStore>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly PayrollService _sut;

    public PayrollServiceTests()
    {
        _clock.Now.Returns(Now);
        _files.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns("2026-10/new.xlsx");
        _sut = new PayrollService(_repo, _excel, _writer, _files, _audit, _uow, _clock, NullLogger<PayrollService>.Instance);
    }

    private static AccountingPeriod Period(int id = 1, string month = Month, string status = RecordStatus.Open) =>
        new()
        {
            PeriodId = id, PeriodMonth = month, Status = status,
            StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31),
        };

    private static Payroll Payroll(int id = 1, string status = PayrollStatus.Draft, AccountingPeriod? period = null) =>
        new() { PayrollId = id, PeriodId = period?.PeriodId ?? 1, Status = status, Period = period ?? Period() };

    private static PayrollExcelItemDto Item(string group, string name, decimal amount) => new(group, name, amount);

    /// <summary>Một dòng nhân viên: mã chấm công 25001, 26 công, thực lĩnh 5.000.000 = 6.000.000 − 500.000 − 500.000.</summary>
    private static PayrollExcelRowDto Row(int rowNumber = 8, string? code = "25001", decimal? netPay = 5_000_000,
        string? grade = "A", string? parseError = null, params PayrollExcelItemDto[] items) =>
        new("Tổ may 1", rowNumber, code, "Nguyễn Thị Thanh", grade, 26, netPay,
            items.Length > 0 || parseError is not null
                ? items
                :
                [
                    Item(PayslipItemGroup.Addition, "Tiểu tác thừa", 6_000_000),
                    Item(PayslipItemGroup.Deduction, "Thuế TNCN", 500_000),
                    Item(PayslipItemGroup.Insurance, "BHXH 10.5%", 500_000),
                ],
            parseError);

    private static IReadOnlyDictionary<string, (int EmployeeId, string FullName, string Status, string? GradeCode)>
        Employees(string? grade = "A") =>
        new Dictionary<string, (int, string, string, string?)>
        {
            ["25001"] = (1, "Nguyễn Thị Thanh", RecordStatus.Active, grade),
        };

    private void GivenEmployees(string? grade = "A") =>
        _repo.GetEmployeesByAttendanceCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Employees(grade));

    // ---------- ExportAsync (UC31) ----------

    [Fact]
    public async Task ExportAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.ExportAsync("2026-13", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task ExportAsync_Valid_BuildsFileFromExportRowsAndWarnings()
    {
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetExportRowsAsync(Month, Arg.Any<CancellationToken>()).Returns(new List<VwPayrollExportDatum>
        {
            new()
            {
                PeriodMonth = Month, EmployeeId = 1, EmployeeCode = "NV001", FullName = "Nguyễn Văn A",
                RoleName = "Công nhân", PayType = "PIECE_RATE", TeamName = "Tổ may 1", WorkDays = 26,
            },
        });
        _repo.GetMissingPunchDaysAsync(period.StartDate, period.EndDate, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, int> { [1] = 2 });
        _repo.GetDaysWithoutOutputAsync(period.StartDate, period.EndDate, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, int> { [1] = 3 });
        _writer.WriteExportSheet(Month, Arg.Any<IReadOnlyList<PayrollExportRowDto>>()).Returns(new byte[] { 1, 2, 3 });

        var result = await _sut.ExportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be("BangLuong_2026-10.xlsx");
        _writer.Received(1).WriteExportSheet(Month, Arg.Is<IReadOnlyList<PayrollExportRowDto>>(
            rows => rows.Single().MissingPunchDays == 2 && rows.Single().DaysWithoutOutput == 3));
    }

    // ---------- PreviewImportAsync / ImportAsync (UC31, DEV-MI-09-4) ----------

    [Fact]
    public async Task PreviewImportAsync_InvalidFile_ReturnsValidationFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(_ => throw new InvalidDataException("sai mẫu"));

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.PayrollExcelInvalid);
    }

    [Fact]
    public async Task PreviewImportAsync_EmptyFile_ReturnsFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto>());

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollExcelEmpty);
    }

    [Fact]
    public async Task PreviewImportAsync_UnknownAttendanceCode_MarksRowError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(code: "99999") });
        GivenEmployees();

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ErrorCount.Should().Be(1);
        result.Data.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Error);
        result.Data.Rows.Single().Message.Should().Contain("99999");
    }

    [Fact]
    public async Task PreviewImportAsync_ParseErrorRow_MarksRowError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto>
        {
            Row(parseError: "Sheet Tổ may 1 dòng 8: cột Thực lĩnh bị lỗi công thức (#REF!)."),
        });
        GivenEmployees();

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        result.Data!.ErrorCount.Should().Be(1);
        result.Data.Rows.Single().Message.Should().Contain("#REF!");
    }

    [Fact]
    public async Task PreviewImportAsync_SameAttendanceCodeTwice_MarksSecondDuplicate()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(8), Row(20) });
        GivenEmployees();

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        result.Data!.ValidCount.Should().Be(1);
        result.Data.DuplicateCount.Should().Be(1);
        result.Data.Rows.Last().RowStatus.Should().Be(PayrollRowStatus.Duplicate);
    }

    [Fact]
    public async Task PreviewImportAsync_ItemsDoNotMatchNetPay_StaysValidWithWarning()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(netPay: 7_000_000) });
        GivenEmployees();

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        var row = result.Data!.Rows.Single();
        row.RowStatus.Should().Be(PayrollRowStatus.Valid);
        row.Message.Should().Contain("lệch Thực lĩnh");
    }

    [Fact]
    public async Task PreviewImportAsync_RoundingWithinTolerance_HasNoWarning()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(netPay: 5_000_400) });
        GivenEmployees();

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        result.Data!.Rows.Single().Message.Should().BeNull();
    }

    [Fact]
    public async Task PreviewImportAsync_GradeDiffersFromSystem_StaysValidWithWarning()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(grade: "A") });
        GivenEmployees(grade: "B");

        var result = await _sut.PreviewImportAsync(new MemoryStream(), CancellationToken.None);

        var row = result.Data!.Rows.Single();
        row.RowStatus.Should().Be(PayrollRowStatus.Valid);
        row.Message.Should().Contain("Bậc thợ");
    }

    [Fact]
    public async Task ImportAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.ImportAsync(new MemoryStream(), "2026-13", CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task ImportAsync_PeriodLocked_ReturnsFailureAndSavesNoFile()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Period(status: RecordStatus.Locked));

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodLocked);
        result.Message.Should().Be(string.Format(Messages.AttendancePeriodLockedMessage, Month));
        await _files.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default);
    }

    [Fact]
    public async Task ImportAsync_ExistingApprovedPayroll_ReturnsInvalidStatusFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>())
            .Returns(Payroll(status: PayrollStatus.Approved, period: period));

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
        result.Message.Should().Be(Messages.PayrollInvalidStatusForImport);
    }

    [Fact]
    public async Task ImportAsync_ValidRows_CreatesDraftPayrollKeepsOriginalFileAndGroupsItems()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>())
            .Returns((Payroll?)null);

        Payroll? addedPayroll = null;
        _repo.When(r => r.AddPayroll(Arg.Any<Payroll>())).Do(c => addedPayroll = c.Arg<Payroll>());
        Payslip? addedPayslip = null;
        _repo.When(r => r.AddPayslip(Arg.Any<Payslip>())).Do(c => addedPayslip = c.Arg<Payslip>());

        var original = new byte[] { 1, 2, 3, 4 };
        var result = await _sut.ImportAsync(new MemoryStream(original), Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ValidCount.Should().Be(1);
        result.Data.PayslipCount.Should().Be(1);
        result.Data.TotalAmount.Should().Be(5_000_000);

        addedPayroll.Should().NotBeNull();
        addedPayroll!.Status.Should().Be(PayrollStatus.Draft);
        addedPayroll.PeriodId.Should().Be(period.PeriodId);
        addedPayroll.ImportedFileUrl.Should().Be("2026-10/new.xlsx");
        await _files.Received(1).SaveAsync(Month, Arg.Is<byte[]>(b => b.SequenceEqual(original)),
            Arg.Any<CancellationToken>());

        addedPayslip.Should().NotBeNull();
        addedPayslip!.EmployeeId.Should().Be(1);
        addedPayslip.WorkDays.Should().Be(26);
        addedPayslip.NetPay.Should().Be(5_000_000);
        addedPayslip.TotalAdditions.Should().Be(6_000_000);
        addedPayslip.TotalDeductions.Should().Be(500_000);
        addedPayslip.Insurance.Should().Be(500_000);
        addedPayslip.PayslipItems.Should().HaveCount(3);

        // Lưu bảng lương (lấy PayrollId) rồi lưu AuditLog, cùng một transaction.
        await _repo.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_ReimportingRejectedPayroll_ReplacesPayslipsAndSwapsOriginalFile()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        var existing = Payroll(status: PayrollStatus.Rejected, period: period);
        existing.RejectReason = "Sai số công";
        existing.ImportedFileUrl = "2026-10/old.xlsx";
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).RemovePayslipsAsync(existing.PayrollId, Arg.Any<CancellationToken>());
        existing.Status.Should().Be(PayrollStatus.Draft);
        existing.RejectReason.Should().BeNull();
        existing.ImportedFileUrl.Should().Be("2026-10/new.xlsx");
        await _files.Received(1).DeleteAsync("2026-10/old.xlsx", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_SaveChangesFails_DeletesNewFileAndKeepsOldOne()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        var existing = Payroll(period: period);
        existing.ImportedFileUrl = "2026-10/old.xlsx";
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>()).Returns(existing);
        _repo.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("db"));

        var act = () => _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _files.Received(1).DeleteAsync("2026-10/new.xlsx", Arg.Any<CancellationToken>());
        await _files.DidNotReceive().DeleteAsync("2026-10/old.xlsx", Arg.Any<CancellationToken>());
    }

    // ---------- GetImportedFileAsync (DEV-MI-09-5) ----------

    [Fact]
    public async Task GetImportedFileAsync_PayrollMissing_ReturnsNotFound()
    {
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns((Payroll?)null);

        var result = await _sut.GetImportedFileAsync(1, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task GetImportedFileAsync_NoStoredFile_ReturnsFileNotFound()
    {
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(Payroll());

        var result = await _sut.GetImportedFileAsync(1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollFileNotFound);
    }

    [Fact]
    public async Task GetImportedFileAsync_StoredFile_ReturnsContentWithPeriodInName()
    {
        var payroll = Payroll();
        payroll.ImportedFileUrl = "2026-10/abc.xlsx";
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);
        _files.ReadAsync("2026-10/abc.xlsx", Arg.Any<CancellationToken>()).Returns(new byte[] { 9, 9 });

        var result = await _sut.GetImportedFileAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Content.Should().Equal(9, 9);
        result.Data.FileName.Should().Be("BangThanhToanLuong_2026-10.xlsx");
    }

    // ---------- SubmitAsync ----------

    [Fact]
    public async Task SubmitAsync_NotDraft_ReturnsFailure()
    {
        var payroll = Payroll(status: PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(1, true, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.SubmitAsync(1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task SubmitAsync_NoPayslips_ReturnsFailure()
    {
        var payroll = Payroll();
        _repo.FindPayrollByIdAsync(1, true, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.SubmitAsync(1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollEmpty);
    }

    [Fact]
    public async Task SubmitAsync_Valid_SetsPendingApprovalAndSubmittedAt()
    {
        var payroll = Payroll();
        payroll.Payslips.Add(new Payslip { EmployeeId = 1, NetPay = 1_000_000 });
        _repo.FindPayrollByIdAsync(1, true, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.SubmitAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.PendingApproval);
        payroll.SubmittedAt.Should().Be(Now);
    }

    // ---------- ApproveAsync / RejectAsync / ReopenAsync (UC32) ----------

    [Fact]
    public async Task ApproveAsync_NotPending_ReturnsFailure()
    {
        var payroll = Payroll();
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.ApproveAsync(1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task ApproveAsync_Valid_ApprovesPayrollAndLocksPeriod()
    {
        var payroll = Payroll(status: PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.ApproveAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.Approved);
        payroll.ApprovedAt.Should().Be(Now);
        payroll.Period.Status.Should().Be(RecordStatus.Locked);
        payroll.Period.LockedAt.Should().Be(Now);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectAsync_NotPending_ReturnsFailure()
    {
        var payroll = Payroll();
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.RejectAsync(1, new RejectPayrollDto("Sai dữ liệu"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task RejectAsync_Valid_SetsRejectedWithReason()
    {
        var payroll = Payroll(status: PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.RejectAsync(1, new RejectPayrollDto("Sai số công"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.Rejected);
        payroll.RejectReason.Should().Be("Sai số công");
    }

    [Fact]
    public async Task ReopenAsync_NotApproved_ReturnsFailure()
    {
        var payroll = Payroll();
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.ReopenAsync(1, new ReopenPayrollDto("Duyệt nhầm"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task ReopenAsync_Valid_SetsDraftAndUnlocksPeriod()
    {
        var period = Period(status: RecordStatus.Locked);
        period.LockedAt = Now.AddDays(-1);
        var payroll = Payroll(status: PayrollStatus.Approved, period: period);
        payroll.ApprovedAt = Now.AddDays(-1);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        var result = await _sut.ReopenAsync(1, new ReopenPayrollDto("Duyệt nhầm số công"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.Draft);
        payroll.ReopenReason.Should().Be("Duyệt nhầm số công");
        payroll.ApprovedAt.Should().BeNull();
        payroll.Period.Status.Should().Be(RecordStatus.Open);
        payroll.Period.LockedAt.Should().BeNull();
    }

    // ---------- AuditLog (GBR-15, DEV-MI-09-3) ----------

    private static bool Logged(AuditEntry e, string table, string action) => e.TableName == table && e.Action == action;

    [Fact]
    public async Task ImportAsync_NewPayroll_LogsInsertAfterGettingPayrollId()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>()).Returns((Payroll?)null);
        _repo.When(r => r.AddPayroll(Arg.Any<Payroll>())).Do(c => c.Arg<Payroll>().PayrollId = 77);

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            Logged(e, nameof(Payroll), AuditActions.Insert) && e.RecordId == 77 && e.OldValues == null));
        await _repo.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());   // lưu bảng lương, rồi lưu log
    }

    [Fact]
    public async Task ImportAsync_ReimportingExistingPayroll_LogsUpdateWithOldValues()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        var existing = Payroll(status: PayrollStatus.Rejected, period: period);
        existing.TotalAmount = 123;
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => Logged(e, nameof(Payroll), AuditActions.Update) && e.OldValues != null));
    }

    [Fact]
    public async Task SubmitAsync_Valid_LogsStatusChange()
    {
        var payroll = Payroll();
        payroll.Payslips.Add(new Payslip { EmployeeId = 1, NetPay = 1_000_000 });
        _repo.FindPayrollByIdAsync(1, true, Arg.Any<CancellationToken>()).Returns(payroll);

        await _sut.SubmitAsync(1, CancellationToken.None);

        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => Logged(e, nameof(Payroll), AuditActions.Update)));
    }

    [Fact]
    public async Task ApproveAsync_Valid_LogsApproveAndPeriodLock()
    {
        var payroll = Payroll(status: PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        await _sut.ApproveAsync(1, CancellationToken.None);

        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => Logged(e, nameof(Payroll), AuditActions.Approve)));
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => Logged(e, nameof(AccountingPeriod), AuditActions.Lock)));
    }

    [Fact]
    public async Task RejectAsync_Valid_LogsRejectWithReason()
    {
        var payroll = Payroll(status: PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        await _sut.RejectAsync(1, new RejectPayrollDto("Sai số công"), CancellationToken.None);

        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            Logged(e, nameof(Payroll), AuditActions.Reject) && e.Reason == "Sai số công"));
    }

    [Fact]
    public async Task ReopenAsync_Valid_LogsUpdateAndPeriodUnlockWithReason()
    {
        var period = Period(status: RecordStatus.Locked);
        var payroll = Payroll(status: PayrollStatus.Approved, period: period);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        await _sut.ReopenAsync(1, new ReopenPayrollDto("Duyệt nhầm số công"), CancellationToken.None);

        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            Logged(e, nameof(Payroll), AuditActions.Update) && e.Reason == "Duyệt nhầm số công"));
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            Logged(e, nameof(AccountingPeriod), AuditActions.Unlock) && e.Reason == "Duyệt nhầm số công"));
    }

    [Fact]
    public async Task ApproveAsync_NotPending_LogsNothing()
    {
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(Payroll());

        await _sut.ApproveAsync(1, CancellationToken.None);

        _audit.DidNotReceiveWithAnyArgs().Add(default!);
    }

    // ---------- Công nhân xem phiếu lương (UC06) ----------

    [Fact]
    public async Task GetMyPayslipAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.GetMyPayslipAsync(1, "2026-13", CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task GetMyPayslipAsync_NotApprovedOrMissing_ReturnsNotFound()
    {
        _repo.FindMyPayslipAsync(1, Month, Arg.Any<CancellationToken>())
            .Returns(((Payslip, DateTime)?)null);

        var result = await _sut.GetMyPayslipAsync(1, Month, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.PayslipNotFound);
    }

    [Fact]
    public async Task GetMyPayslipAsync_Approved_ReturnsDetailWithItemsOrderedBySortOrder()
    {
        var payslip = new Payslip
        {
            PayslipId = 10, EmployeeId = 1, WorkDays = 26, TotalAdditions = 6000000, TotalDeductions = 500000,
            Insurance = 500000, NetPay = 5000000,
            PayslipItems = new List<PayslipItem>
            {
                new() { ItemGroup = PayslipItemGroup.Deduction, ItemName = "Hàng hỏng", Amount = 500000, SortOrder = 1 },
                new() { ItemGroup = PayslipItemGroup.Addition, ItemName = "Lương sản phẩm", Amount = 6000000, SortOrder = 0 },
            },
        };
        _repo.FindMyPayslipAsync(1, Month, Arg.Any<CancellationToken>()).Returns((payslip, Now));

        var result = await _sut.GetMyPayslipAsync(1, Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.NetPay.Should().Be(5000000);
        result.Data.Items.Should().HaveCount(2);
        result.Data.Items[0].ItemName.Should().Be("Lương sản phẩm");
        result.Data.Items[0].ItemGroup.Should().Be("Addition");
    }
}

public class PayrollValidatorTests
{
    private readonly RejectPayrollDtoValidator _reject = new();
    private readonly ReopenPayrollDtoValidator _reopen = new();

    [Fact]
    public void Reject_EmptyReason_HasError() =>
        _reject.TestValidate(new RejectPayrollDto("")).ShouldHaveValidationErrorFor(x => x.Reason);

    [Fact]
    public void Reject_ValidReason_HasNoError() =>
        _reject.TestValidate(new RejectPayrollDto("Sai số công")).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Reopen_EmptyReason_HasError() =>
        _reopen.TestValidate(new ReopenPayrollDto("")).ShouldHaveValidationErrorFor(x => x.Reason);

    [Fact]
    public void Reopen_ValidReason_HasNoError() =>
        _reopen.TestValidate(new ReopenPayrollDto("Duyệt nhầm")).ShouldNotHaveAnyValidationErrors();
}
