using FluentAssertions;
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

/// <summary>UTCID135–UTCID203: PayrollService per UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES.md.</summary>
public class PayrollServiceHighComplexityTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0);
    private const string Month = "2026-10";

    private readonly IPayrollRepository _repo = Substitute.For<IPayrollRepository>();
    private readonly IExcelPayrollParser _excel = Substitute.For<IExcelPayrollParser>();
    private readonly IPayrollExcelWriter _writer = Substitute.For<IPayrollExcelWriter>();
    private readonly IPayrollFileStore _files = Substitute.For<IPayrollFileStore>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly PayrollService _sut;

    public PayrollServiceHighComplexityTests()
    {
        _clock.Now.Returns(Now);
        _files.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns("2026-10/new.xlsx");
        _repo.GetImportFilesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, PayrollImportFile>());
        _repo.GetReopenReasonsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, string>());
        _repo.RemoveImportFilesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<string>());
        _sut = new PayrollService(_repo, _excel, _writer, _files, _audit, _uow, _clock, NullLogger<PayrollService>.Instance);
    }

    private static AccountingPeriod Period(int id = 1, string month = Month, string status = RecordStatus.Open) =>
        new()
        {
            PeriodId = id, PeriodMonth = month, Status = status,
            StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31),
        };

    private static Payroll PayrollEntity(int id = 1, string status = PayrollStatus.Draft, AccountingPeriod? period = null) =>
        new() { PayrollId = id, PeriodId = period?.PeriodId ?? 1, Status = status, Period = period ?? Period() };

    private static PayrollExcelItemDto Item(string group, string name, decimal amount) => new(group, name, amount);

    private static PayrollExcelRowDto Row(int rowNumber = 8, string? code = "25001", decimal? netPay = 5_000_000,
        decimal? workDays = 26, string? grade = "A", string? parseError = null, params PayrollExcelItemDto[] items) =>
        new("MAY1", rowNumber, code, "Nguyễn Thị Thanh", grade, workDays, netPay,
            items.Length > 0 || parseError is not null
                ? items
                :
                [
                    Item(PayslipItemGroup.Addition, "Tiểu tác thừa", 6_000_000),
                    Item(PayslipItemGroup.Deduction, "Thuế TNCN", 500_000),
                    Item(PayslipItemGroup.Insurance, "BHXH 10.5%", 500_000),
                ],
            parseError);

    private void GivenEmployees(params (string Code, int Id)[] rows)
    {
        IReadOnlyDictionary<string, (int EmployeeId, string FullName, string Status, string? GradeCode)> map =
            rows.Length == 0
                ? new Dictionary<string, (int, string, string, string?)>
                {
                    ["25001"] = (1, "Nguyễn Thị Thanh", RecordStatus.Active, "A"),
                    ["NV001"] = (1, "NV001", RecordStatus.Active, "A"),
                    ["NV002"] = (2, "NV002", RecordStatus.Active, "A"),
                    ["NV003"] = (3, "NV003", RecordStatus.Active, "A"),
                }
                : rows.ToDictionary(r => r.Code, r => (r.Id, "NV " + r.Code, RecordStatus.Active, (string?)"A"));
        if (rows.Length > 0)
        {
            var mutable = map.ToDictionary(kv => kv.Key, kv => kv.Value);
            mutable["NV002"] = mutable.GetValueOrDefault("NV002", (2, "NV002", RecordStatus.Active, "A"));
            mutable["NV003"] = mutable.GetValueOrDefault("NV003", (3, "NV003", RecordStatus.Active, "A"));
            map = mutable;
        }
        _repo.GetEmployeesByAttendanceCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(map);
    }

    [Fact]
    public async Task UTCID135_ExportAsync_ExistingPeriod_ReturnsExcelFile()
    {
        // Arrange
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetExportRowsAsync(Month, Arg.Any<CancellationToken>()).Returns(Array.Empty<VwPayrollExportDatum>());
        _repo.GetMissingPunchDaysAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, int>());
        _repo.GetDaysWithoutOutputAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, int>());
        _writer.WriteExportSheet(Month, Arg.Any<IReadOnlyList<PayrollExportRowDto>>()).Returns(new byte[] { 1, 2 });

        // Act
        var result = await _sut.ExportAsync(Month, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Contain("2026-10");
        result.Data.Content.Length.Should().BeGreaterThan(0);
        _writer.Received(1).WriteExportSheet(Month, Arg.Any<IReadOnlyList<PayrollExportRowDto>>());
    }

    [Fact]
    public async Task UTCID136_ExportAsync_NewPeriod_GetOrCreateCalled()
    {
        // Arrange
        _repo.GetOrCreatePeriodAsync("2026-12", Arg.Any<CancellationToken>()).Returns(Period(month: "2026-12"));
        _repo.GetExportRowsAsync("2026-12", Arg.Any<CancellationToken>()).Returns(Array.Empty<VwPayrollExportDatum>());
        _repo.GetMissingPunchDaysAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, int>());
        _repo.GetDaysWithoutOutputAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, int>());
        _writer.WriteExportSheet("2026-12", Arg.Any<IReadOnlyList<PayrollExportRowDto>>()).Returns(new byte[] { 1 });

        // Act
        var result = await _sut.ExportAsync("2026-12", default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).GetOrCreatePeriodAsync("2026-12", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID137_ExportAsync_LowerBoundaryMonth_Succeeds()
    {
        // Arrange
        _repo.GetOrCreatePeriodAsync("2026-01", Arg.Any<CancellationToken>()).Returns(Period(month: "2026-01"));
        _repo.GetExportRowsAsync("2026-01", Arg.Any<CancellationToken>()).Returns(Array.Empty<VwPayrollExportDatum>());
        _repo.GetMissingPunchDaysAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, int>());
        _repo.GetDaysWithoutOutputAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, int>());
        _writer.WriteExportSheet("2026-01", Arg.Any<IReadOnlyList<PayrollExportRowDto>>()).Returns(new byte[] { 1 });

        // Act
        var result = await _sut.ExportAsync("2026-01", default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID138_ExportAsync_Month13_Failure()
    {
        // Act
        var result = await _sut.ExportAsync("2026-13", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().GetOrCreatePeriodAsync(default!, default);
    }

    [Fact]
    public async Task UTCID139_ExportAsync_Month00_Failure()
    {
        // Act
        var result = await _sut.ExportAsync("2026-00", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task UTCID140_ExportAsync_EmptyMonth_Failure()
    {
        // Act
        var result = await _sut.ExportAsync("", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task UTCID141_ExportAsync_NullMonth_FailureNoException()
    {
        // Act
        var result = await _sut.ExportAsync(null!, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task UTCID142_ExportAsync_WrongSeparator_Failure()
    {
        // Act
        var result = await _sut.ExportAsync("2026/10", default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task UTCID143_PreviewImportAsync_ThreeValidRows_Succeeds()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(code: "NV001"), Row(20, "NV002"), Row(30, "NV003") });
        GivenEmployees(("NV001", 1), ("NV002", 2), ("NV003", 3));

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ValidCount.Should().Be(3);
        result.Data.Rows.Should().OnlyContain(r => r.RowStatus == PayrollRowStatus.Valid);
    }

    [Fact]
    public async Task UTCID144_PreviewImportAsync_ZeroRows_FailureEmpty()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto>());

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollExcelEmpty);
    }

    [Fact]
    public async Task UTCID145_PreviewImportAsync_WorkDaysZero_Valid()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(workDays: 0) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Valid);
    }

    [Fact]
    public async Task UTCID146_PreviewImportAsync_WorkDays31_Valid()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(workDays: 31) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Valid);
    }

    [Fact]
    public async Task UTCID147_PreviewImportAsync_WorkDays32_Error()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(workDays: 32) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Error);
    }

    [Fact]
    public async Task UTCID148_PreviewImportAsync_WorkDaysNegative_Error()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(workDays: -1) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Error);
    }

    [Fact]
    public async Task UTCID149_PreviewImportAsync_WorkDaysNull_Error()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(workDays: null) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Error);
    }

    [Fact]
    public async Task UTCID150_PreviewImportAsync_NetPayWithinTolerance_NoWarning()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(netPay: 5_000_000) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().Message.Should().BeNull();
    }

    [Fact]
    public async Task UTCID151_PreviewImportAsync_NetPayOffBy1001_Warning()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(netPay: 5_001_001) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Valid);
        result.Data.Rows.Single().Message.Should().Contain("lệch Thực lĩnh");
    }

    [Fact]
    public async Task UTCID152_PreviewImportAsync_DuplicateAttendanceCode_MarksDuplicate()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(8, "NV001"), Row(20, "NV001") });
        GivenEmployees(("NV001", 1));

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.DuplicateCount.Should().Be(1);
        result.Data.Rows.Last().RowStatus.Should().Be(PayrollRowStatus.Duplicate);
    }

    [Fact]
    public async Task UTCID153_PreviewImportAsync_ParserThrows_FailureExcelInvalid()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(_ => throw new InvalidDataException("bad"));

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollExcelInvalid);
    }

    [Fact]
    public async Task UTCID154_PreviewImportAsync_NetPayNull_Error()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(netPay: null) });
        GivenEmployees();

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Error);
    }

    [Fact]
    public async Task UTCID155_PreviewImportAsync_EmptyItems_Error()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto>
        {
            new("MAY1", 8, "NV001", "A", "A", 26, 1_000_000, [], null),
        });
        GivenEmployees(("NV001", 1));

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Error);
    }

    [Fact]
    public async Task UTCID156_PreviewImportAsync_UnknownEmployee_Error()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(code: "NV999") });
        _repo.GetEmployeesByAttendanceCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (int, string, string, string?)>());

        // Act
        var result = await _sut.PreviewImportAsync(new MemoryStream(), default);

        // Assert
        result.Data!.Rows.Single().RowStatus.Should().Be(PayrollRowStatus.Error);
        result.Data.Rows.Single().Message.Should().Contain("NV999");
    }

    [Fact]
    public async Task UTCID157_ImportAsync_DraftWithOldFile_ReplacesAndDeletesOld()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(code: "NV001"), Row(20, "NV002"), Row(30, "NV003") });
        GivenEmployees(("NV001", 1), ("NV002", 2), ("NV003", 3));
        var period = Period();
        var existing = PayrollEntity(period: period);
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>()).Returns(existing);
        _repo.RemoveImportFilesAsync(existing.PayrollId, Arg.Any<CancellationToken>()).Returns(new List<string> { "old.xlsx" });

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(new byte[] { 9 }), Month, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ValidCount.Should().Be(3);
        await _files.Received(1).SaveAsync(Month, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await _files.Received(1).DeleteAsync("old.xlsx", Arg.Any<CancellationToken>());
        await _repo.Received(1).RemovePayslipsAsync(existing.PayrollId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID158_ImportAsync_RejectedStatus_Succeeds()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>())
            .Returns(PayrollEntity(status: PayrollStatus.Rejected, period: period));

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID159_ImportAsync_MixedValidError_ReturnsCounts()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(code: "NV001"), Row(20, "NV002"), Row(30, "BAD", workDays: 32) });
        GivenEmployees(("NV001", 1), ("NV002", 2));
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>()).Returns((Payroll?)null);

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ValidCount.Should().Be(2);
        result.Data.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task UTCID160_ImportAsync_AllError_NoFileStore()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row(workDays: 32) });
        GivenEmployees();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(Period());

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _files.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default);
    }

    [Fact]
    public async Task UTCID161_ImportAsync_PeriodLocked_FailureNoFile()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(Period(status: RecordStatus.Locked));

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodLocked);
        await _files.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default);
    }

    [Fact]
    public async Task UTCID162_ImportAsync_PendingApproval_Failure()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>())
            .Returns(PayrollEntity(status: PayrollStatus.PendingApproval, period: period));

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task UTCID163_ImportAsync_Approved_Failure()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>())
            .Returns(PayrollEntity(status: PayrollStatus.Approved, period: period));

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task UTCID164_ImportAsync_InvalidMonthFormat_Failure()
    {
        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), "10-2026", default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task UTCID165_ImportAsync_SaveFails_DeletesNewFileNotOld()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<PayrollExcelRowDto> { Row() });
        GivenEmployees();
        var period = Period();
        var existing = PayrollEntity(period: period);
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>()).Returns(existing);
        _repo.RemoveImportFilesAsync(existing.PayrollId, Arg.Any<CancellationToken>()).Returns(new List<string> { "2026-10/old.xlsx" });
        _repo.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("db"));

        // Act
        var act = () => _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _files.Received(1).DeleteAsync("2026-10/new.xlsx", Arg.Any<CancellationToken>());
        await _files.DidNotReceive().DeleteAsync("2026-10/old.xlsx", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID166_ImportAsync_ParserThrows_FailureExcelInvalid()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(_ => throw new InvalidDataException("bad"));

        // Act
        var result = await _sut.ImportAsync(new MemoryStream(), Month, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollExcelInvalid);
    }

    [Fact]
    public async Task UTCID167_GetByPeriodMonthAsync_ThreePayslips_ReturnsSummary()
    {
        // Arrange
        var period = Period();
        var payroll = PayrollEntity(period: period);
        payroll.Payslips.Add(new Payslip { NetPay = 1_000_000 });
        payroll.Payslips.Add(new Payslip { NetPay = 2_000_000 });
        payroll.Payslips.Add(new Payslip { NetPay = 3_000_000 });
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.FindPayrollByPeriodIdAsync(period.PeriodId, true, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.GetByPeriodMonthAsync(Month, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.PayslipCount.Should().Be(3);
        result.Data.TotalAmount.Should().Be(6_000_000);
    }

    [Fact]
    public async Task UTCID168_GetByPeriodMonthAsync_InvalidMonth_Failure()
    {
        // Act
        var result = await _sut.GetByPeriodMonthAsync("2026-13", default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task UTCID169_GetByPeriodMonthAsync_NoPayroll_NotFound()
    {
        // Arrange
        _repo.FindPeriodByMonthAsync("2026-09", Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        // Act
        var result = await _sut.GetByPeriodMonthAsync("2026-09", default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task UTCID170_SubmitAsync_DraftFivePayslips_SuccessWithAudit()
    {
        // Arrange
        var payroll = PayrollEntity(1);
        payroll.Payslips.Add(new Payslip { EmployeeId = 1, NetPay = 1 });
        payroll.Payslips.Add(new Payslip { EmployeeId = 2, NetPay = 1 });
        payroll.Payslips.Add(new Payslip { EmployeeId = 3, NetPay = 1 });
        payroll.Payslips.Add(new Payslip { EmployeeId = 4, NetPay = 1 });
        payroll.Payslips.Add(new Payslip { EmployeeId = 5, NetPay = 1 });
        _repo.FindPayrollByIdAsync(1, true, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.SubmitAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.PendingApproval);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Payroll)));
    }

    [Fact]
    public async Task UTCID171_SubmitAsync_DraftNoPayslips_FailureEmpty()
    {
        // Arrange
        _repo.FindPayrollByIdAsync(2, true, Arg.Any<CancellationToken>()).Returns(PayrollEntity(2));

        // Act
        var result = await _sut.SubmitAsync(2, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollEmpty);
    }

    [Fact]
    public async Task UTCID172_SubmitAsync_Approved_FailureInvalidStatus()
    {
        // Arrange
        _repo.FindPayrollByIdAsync(3, true, Arg.Any<CancellationToken>())
            .Returns(PayrollEntity(3, PayrollStatus.Approved));

        // Act
        var result = await _sut.SubmitAsync(3, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task UTCID173_SubmitAsync_NotFound_Failure()
    {
        // Arrange
        _repo.FindPayrollByIdAsync(999, true, Arg.Any<CancellationToken>()).Returns((Payroll?)null);

        // Act
        var result = await _sut.SubmitAsync(999, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task UTCID174_GetImportedFileAsync_HasPath_ReturnsFile()
    {
        // Arrange
        var payroll = PayrollEntity();
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);
        _repo.GetImportFilesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, PayrollImportFile> { [1] = new("2026-10/file.xlsx", Now) });
        _files.ReadAsync("2026-10/file.xlsx", Arg.Any<CancellationToken>()).Returns(new byte[] { 1, 2, 3 });

        // Act
        var result = await _sut.GetImportedFileAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Content.Should().NotBeEmpty();
        result.Data.FileName.Should().Contain(".xlsx");
    }

    [Fact]
    public async Task UTCID175_GetImportedFileAsync_NoPath_FailureFileNotFound()
    {
        // Arrange
        _repo.FindPayrollByIdAsync(4, false, Arg.Any<CancellationToken>()).Returns(PayrollEntity(4));

        // Act
        var result = await _sut.GetImportedFileAsync(4, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollFileNotFound);
    }

    [Fact]
    public async Task UTCID176_GetImportedFileAsync_MissingOnDisk_FailureFileNotFound()
    {
        // Arrange
        var payroll = PayrollEntity();
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);
        _repo.GetImportFilesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, PayrollImportFile> { [1] = new("gone.xlsx", Now) });
        _files.ReadAsync("gone.xlsx", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        // Act
        var result = await _sut.GetImportedFileAsync(1, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollFileNotFound);
    }

    [Fact]
    public async Task UTCID177_GetImportedFileAsync_PayrollNotFound_Failure()
    {
        // Act
        var result = await _sut.GetImportedFileAsync(999, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task UTCID178_GetPayrollsAsync_FilterPendingApproval_ReturnsOne()
    {
        // Arrange
        var list = new List<Payroll>
        {
            PayrollEntity(1, PayrollStatus.Draft),
            PayrollEntity(2, PayrollStatus.PendingApproval),
            PayrollEntity(3, PayrollStatus.Approved),
        };
        _repo.GetPayrollsAsync(PayrollStatus.PendingApproval, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new List<Payroll> { list[1] }, 1));

        // Act
        var result = await _sut.GetPayrollsAsync(new PayrollQuery("PendingApproval", Page: 1), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.Single().Status.Should().Be("PendingApproval");
    }

    [Fact]
    public async Task UTCID179_GetPayrollsAsync_NullStatus_ReturnsAll()
    {
        // Arrange
        var list = new List<Payroll>
        {
            PayrollEntity(1, PayrollStatus.Draft),
            PayrollEntity(2, PayrollStatus.PendingApproval),
            PayrollEntity(3, PayrollStatus.Approved),
        };
        _repo.GetPayrollsAsync(null, 1, 20, Arg.Any<CancellationToken>()).Returns((list, 3));

        // Act
        var result = await _sut.GetPayrollsAsync(new PayrollQuery(null, Page: 1), default);

        // Assert
        result.Data!.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task UTCID180_GetPayrollsAsync_PageBeyondData_EmptyItems()
    {
        // Arrange
        _repo.GetPayrollsAsync(null, 5, 10, Arg.Any<CancellationToken>()).Returns((new List<Payroll>(), 3));

        // Act
        var result = await _sut.GetPayrollsAsync(new PayrollQuery(null, Page: 5, PageSize: 10), default);

        // Assert
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(3);
    }

    [Fact]
    public async Task UTCID181_GetPayrollsAsync_InvalidStatus_FailureValidation()
    {
        // Arrange
        _repo.GetPayrollsAsync("Foo", 1, 20, Arg.Any<CancellationToken>()).Returns((new List<Payroll>(), 0));

        // Act
        var result = await _sut.GetPayrollsAsync(new PayrollQuery("Foo", Page: 1), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID182_GetDetailAsync_FivePayslips_ReturnsDetail()
    {
        // Arrange
        var payroll = PayrollEntity();
        for (var i = 0; i < 5; i++)
            payroll.Payslips.Add(new Payslip { EmployeeId = i + 1, Employee = new Employee { EmployeeId = i + 1, EmployeeCode = $"NV{i}", FullName = "A" }, NetPay = 1 });
        _repo.FindPayrollByIdAsync(1, true, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.GetDetailAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Payslips.Should().HaveCount(5);
    }

    [Fact]
    public async Task UTCID183_GetDetailAsync_DraftNoPayslips_EmptyList()
    {
        // Arrange
        _repo.FindPayrollByIdAsync(2, true, Arg.Any<CancellationToken>()).Returns(PayrollEntity(2));

        // Act
        var result = await _sut.GetDetailAsync(2, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Payslips.Should().BeEmpty();
        result.Data.TotalAmount.Should().Be(0);
    }

    [Fact]
    public async Task UTCID184_GetDetailAsync_NotFound_Failure()
    {
        // Act
        var result = await _sut.GetDetailAsync(999, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task UTCID185_ApproveAsync_PendingApproval_LocksPeriod()
    {
        // Arrange
        var payroll = PayrollEntity(1, PayrollStatus.PendingApproval);
        payroll.Payslips.Add(new Payslip { NetPay = 1 });
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.ApproveAsync(1, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.Approved);
        payroll.Period.Status.Should().Be(RecordStatus.Locked);
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Approve));
    }

    [Fact]
    public async Task UTCID186_ApproveAsync_PendingZeroPayslips_FailureEmpty()
    {
        // Arrange
        var payroll = PayrollEntity(5, PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(5, false, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.ApproveAsync(5, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PayrollEmpty);
    }

    [Fact]
    public async Task UTCID187_ApproveAsync_Draft_FailureNoLock()
    {
        // Arrange
        var payroll = PayrollEntity(2, PayrollStatus.Draft);
        _repo.FindPayrollByIdAsync(2, false, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.ApproveAsync(2, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
        payroll.Period.Status.Should().Be(RecordStatus.Open);
    }

    [Fact]
    public async Task UTCID188_ApproveAsync_NotFound_Failure()
    {
        // Act
        var result = await _sut.ApproveAsync(999, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task UTCID189_RejectAsync_Pending_SuccessWithReason()
    {
        // Arrange
        var payroll = PayrollEntity(1, PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.RejectAsync(1, new RejectPayrollDto("Sai phụ cấp"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.Rejected);
        payroll.RejectReason.Should().Be("Sai phụ cấp");
    }

    [Fact]
    public async Task UTCID190_RejectAsync_WhitespaceReason_FailureValidation()
    {
        // Arrange
        var payroll = PayrollEntity(1, PayrollStatus.PendingApproval);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);
        var validator = new RejectPayrollDtoValidator();

        // Act
        var valid = validator.Validate(new RejectPayrollDto("   "));
        var result = await _sut.RejectAsync(1, new RejectPayrollDto("   "), default);

        // Assert
        valid.IsValid.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID191_RejectAsync_Draft_FailureInvalidStatus()
    {
        // Arrange
        _repo.FindPayrollByIdAsync(2, false, Arg.Any<CancellationToken>()).Returns(PayrollEntity(2));

        // Act
        var result = await _sut.RejectAsync(2, new RejectPayrollDto("x"), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task UTCID192_RejectAsync_NotFound_Failure()
    {
        // Act
        var result = await _sut.RejectAsync(999, new RejectPayrollDto("x"), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task UTCID193_ReopenAsync_Approved_UnlocksPeriod()
    {
        // Arrange
        var period = Period(status: RecordStatus.Locked);
        period.LockedAt = Now;
        var payroll = PayrollEntity(1, PayrollStatus.Approved, period);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.ReopenAsync(1, new ReopenPayrollDto("Điều chỉnh thưởng"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        payroll.Status.Should().Be(PayrollStatus.Draft);
        payroll.Period.Status.Should().Be(RecordStatus.Open);
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Unlock));
    }

    [Fact]
    public async Task UTCID194_ReopenAsync_NullReason_FailureValidation()
    {
        // Arrange
        var payroll = PayrollEntity(1, PayrollStatus.Approved);
        _repo.FindPayrollByIdAsync(1, false, Arg.Any<CancellationToken>()).Returns(payroll);

        // Act
        var result = await _sut.ReopenAsync(1, new ReopenPayrollDto(null!), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID195_ReopenAsync_Draft_FailureInvalidStatus()
    {
        // Arrange
        _repo.FindPayrollByIdAsync(2, false, Arg.Any<CancellationToken>()).Returns(PayrollEntity(2));

        // Act
        var result = await _sut.ReopenAsync(2, new ReopenPayrollDto("x"), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollInvalidStatus);
    }

    [Fact]
    public async Task UTCID196_ReopenAsync_NotFound_Failure()
    {
        // Act
        var result = await _sut.ReopenAsync(999, new ReopenPayrollDto("x"), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollNotFound);
    }

    [Fact]
    public async Task UTCID197_GetMyPayslipsAsync_ThreeApproved_OrderedDesc()
    {
        // Arrange
        // Repo thật OrderByDescending(PeriodMonth) — mock phải phản ánh contract đó
        _repo.GetMyPayslipSummariesAsync(10, Arg.Any<CancellationToken>()).Returns(new List<(string PeriodMonth, decimal NetPay, DateTime ApprovedAt)>
        {
            ("2026-10", 3, Now), ("2026-09", 2, Now), ("2026-08", 1, Now),
        });

        // Act
        var result = await _sut.GetMyPayslipsAsync(10, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().HaveCount(3);
        result.Data.Select(x => x.PeriodMonth).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task UTCID198_GetMyPayslipsAsync_NoPayslips_Empty()
    {
        // Arrange
        _repo.GetMyPayslipSummariesAsync(11, Arg.Any<CancellationToken>()).Returns(new List<(string, decimal, DateTime)>());

        // Act
        var result = await _sut.GetMyPayslipsAsync(11, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID199_GetMyPayslipsAsync_EmployeeZero_FailureOrEmpty()
    {
        // Arrange
        _repo.GetMyPayslipSummariesAsync(0, Arg.Any<CancellationToken>()).Returns(new List<(string, decimal, DateTime)>());

        // Act
        var result = await _sut.GetMyPayslipsAsync(0, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID200_GetMyPayslipAsync_Approved_ReturnsDetail()
    {
        // Arrange
        var payslip = new Payslip
        {
            NetPay = 5_000_000, WorkDays = 26,
            PayslipItems = new List<PayslipItem> { new() { ItemGroup = PayslipItemGroup.Addition, ItemName = "Lương", Amount = 5_000_000, SortOrder = 0 } },
        };
        _repo.FindMyPayslipAsync(10, Month, Arg.Any<CancellationToken>()).Returns((payslip, Now));

        // Act
        var result = await _sut.GetMyPayslipAsync(10, Month, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.NetPay.Should().Be(5_000_000);
        result.Data.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UTCID201_GetMyPayslipAsync_InvalidMonth_Failure()
    {
        // Act
        var result = await _sut.GetMyPayslipAsync(10, "2026-13", default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayrollPeriodMonthInvalid);
    }

    [Fact]
    public async Task UTCID202_GetMyPayslipAsync_OtherEmployee_NotFound()
    {
        // Arrange
        _repo.FindMyPayslipAsync(10, Month, Arg.Any<CancellationToken>()).Returns(((Payslip, DateTime)?)null);

        // Act
        var result = await _sut.GetMyPayslipAsync(10, Month, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayslipNotFound);
    }

    [Fact]
    public async Task UTCID203_GetMyPayslipAsync_DraftPayroll_NotFound()
    {
        // Arrange
        _repo.FindMyPayslipAsync(10, "2026-11", Arg.Any<CancellationToken>()).Returns(((Payslip, DateTime)?)null);

        // Act
        var result = await _sut.GetMyPayslipAsync(10, "2026-11", default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PayslipNotFound);
    }
}