using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Services;

namespace Application.Tests;

/// <summary>KI-01: nhập công đoạn từ file Excel quy trình công nghệ (sheet "BƯỚC CÔNG VIỆC").</summary>
[Collection(ExcelTestCollection.Name)]
public class OperationImportTests : ProductionTestBase
{
    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "LUNA", ProductName = "Áo dạ" }];
    private readonly List<Operation> _operations = [];
    private readonly List<PieceRate> _rates = [];
    private readonly OperationService _sut;
    private int _nextId = 100;
    private int _nextRateId = 500;

    public OperationImportTests()
    {
        SetupQueryable(_products);
        SetupQueryable(_operations);
        SetupQueryable(_rates);
        Repo<PieceRate>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<PieceRate>>())).Do(c =>
        {
            foreach (var rate in c.Arg<IEnumerable<PieceRate>>())
            {
                rate.PieceRateId = _nextRateId++;
                _rates.Add(rate);
            }
        });
        Repo<Operation>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<Operation>>())).Do(c =>
        {
            foreach (var op in c.Arg<IEnumerable<Operation>>())
            {
                op.OperationId = _nextId++;
                _operations.Add(op);
            }
        });
        _sut = new OperationService(Uow, Audit, Clock, new ClosedXmlOperationParser(), Notifier);
    }

    /// <summary>Mô phỏng sheet quy trình: tiêu đề ở dòng 6, có dòng nhóm, chỗ trống đã đánh số và dòng tổng.</summary>
    private static MemoryStream Workbook(Action<IXLWorkbook>? extra = null)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("gốc(trơn)");
        ws.Cell(1, 1).Value = "CTY CP MAY";
        ws.Cell(3, 1).Value = "MÃ : LUNA(màu trơn)-   KHÁCH HÀNG : H&M";
        string[] headers = ["STT", "BƯỚC CÔNG VIỆC", "BẬC", "TGCT", "TGQĐ", "TIỀN", "THIẾT BỊ"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(6, c + 1).Value = headers[c];

        void Row(int r, int stt, string? name, int? skill, double? time, string? equipment = null)
        {
            ws.Cell(r, 1).Value = stt;
            if (name is not null) ws.Cell(r, 2).Value = name;
            if (skill is not null) ws.Cell(r, 3).Value = skill.Value;
            if (time is not null) ws.Cell(r, 4).Value = time.Value;
            ws.Cell(r, 6).Value = 19.7;
            if (equipment is not null) ws.Cell(r, 7).Value = equipment;
        }

        Row(7, 1, "KT cơi  lót túi sườn", 1, 5, "Thợ phụ");
        Row(8, 2, "KT nẹp ve", 1, 5, "Thợ phụ");
        Row(9, 3, null, null, null);                       // chỗ trống đã đánh số
        ws.Cell(10, 2).Value = "LÀ";                       // tiêu đề nhóm, không có STT
        Row(11, 4, "Là dây treo", 3, 7, "Là");
        ws.Cell(12, 2).Value = "148.4";                    // dòng tổng cuối bảng
        ws.Cell(12, 4).Value = 4007;
        Row(13, 5, "Dự Phòng", 3, 25);                     // dòng dự phòng, bỏ qua

        extra?.Invoke(wb);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    // ========== Parser ==========

    [Fact]
    public void Parser_ReadsOperationRows_SkipsSectionHeadersBlanksAndTotals()
    {
        var sheets = new ClosedXmlOperationParser().Parse(Workbook());

        var sheet = sheets.Should().ContainSingle().Subject;
        sheet.SheetName.Should().Be("gốc(trơn)");
        sheet.ProductText.Should().Be("MÃ : LUNA(màu trơn)- KHÁCH HÀNG : H&M");
        sheet.Rows.Select(r => r.Stt).Should().Equal(1, 2, 4);
        var first = sheet.Rows[0];
        first.Name.Should().Be("KT cơi lót túi sườn");   // gộp khoảng trắng thừa
        first.SkillLevel.Should().Be(1);
        first.StandardTimeSec.Should().Be(5m);
        first.Equipment.Should().Be("Thợ phụ");
        first.ParseError.Should().BeNull();
    }

    [Fact]
    public void Parser_IgnoresHiddenSheets()
    {
        using var stream = Workbook(wb =>
        {
            var hidden = wb.Worksheets.Add("nháp");
            hidden.Cell(1, 1).Value = "STT";
            hidden.Cell(1, 2).Value = "BƯỚC CÔNG VIỆC";
            hidden.Cell(1, 4).Value = "TGCT";
            hidden.Cell(2, 1).Value = 1;
            hidden.Cell(2, 2).Value = "Việc cũ";
            hidden.Visibility = XLWorksheetVisibility.Hidden;
        });

        var sheets = new ClosedXmlOperationParser().Parse(stream);

        sheets.Select(s => s.SheetName).Should().Equal("gốc(trơn)");
    }

    [Fact]
    public void Parser_WorkbookWithoutProcessSheet_Throws()
    {
        using var wb = new XLWorkbook();
        wb.Worksheets.Add("Sheet1").Cell(1, 1).Value = "Không liên quan";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var act = () => new ClosedXmlOperationParser().Parse(ms);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parser_InvalidSkill_MarksRowAsError()
    {
        using var stream = Workbook(wb => wb.Worksheet(1).Cell(8, 3).Value = 12);

        var rows = new ClosedXmlOperationParser().Parse(stream)[0].Rows;

        rows.Single(r => r.Stt == 2).ParseError.Should().Contain("Bậc");
        rows.Single(r => r.Stt == 1).ParseError.Should().BeNull();
    }

    // ========== Xem trước ==========

    [Fact]
    public async Task Preview_NewProduct_AllRowsAreNewWithGeneratedCodes()
    {
        var result = await _sut.PreviewImportAsync(1, Workbook(), null, null, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.NewCount.Should().Be(3);
        result.Data.Rows.Select(r => r.OperationCode).Should().Equal("OP01", "OP02", "OP04");
        result.Data.ProductHasFinalStep.Should().BeFalse();
        _operations.Should().BeEmpty();   // xem trước không ghi gì
    }

    [Fact]
    public async Task Preview_ExistingOperations_ClassifiesUpdateAndUnchanged()
    {
        _operations.Add(new Operation { OperationId = 1, ProductId = 1, OperationCode = "OP01", OperationName = "KT cơi lót túi sườn",
            StationSequence = 1, SkillLevel = 1, StandardTimeSec = 5, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing });
        _operations.Add(new Operation { OperationId = 2, ProductId = 1, OperationCode = "OP02", OperationName = "Tên cũ",
            StationSequence = 2, SkillLevel = 1, StandardTimeSec = 5, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing });

        var result = await _sut.PreviewImportAsync(1, Workbook(), null, null, default);

        result.Data!.Rows.Select(r => r.Action).Should().Equal("Unchanged", "Update", "New");
    }

    [Fact]
    public async Task Preview_UnknownProduct_ReturnsNotFound()
    {
        var result = await _sut.PreviewImportAsync(99, Workbook(), null, null, default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Preview_UnknownSheet_ReturnsValidationError()
    {
        var result = await _sut.PreviewImportAsync(1, Workbook(), "không có", null, default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationImportSheetNotFound);
    }

    [Fact]
    public async Task Preview_FileWithoutProcessSheet_ReturnsExcelInvalid()
    {
        using var wb = new XLWorkbook();
        wb.Worksheets.Add("A").Cell(1, 1).Value = "x";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var result = await _sut.PreviewImportAsync(1, ms, null, null, default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationExcelInvalid);
    }

    // ========== Nhập ==========

    [Fact]
    public async Task Import_CreatesOperationsAsActiveSewingAndLogsEach()
    {
        var result = await _sut.ImportAsync(1, Workbook(), null, null, null, false, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Created.Should().Be(3);
        _operations.Should().HaveCount(3).And.OnlyContain(o => o.Status == OperationStatuses.Active
            && o.TeamType == TeamTypes.Sewing && !o.IsFinalStep);
        _operations.Single(o => o.OperationCode == "OP04").StationSequence.Should().Be(4);
        Audit.Received(3).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Operation) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task Import_ExistingRow_IsUpdatedNotDuplicated()
    {
        _operations.Add(new Operation { OperationId = 1, ProductId = 1, OperationCode = "OP02", OperationName = "Tên cũ",
            StationSequence = 2, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing });

        var result = await _sut.ImportAsync(1, Workbook(), null, null, null, false, default);

        result.Data!.Should().BeEquivalentTo(new OperationImportResultDto(2, 1, 0, 0, 0, 0, 0, 0));
        _operations.Count(o => o.OperationCode == "OP02").Should().Be(1);
        _operations.Single(o => o.OperationCode == "OP02").OperationName.Should().Be("KT nẹp ve");
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task Import_WithFinalStt_MarksThatOperationAsFinal()
    {
        var result = await _sut.ImportAsync(1, Workbook(), null, 4, null, false, default);

        result.IsSuccess.Should().BeTrue();
        _operations.Single(o => o.IsFinalStep).OperationCode.Should().Be("OP04");
    }

    [Fact]
    public async Task Import_FinalSttNotInFile_IsRejectedAndWritesNothing()
    {
        var result = await _sut.ImportAsync(1, Workbook(), null, 3, null, false, default);   // STT 3 là chỗ trống

        result.ErrorCode.Should().Be(ErrorCodes.OperationImportFinalNotFound);
        _operations.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_ProductAlreadyHasFinalStep_RejectsAnotherFinal()
    {
        _operations.Add(new Operation { OperationId = 1, ProductId = 1, OperationCode = "OP99", OperationName = "Đóng gói",
            StationSequence = 99, IsFinalStep = true, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing });

        var result = await _sut.ImportAsync(1, Workbook(), null, 4, null, false, default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationFinalStepExists);
        _operations.Should().HaveCount(1);
    }

    [Fact]
    public async Task Import_ErrorRowsAreSkippedAndCounted()
    {
        using var stream = Workbook(wb => wb.Worksheet(1).Cell(8, 3).Value = 12);   // STT 2 có bậc sai

        var result = await _sut.ImportAsync(1, stream, null, null, null, false, default);

        result.Data!.Created.Should().Be(2);
        result.Data.Skipped.Should().Be(1);
        _operations.Select(o => o.OperationCode).Should().Equal("OP01", "OP04");
    }

    // ========== Đơn giá từ cột TIỀN ==========

    private static readonly DateOnly RateDate = new(2026, 11, 1);

    private Operation ExistingOp(int id, string code, string name)
    {
        var op = new Operation { OperationId = id, ProductId = 1, OperationCode = code, OperationName = name,
            StationSequence = short.Parse(code[2..]), SkillLevel = 1, StandardTimeSec = 5, Status = OperationStatuses.Active,
            TeamType = TeamTypes.Sewing };
        _operations.Add(op);
        return op;
    }

    private PieceRate ExistingRate(int id, Operation op, decimal rate, DateOnly date, string status)
    {
        var r = new PieceRate { PieceRateId = id, OperationId = op.OperationId, Operation = op, Rate = rate,
            EffectiveDate = date, Status = status };
        _rates.Add(r);
        return r;
    }

    [Fact]
    public void Parser_ReadsRateColumn()
    {
        var sheet = new ClosedXmlOperationParser().Parse(Workbook())[0];

        sheet.HasRateColumn.Should().BeTrue();
        sheet.Rows.Should().OnlyContain(r => r.Rate == 19.7m);
    }

    [Fact]
    public async Task Preview_WithoutEffectiveDate_DoesNotPlanRates()
    {
        var result = await _sut.PreviewImportAsync(1, Workbook(), null, null, default);

        result.Data!.HasRateColumn.Should().BeTrue();
        result.Data.Rows.Should().OnlyContain(r => r.RateAction == "None");
    }

    [Fact]
    public async Task Preview_WithEffectiveDate_PlansNewRatesForNewOperations()
    {
        var result = await _sut.PreviewImportAsync(1, Workbook(), null, RateDate, default);

        result.Data!.RateNewCount.Should().Be(3);
        result.Data.Rows.Should().OnlyContain(r => r.RateAction == "New" && r.Rate == 19.7m);
        _rates.Should().BeEmpty();   // xem trước không ghi gì
    }

    [Fact]
    public async Task Import_WithEffectiveDate_CreatesDraftRatesAndLogsThem()
    {
        var result = await _sut.ImportAsync(1, Workbook(), null, null, RateDate, false, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.RateCreated.Should().Be(3);
        _rates.Should().HaveCount(3).And.OnlyContain(r => r.Status == PieceRateStatuses.Draft
            && r.Rate == 19.7m && r.EffectiveDate == RateDate && r.SubmittedAt == null);
        _rates.Select(r => r.OperationId).Should().BeEquivalentTo(_operations.Select(o => o.OperationId));
        Audit.Received(3).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(PieceRate) && e.Action == AuditActions.Insert));
        await Notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
    }

    [Fact]
    public async Task Import_WithSubmit_CreatesPendingRatesAndNotifiesAdminOnce()
    {
        var result = await _sut.ImportAsync(1, Workbook(), null, null, RateDate, true, default);

        result.IsSuccess.Should().BeTrue();
        _rates.Should().HaveCount(3).And.OnlyContain(r => r.Status == PieceRateStatuses.PendingApproval && r.SubmittedAt == Now);
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_WithoutEffectiveDate_IgnoresRateColumn()
    {
        var result = await _sut.ImportAsync(1, Workbook(), null, null, null, true, default);

        result.Data!.RateCreated.Should().Be(0);
        _rates.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_ExistingRates_AreUpdatedKeptOrSkippedByStatus()
    {
        var op1 = ExistingOp(1, "OP01", "KT cơi lót túi sườn");   // Nháp cùng ngày, giá khác → cập nhật
        var op2 = ExistingOp(2, "OP02", "KT nẹp ve");             // đã duyệt cùng ngày, giá khác → bỏ qua
        var op4 = ExistingOp(4, "OP04", "Là dây treo");           // giá hiện hành đã bằng giá trong file → không đổi
        var draft = ExistingRate(1, op1, 10m, RateDate, PieceRateStatuses.Draft);
        ExistingRate(2, op2, 12m, RateDate, PieceRateStatuses.Approved);
        ExistingRate(3, op4, 19.7m, RateDate.AddMonths(-1), PieceRateStatuses.Approved);

        var preview = await _sut.PreviewImportAsync(1, Workbook(), null, RateDate, default);
        var result = await _sut.ImportAsync(1, Workbook(), null, null, RateDate, false, default);

        preview.Data!.Rows.Select(r => r.RateAction).Should().Equal("Update", "Skipped", "Unchanged");
        result.Data!.Should().BeEquivalentTo(new { RateCreated = 0, RateUpdated = 1, RateUnchanged = 1, RateSkipped = 1 });
        draft.Rate.Should().Be(19.7m);
        _rates.Single(r => r.PieceRateId == 2).Rate.Should().Be(12m);   // bản đã duyệt không bị đụng tới
        _rates.Should().HaveCount(3);
    }

    [Fact]
    public async Task Import_RejectedRateOnSameDate_GoesBackToDraftOrPending()
    {
        var op1 = ExistingOp(1, "OP01", "KT cơi lót túi sườn");
        var rejected = ExistingRate(1, op1, 19.7m, RateDate, PieceRateStatuses.Rejected);
        rejected.DecisionNote = "Sai giá";

        await _sut.ImportAsync(1, Workbook(), null, null, RateDate, true, default);

        rejected.Status.Should().Be(PieceRateStatuses.PendingApproval);
        rejected.DecisionNote.Should().BeNull();
    }
}
