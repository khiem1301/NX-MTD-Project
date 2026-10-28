using ClosedXML.Excel;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Hr;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Services;

namespace Application.Tests;

/// <summary>KH-22 / UC17, UC22: import nhân viên từ Excel (xem trước MSG24 rồi nhập) và xem AuditLog.</summary>
public class EmployeeImportTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IExcelEmployeeParser _parser = Substitute.For<IExcelEmployeeParser>();
    private readonly IEmployeeService _employees = Substitute.For<IEmployeeService>();
    private readonly List<Employee> _existing = new() { new Employee { EmployeeCode = "NV001" } };
    private readonly List<Team> _teams = new() { new Team { TeamId = 3, TeamCode = "MAY1", Status = RecordStatus.Active } };
    private readonly List<AuditLog> _logs = new();

    public EmployeeImportTests()
    {
        Setup(_existing); Setup(_teams); Setup(_logs);
        _employees.CreateAsync(Arg.Any<CreateEmployeeDto>(), Arg.Any<CancellationToken>()).Returns(c =>
            Result<CreatedEmployeeDto>.Success(new CreatedEmployeeDto(1, c.Arg<CreateEmployeeDto>().EmployeeCode, "Tmp#123")));
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private EmployeeImportService Service() => new(_uow, _parser, _employees, new CreateEmployeeDtoValidator(), new EmployeeScope(_user));
    private readonly ICurrentUser _user = CurrentAdmin();

    private static ICurrentUser CurrentAdmin()
    {
        var user = Substitute.For<ICurrentUser>();
        user.Role.Returns(Roles.Admin);
        return user;
    }

    private static EmployeeExcelRowDto Row(int n, string code, string role = "Worker", string? team = "MAY1", string? error = null, int? att = null,
        string name = "Nguyễn Văn A", string pay = "PieceRate") =>
        new(n, code, name, "Nam", null, "0901234567", role, team, pay, new DateOnly(2026, 1, 5), att, error);

    private void Rows(params EmployeeExcelRowDto[] rows) => _parser.Parse(Arg.Any<Stream>()).Returns(rows);

    [Fact]
    public async Task Preview_splits_rows_into_valid_error_and_duplicate_with_the_msg24_summary()
    {
        Rows(Row(2, "NV100"), Row(3, "NV001"), Row(4, "NV101", team: "KHONGCO"), Row(5, "NV100"), Row(6, "NV102", role: "Giám đốc"), Row(7, "NV103", error: "Ngày sinh sai"));

        var preview = (await Service().PreviewAsync(Stream.Null, default)).Data!;

        preview.ValidCount.Should().Be(1);
        preview.DuplicateCount.Should().Be(2);   // trùng DB + lặp trong file
        preview.ErrorCount.Should().Be(3);
        preview.Summary.Should().Be("1 dòng hợp lệ, 3 dòng lỗi, 2 dòng trùng. Chỉ các dòng hợp lệ được nhập.");
        preview.Rows.Single(r => r.RowNumber == 3).Status.Should().Be(EmployeeImportService.StatusDuplicate);
        preview.Rows.Single(r => r.RowNumber == 4).Message.Should().Contain("KHONGCO");
        preview.Rows.Single(r => r.RowNumber == 7).Message.Should().Be("Ngày sinh sai");
    }

    [Fact]
    public async Task Role_pay_type_and_gender_accept_common_spellings()
    {
        Rows(Row(2, "A1", role: "team_leader", pay: "khoán"), Row(3, "A2", role: "WORKER", pay: "Daily"), Row(4, "A3", role: "qa qc", team: null, pay: "monthly"));

        var preview = (await Service().PreviewAsync(Stream.Null, default)).Data!;

        preview.ValidCount.Should().Be(3);
        await _employees.DidNotReceive().CreateAsync(Arg.Any<CreateEmployeeDto>(), Arg.Any<CancellationToken>());   // xem trước không tạo gì
    }

    [Fact]
    public async Task A_worker_without_a_team_and_a_repeated_attendance_code_are_flagged()
    {
        Rows(Row(2, "B1", team: null), Row(3, "B2", att: 55), Row(4, "B3", att: 55));

        var preview = (await Service().PreviewAsync(Stream.Null, default)).Data!;

        preview.Rows.Single(r => r.RowNumber == 2).Status.Should().Be(EmployeeImportService.StatusError);
        preview.Rows.Single(r => r.RowNumber == 3).Status.Should().Be(EmployeeImportService.StatusValid);
        preview.Rows.Single(r => r.RowNumber == 4).Status.Should().Be(EmployeeImportService.StatusDuplicate);
    }

    [Fact]
    public async Task Import_creates_only_valid_rows_and_returns_temporary_passwords_once()
    {
        Rows(Row(2, "NV100"), Row(3, "NV001"), Row(4, "NV200", team: "KHONGCO"));

        var result = (await Service().ImportAsync(Stream.Null, default)).Data!;

        result.CreatedCount.Should().Be(1);
        result.Created.Single().Should().Be(new EmployeeImportCreatedDto("NV100", "Nguyễn Văn A", "Tmp#123"));
        result.FailedCount.Should().Be(2);
        await _employees.Received(1).CreateAsync(Arg.Is<CreateEmployeeDto>(d => d.EmployeeCode == "NV100" && d.TeamId == 3 && d.PayType == "PieceRate" && d.Gender == "M"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_row_the_employee_service_refuses_is_reported_as_failed_not_lost()
    {
        Rows(Row(2, "NV100"), Row(3, "NV101"));
        _employees.CreateAsync(Arg.Is<CreateEmployeeDto>(d => d.EmployeeCode == "NV101"), Arg.Any<CancellationToken>())
            .Returns(Result<CreatedEmployeeDto>.Failure(ErrorCodes.EmployeeCodeDuplicate, "Mã chấm công đã được gán."));

        var result = (await Service().ImportAsync(Stream.Null, default)).Data!;

        result.CreatedCount.Should().Be(1);
        result.Failed.Single().Should().Match<EmployeeImportRowDto>(f => f.RowNumber == 3 && f.Message == "Mã chấm công đã được gán.");
    }

    [Fact]
    public async Task An_empty_or_oversized_or_malformed_file_is_rejected()
    {
        Rows();
        (await Service().PreviewAsync(Stream.Null, default)).ErrorCode.Should().Be(ErrorCodes.EmployeeImportInvalid);

        _parser.Parse(Arg.Any<Stream>()).Returns(Enumerable.Range(2, 501).Select(i => Row(i, $"X{i}")).ToArray());
        (await Service().PreviewAsync(Stream.Null, default)).ErrorCode.Should().Be(ErrorCodes.EmployeeImportInvalid);

        _parser.Parse(Arg.Any<Stream>()).Returns(_ => throw new InvalidDataException("File Excel sai cột"));
        (await Service().ImportAsync(Stream.Null, default)).Message.Should().Contain("sai cột");
    }

    // ---------- Parser thật ----------

    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        using var wb = new XLWorkbook();
        fill(wb.AddWorksheet("NhanVien"));
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static readonly string[] Headers = { "Mã NV", "Họ tên", "Giới tính", "Ngày sinh", "SĐT", "Vai trò", "Mã tổ", "Hình thức lương", "Ngày vào làm", "Mã chấm công" };

    [Fact]
    public void The_parser_reads_rows_dates_and_numbers_and_skips_blank_rows()
    {
        using var file = Workbook(ws =>
        {
            for (var i = 0; i < Headers.Length; i++) ws.Cell(1, i + 1).Value = Headers[i];
            ws.Cell(2, 1).Value = "CN10"; ws.Cell(2, 2).Value = "Trần Thị B"; ws.Cell(2, 3).Value = "Nữ"; ws.Cell(2, 4).Value = new DateTime(1998, 3, 4);
            ws.Cell(2, 5).Value = "0912345678"; ws.Cell(2, 6).Value = "Worker"; ws.Cell(2, 7).Value = "MAY1"; ws.Cell(2, 8).Value = "PieceRate";
            ws.Cell(2, 9).Value = "05/01/2026"; ws.Cell(2, 10).Value = 77;
            ws.Cell(4, 1).Value = "CN11"; ws.Cell(4, 4).Value = "không phải ngày"; ws.Cell(4, 10).Value = "abc";
        });

        var rows = new ClosedXmlEmployeeParser().Parse(file);

        rows.Should().HaveCount(2);
        rows[0].Should().Match<EmployeeExcelRowDto>(r => r.Code == "CN10" && r.RowNumber == 2 && r.Gender == "Nữ" && r.AttendanceCode == 77
            && r.DateOfBirth == new DateOnly(1998, 3, 4) && r.StartDate == new DateOnly(2026, 1, 5) && r.ParseError == null);
        rows[1].RowNumber.Should().Be(4);
        rows[1].ParseError.Should().Contain("Ngày sinh");
    }

    [Fact]
    public void The_parser_rejects_a_file_with_the_wrong_columns()
    {
        using var file = Workbook(ws => { ws.Cell(1, 1).Value = "Tên"; ws.Cell(1, 2).Value = "Tuổi"; });

        var act = () => new ClosedXmlEmployeeParser().Parse(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*sai cột*");
    }

    // ---------- AuditLog ----------

    [Fact]
    public async Task Audit_log_can_be_filtered_by_table_action_person_record_and_date_range()
    {
        var admin = new Employee { EmployeeId = 1, FullName = "Admin" };
        _logs.Add(new AuditLog { AuditLogId = 1, TableName = "Employee", RecordId = 5, Action = "UPDATE", ChangedBy = 1, ChangedByNavigation = admin, ChangedAt = new(2026, 10, 1, 9, 0, 0) });
        _logs.Add(new AuditLog { AuditLogId = 2, TableName = "Account", RecordId = 5, Action = "UPDATE", ChangedBy = 1, ChangedByNavigation = admin, ChangedAt = new(2026, 10, 2, 9, 0, 0) });
        _logs.Add(new AuditLog { AuditLogId = 3, TableName = "Employee", RecordId = 6, Action = "INSERT", ChangedBy = null, ChangedAt = new(2026, 10, 3, 23, 30, 0), Reason = "Nhập từ Excel" });
        var svc = new AuditLogQueryService(_uow);

        (await svc.ListAsync(new AuditLogQuery(), default)).Data!.Items.Select(l => l.AuditLogId).Should().Equal(3, 2, 1);
        (await svc.ListAsync(new AuditLogQuery { TableName = "Employee" }, default)).Data!.Items.Should().HaveCount(2);
        (await svc.ListAsync(new AuditLogQuery { Action = "INSERT" }, default)).Data!.Items.Single().ChangedByName.Should().BeNull();
        (await svc.ListAsync(new AuditLogQuery { RecordId = 5 }, default)).Data!.Items.Should().HaveCount(2);
        (await svc.ListAsync(new AuditLogQuery { ChangedBy = 1 }, default)).Data!.Items.Should().HaveCount(2);
        (await svc.ListAsync(new AuditLogQuery { FromDate = new(2026, 10, 3), ToDate = new(2026, 10, 3) }, default)).Data!.Items.Single().AuditLogId.Should().Be(3);   // gồm cả cuối ngày 3
        (await svc.ListAsync(new AuditLogQuery { Keyword = "Nhập từ" }, default)).Data!.Items.Should().ContainSingle();
        (await svc.GetTableNamesAsync(default)).Data!.Should().Equal("Account", "Employee");
    }
}
