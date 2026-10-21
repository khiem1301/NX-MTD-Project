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

public class AttendanceServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 9, 0, 0);

    private readonly IAttendanceRepository _repo = Substitute.For<IAttendanceRepository>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IExcelAttendanceParser _excel = Substitute.For<IExcelAttendanceParser>();
    private readonly AttendanceService _sut;

    public AttendanceServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(DateOnly.FromDateTime(Now));
        _settings.GetIntAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<int>(1));
        _sut = new AttendanceService(_repo, _settings, _clock, _excel, NullLogger<AttendanceService>.Instance);
    }

    private static AttendancePunchDto Punch(int code = 101, int? sourceId = 1) =>
        new(sourceId, code, new DateTime(2026, 10, 20, 7, 25, 0));

    private static Employee Employee(int id = 7, int? attendanceCode = null, string status = RecordStatus.Active) =>
        new() { EmployeeId = id, EmployeeCode = "CN007", FullName = "Nguyễn Văn A",
            AttendanceCode = attendanceCode, Status = status };

    // ---------- IngestAsync (UC61) ----------

    [Fact]
    public async Task IngestAsync_EmptyBatch_ReturnsFailure()
    {
        var result = await _sut.IngestAsync(new IngestPunchesDto([], null), SyncSource.RjSync, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.AttendanceEmptyBatch);
        await _repo.DidNotReceiveWithAnyArgs()
            .IngestBatchAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task IngestAsync_ValidBatch_PassesSourceToRepositoryAndReturnsCounts()
    {
        _repo.IngestBatchAsync(Arg.Any<IReadOnlyList<AttendancePunchDto>>(), SyncSource.RjSync, null,
                Arg.Any<CancellationToken>())
            .Returns(new IngestResultDto(SyncSessionId: 12, Received: 5, Inserted: 3,
                SkippedLockedMonth: 1, Duplicates: 1, Unmapped: 2));

        var result = await _sut.IngestAsync(new IngestPunchesDto([Punch()], null), SyncSource.RjSync,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Inserted.Should().Be(3);
        result.Data.Unmapped.Should().Be(2);
        // MSG24: thông báo phải nêu rõ số dòng hợp lệ / khóa tháng / trùng.
        result.Message.Should().Contain("3 dòng hợp lệ").And.Contain("1 dòng trùng");
    }

    [Fact]
    public async Task IngestAsync_ExcelImport_UsesExcelSourceAndFileName()
    {
        _repo.IngestBatchAsync(Arg.Any<IReadOnlyList<AttendancePunchDto>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new IngestResultDto(1, 1, 1, 0, 0, 0));

        await _sut.IngestAsync(new IngestPunchesDto([Punch()], "rj-2026-10.xlsx"), SyncSource.ExcelImport,
            CancellationToken.None);

        await _repo.Received(1).IngestBatchAsync(Arg.Any<IReadOnlyList<AttendancePunchDto>>(),
            SyncSource.ExcelImport, "rj-2026-10.xlsx", Arg.Any<CancellationToken>());
    }

    // ---------- GetHealthAsync (MSG23) ----------

    [Fact]
    public async Task GetHealthAsync_LastSyncWithinThreshold_IsNotOverdue()
    {
        _repo.GetLastSucceededAtAsync(Arg.Any<CancellationToken>()).Returns(Now.AddMinutes(-5));
        _repo.CountUnmappedCodesAsync(Arg.Any<CancellationToken>()).Returns(0);

        var result = await _sut.GetHealthAsync(CancellationToken.None);

        result.Data!.IsOverdue.Should().BeFalse();
        result.Data.MinutesSinceLastSuccess.Should().Be(5);
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task GetHealthAsync_LastSyncOlderThanThreshold_IsOverdueWithWarning()
    {
        _repo.GetLastSucceededAtAsync(Arg.Any<CancellationToken>()).Returns(Now.AddMinutes(-31));
        _repo.CountUnmappedCodesAsync(Arg.Any<CancellationToken>()).Returns(4);

        var result = await _sut.GetHealthAsync(CancellationToken.None);

        result.Data!.IsOverdue.Should().BeTrue();
        result.Data.UnmappedCodeCount.Should().Be(4);
        result.Message.Should().Be(Messages.AttendanceSyncOverdue);
    }

    [Fact]
    public async Task GetHealthAsync_NeverSynced_IsOverdue()
    {
        _repo.GetLastSucceededAtAsync(Arg.Any<CancellationToken>()).Returns((DateTime?)null);

        var result = await _sut.GetHealthAsync(CancellationToken.None);

        result.Data!.IsOverdue.Should().BeTrue();
        result.Data.MinutesSinceLastSuccess.Should().BeNull();
    }

    // ---------- MapCodeAsync (UC28) ----------

    [Fact]
    public async Task MapCodeAsync_EmployeeNotFound_ReturnsNotFound()
    {
        _repo.GetEmployeeAsync(7, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        var result = await _sut.MapCodeAsync(new MapAttendanceCodeDto(101, 7), 1, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.AttendanceEmployeeNotFound);
    }

    [Fact]
    public async Task MapCodeAsync_EmployeeAlreadyHasAnotherCode_ReturnsFailure()
    {
        _repo.GetEmployeeAsync(7, Arg.Any<CancellationToken>()).Returns(Employee(attendanceCode: 999));

        var result = await _sut.MapCodeAsync(new MapAttendanceCodeDto(101, 7), 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendanceEmployeeHasCode);
        await _repo.DidNotReceiveWithAnyArgs().MapCodeAsync(default, default, default, default);
    }

    [Fact]
    public async Task MapCodeAsync_CodeBelongsToAnotherEmployee_ReturnsFailure()
    {
        _repo.GetEmployeeAsync(7, Arg.Any<CancellationToken>()).Returns(Employee());
        _repo.GetEmployeeByAttendanceCodeAsync(101, Arg.Any<CancellationToken>()).Returns(Employee(id: 8));

        var result = await _sut.MapCodeAsync(new MapAttendanceCodeDto(101, 7), 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendanceCodeAlreadyMapped);
        await _repo.DidNotReceiveWithAnyArgs().MapCodeAsync(default, default, default, default);
    }

    [Fact]
    public async Task MapCodeAsync_CodeHasNoPunches_ReturnsNotFound()
    {
        _repo.GetEmployeeAsync(7, Arg.Any<CancellationToken>()).Returns(Employee());
        _repo.GetEmployeeByAttendanceCodeAsync(101, Arg.Any<CancellationToken>()).Returns((Employee?)null);
        _repo.HasPunchesForCodeAsync(101, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.MapCodeAsync(new MapAttendanceCodeDto(101, 7), 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendanceCodeNotFound);
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task MapCodeAsync_ValidRequest_CallsRepositoryWithChangedBy()
    {
        _repo.GetEmployeeAsync(7, Arg.Any<CancellationToken>()).Returns(Employee());
        _repo.GetEmployeeByAttendanceCodeAsync(101, Arg.Any<CancellationToken>()).Returns((Employee?)null);
        _repo.HasPunchesForCodeAsync(101, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.MapCodeAsync(new MapAttendanceCodeDto(101, 7), changedBy: 42,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).MapCodeAsync(101, 7, 42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MapCodeAsync_RemappingSameCodeToSameEmployee_IsAllowed()
    {
        _repo.GetEmployeeAsync(7, Arg.Any<CancellationToken>()).Returns(Employee(attendanceCode: 101));
        _repo.GetEmployeeByAttendanceCodeAsync(101, Arg.Any<CancellationToken>()).Returns(Employee(attendanceCode: 101));
        _repo.HasPunchesForCodeAsync(101, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.MapCodeAsync(new MapAttendanceCodeDto(101, 7), 1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ---------- Phạm vi xem theo vai trò (GBR-01) ----------

    [Fact]
    public async Task GetTimesheetsAsync_TeamScope_OverridesTeamIdFromClient()
    {
        GivenTimesheetPage();
        // Client cố tình truyền tổ khác; scope của tổ trưởng phải thắng.
        var query = BaseQuery() with { TeamId = 99 };

        await _sut.GetTimesheetsAsync(query, TimesheetScope.ForTeam(3), CancellationToken.None);

        await _repo.Received(1).GetTimesheetsAsync(
            Arg.Is<TimesheetQuery>(q => q.TeamId == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTimesheetsAsync_EmployeeScope_OverridesEmployeeIdFromClient()
    {
        GivenTimesheetPage();
        var query = BaseQuery() with { EmployeeId = 99 };

        await _sut.GetTimesheetsAsync(query, TimesheetScope.ForEmployee(7), CancellationToken.None);

        await _repo.Received(1).GetTimesheetsAsync(
            Arg.Is<TimesheetQuery>(q => q.EmployeeId == 7), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTimesheetsAsync_AllScope_KeepsClientFilters()
    {
        GivenTimesheetPage();
        var query = BaseQuery() with { TeamId = 5, EmployeeId = 9 };

        await _sut.GetTimesheetsAsync(query, TimesheetScope.All, CancellationToken.None);

        await _repo.Received(1).GetTimesheetsAsync(
            Arg.Is<TimesheetQuery>(q => q.TeamId == 5 && q.EmployeeId == 9), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTimesheetsAsync_PageSizeAboveMax_IsClampedToMax()
    {
        GivenTimesheetPage();
        var query = BaseQuery() with { Page = 0, PageSize = 5000 };

        var result = await _sut.GetTimesheetsAsync(query, TimesheetScope.All, CancellationToken.None);

        await _repo.Received(1).GetTimesheetsAsync(
            Arg.Is<TimesheetQuery>(q => q.Page == 1 && q.PageSize == Paging.MaxPageSize),
            Arg.Any<CancellationToken>());
        result.Data!.Page.Should().Be(1);
        result.Data.PageSize.Should().Be(Paging.MaxPageSize);
    }

    [Fact]
    public async Task GetTimesheetsAsync_NoRows_ReturnsNoDataMessage()
    {
        _repo.GetTimesheetsAsync(Arg.Any<TimesheetQuery>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<DailyTimesheetDto>(), 0));

        var result = await _sut.GetTimesheetsAsync(BaseQuery(), TimesheetScope.All, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.NoData);   // MSG02
        result.Data!.TotalPages.Should().Be(0);
    }

    private static TimesheetQuery BaseQuery() =>
        new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20), null, null, null, null);

    private void GivenTimesheetPage() =>
        _repo.GetTimesheetsAsync(Arg.Any<TimesheetQuery>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<DailyTimesheetDto>(), 0));

    // ---------- Chấm bù (UC29) ----------

    private static CreateAttendanceAdjustmentDto Adjustment(
        string type = AdjustmentType.ForgotPunch, int employeeId = 7,
        int? requestId = null, TimeOnly? checkIn = null, TimeOnly? checkOut = null) =>
        new(employeeId, new DateOnly(2026, 10, 20), type, "Quên quẹt vân tay", requestId,
            checkIn ?? new TimeOnly(7, 30), checkOut ?? new TimeOnly(16, 30));

    [Fact]
    public async Task CreateAdjustmentAsync_EmployeeNotFound_ReturnsNotFound()
    {
        _repo.GetEmployeeStatusAsync(7, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        var result = await _sut.CreateAdjustmentAsync(Adjustment(), 1, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.AttendanceEmployeeNotFound);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_InactiveEmployee_ReturnsFailure()
    {
        _repo.GetEmployeeStatusAsync(7, Arg.Any<CancellationToken>()).Returns(
            Employee(status: RecordStatus.Resigned));

        var result = await _sut.CreateAdjustmentAsync(Adjustment(), 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendanceEmployeeInactive);
        await _repo.DidNotReceiveWithAnyArgs().ApplyAdjustmentAsync(default!, default, default);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_LockedPeriod_ReturnsConflict()
    {
        _repo.IsPeriodLockedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.CreateAdjustmentAsync(Adjustment(), 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendancePeriodLocked);
        result.Kind.Should().Be(ResultKind.Conflict);
        await _repo.DidNotReceiveWithAnyArgs().ApplyAdjustmentAsync(default!, default, default);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_AlreadyAdjusted_ReturnsFailure()
    {
        _repo.GetEmployeeStatusAsync(7, Arg.Any<CancellationToken>()).Returns(Employee());
        _repo.HasAdjustmentAsync(7, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.CreateAdjustmentAsync(Adjustment(), 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendanceAdjustmentExists);
        await _repo.DidNotReceiveWithAnyArgs().ApplyAdjustmentAsync(default!, default, default);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_ApprovedRequest_RequiresApprovedAttendanceCorrection()
    {
        _repo.GetEmployeeStatusAsync(7, Arg.Any<CancellationToken>()).Returns(Employee());
        _repo.HasAdjustmentAsync(7, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.GetAdjustmentRequestAsync(11, Arg.Any<CancellationToken>()).Returns(
            new Request { RequestId = 11, RequestTypeCode = RequestTypes.AttendanceCorrection, Status = RequestStatuses.PendingApproval });

        var result = await _sut.CreateAdjustmentAsync(
            Adjustment(type: AdjustmentType.ApprovedRequest, requestId: 11), 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendanceRequestInvalid);
        await _repo.DidNotReceiveWithAnyArgs().ApplyAdjustmentAsync(default!, default, default);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_ValidRequest_CallsRepository()
    {
        _repo.GetEmployeeStatusAsync(7, Arg.Any<CancellationToken>()).Returns(Employee());
        _repo.HasAdjustmentAsync(7, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.CreateAdjustmentAsync(Adjustment(), 42, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).ApplyAdjustmentAsync(Arg.Any<CreateAttendanceAdjustmentDto>(), 42,
            Arg.Any<CancellationToken>());
    }

    // ---------- Import Excel chấm công (UC29) ----------

    /// <summary>Mặc định là dòng đủ cả giờ vào và giờ ra; truyền null để bỏ một cột.</summary>
    private static AttendanceExcelRowDto ExcelRow(int code = 101) =>
        new(code, new DateOnly(2026, 10, 20), new TimeOnly(7, 30), new TimeOnly(16, 30));

    [Fact]
    public async Task ImportExcelAsync_EmptyFile_ReturnsFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(Array.Empty<AttendanceExcelRowDto>());

        var result = await _sut.ImportExcelAsync(new MemoryStream(), "rj.xlsx", CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.AttendanceExcelEmpty);
        await _repo.DidNotReceiveWithAnyArgs().IngestBatchAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task ImportExcelAsync_InvalidFile_ReturnsValidationError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(x => throw new InvalidDataException("sai cột"));

        var result = await _sut.ImportExcelAsync(new MemoryStream(), "rj.xlsx", CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.AttendanceExcelInvalid);
        await _repo.DidNotReceiveWithAnyArgs().IngestBatchAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task ImportExcelAsync_ValidFile_SendsPunchesWithExcelSource()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new[] { ExcelRow() });
        _repo.IngestBatchAsync(Arg.Any<IReadOnlyList<AttendancePunchDto>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new IngestResultDto(1, 2, 2, 0, 0, 0));

        var result = await _sut.ImportExcelAsync(new MemoryStream(), "rj.xlsx", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).IngestBatchAsync(
            Arg.Is<IReadOnlyList<AttendancePunchDto>>(p => p.Count == 2),
            SyncSource.ExcelImport, "rj.xlsx", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewExcelAsync_ValidFile_ReturnsRowsAndPunchCount()
    {
        var onlyCheckIn = ExcelRow(code: 102) with { CheckOut = null };
        _excel.Parse(Arg.Any<Stream>()).Returns(new[] { ExcelRow(), onlyCheckIn });

        var result = await _sut.PreviewExcelAsync(new MemoryStream(), "rj.xlsx", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.RowCount.Should().Be(2);
        result.Data.PunchCount.Should().Be(3);   // dòng 1: 2 lượt; dòng 2 chỉ giờ vào: 1 lượt
    }
}

public class AttendanceValidatorTests
{
    private readonly IngestPunchesDtoValidator _ingest = new();
    private readonly MapAttendanceCodeDtoValidator _map = new();
    private readonly TimesheetQueryValidator _query = new();

    private static AttendancePunchDto ValidPunch() => new(1, 101, new DateTime(2026, 10, 20, 7, 25, 0));

    [Fact]
    public void Ingest_EmptyPunchList_HasError() =>
        _ingest.TestValidate(new IngestPunchesDto([], null))
            .ShouldHaveValidationErrorFor(x => x.Punches);

    [Fact]
    public void Ingest_BatchOverMaxSize_HasError()
    {
        var tooMany = Enumerable.Range(1, IngestPunchesDtoValidator.MaxBatchSize + 1)
            .Select(i => new AttendancePunchDto(i, 101, new DateTime(2026, 10, 20, 7, 0, 0)))
            .ToList();

        _ingest.TestValidate(new IngestPunchesDto(tooMany, null))
            .ShouldHaveValidationErrorFor(x => x.Punches);
    }

    [Fact]
    public void Ingest_NonPositiveAttendanceCode_HasError() =>
        _ingest.TestValidate(new IngestPunchesDto([ValidPunch() with { AttendanceCode = 0 }], null))
            .ShouldHaveValidationErrorFor("Punches[0].AttendanceCode");

    [Fact]
    public void Ingest_PunchTimeInFarFuture_HasError() =>
        _ingest.TestValidate(new IngestPunchesDto([ValidPunch() with { PunchTime = DateTime.Now.AddDays(3) }], null))
            .ShouldHaveValidationErrorFor("Punches[0].PunchTime");

    [Fact]
    public void Ingest_PunchTimeBefore2020_HasError() =>
        _ingest.TestValidate(new IngestPunchesDto([ValidPunch() with { PunchTime = new DateTime(2019, 12, 31) }], null))
            .ShouldHaveValidationErrorFor("Punches[0].PunchTime");

    [Fact]
    public void Ingest_ExcelRowWithoutSourceId_IsValid() =>
        _ingest.TestValidate(new IngestPunchesDto([ValidPunch() with { SourceRecordId = null }], "rj.xlsx"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Map_NonPositiveIds_HaveErrors()
    {
        var result = _map.TestValidate(new MapAttendanceCodeDto(0, 0));

        result.ShouldHaveValidationErrorFor(x => x.AttendanceCode);
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId);
    }

    [Fact]
    public void Query_ToDateBeforeFromDate_HasError() =>
        _query.TestValidate(new TimesheetQuery(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 1),
                null, null, null, null))
            .ShouldHaveValidationErrorFor(x => x.ToDate);

    [Fact]
    public void Query_RangeWiderThanLimit_HasError() =>
        _query.TestValidate(new TimesheetQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
                null, null, null, null))
            .ShouldHaveValidationErrorFor(x => x.ToDate);

    [Fact]
    public void Query_PageSizeOverMax_HasError() =>
        _query.TestValidate(new TimesheetQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20),
                null, null, null, null, Page: 1, PageSize: 101))
            .ShouldHaveValidationErrorFor(x => x.PageSize);

    [Fact]
    public void Query_ValidRange_HasNoError() =>
        _query.TestValidate(new TimesheetQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20),
                3, null, true, "An", Page: 1, PageSize: 20))
            .ShouldNotHaveAnyValidationErrors();
}

public class AdjustmentValidatorTests
{
    private readonly CreateAttendanceAdjustmentDtoValidator _create = new();
    private readonly AdjustmentQueryValidator _query = new();

    private static CreateAttendanceAdjustmentDto ValidAdjustment() =>
        new(7, new DateOnly(2026, 10, 20), AdjustmentType.ForgotPunch, "Quên quẹt",
            null, new TimeOnly(7, 30), new TimeOnly(16, 30));

    [Fact]
    public void Create_NoReason_HasError() =>
        _create.TestValidate(ValidAdjustment() with { Reason = "" })
            .ShouldHaveValidationErrorFor(x => x.Reason);

    [Fact]
    public void Create_NoTime_HasError() =>
        _create.TestValidate(ValidAdjustment() with { CheckIn = null, CheckOut = null })
            .ShouldHaveValidationErrorFor(x => x);

    [Fact]
    public void Create_ApprovedRequestWithoutRequestId_HasError() =>
        _create.TestValidate(ValidAdjustment() with { AdjustmentType = AdjustmentType.ApprovedRequest, RequestId = null })
            .ShouldHaveValidationErrorFor(x => x.RequestId);

    [Fact]
    public void Create_Valid_HasNoError() =>
        _create.TestValidate(ValidAdjustment()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void AdjustmentQuery_ToDateBeforeFromDate_HasError() =>
        _query.TestValidate(new AdjustmentQuery(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 1),
                null, null))
            .ShouldHaveValidationErrorFor(x => x.ToDate);

    [Fact]
    public void AdjustmentQuery_Valid_HasNoError() =>
        _query.TestValidate(new AdjustmentQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20),
                7, "An"))
            .ShouldNotHaveAnyValidationErrors();
}
