using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>DEV-ALL-179: nhập công đoạn từ bảng đã đọc và sửa trên màn hình (popup kiểm tra Excel), không đọc lại file.</summary>
[Collection(ExcelTestCollection.Name)]
public class OperationImportEditedSheetTests : ProductionTestBase
{
    private readonly List<Operation> _operations = [];
    private readonly OperationService _sut;

    public OperationImportEditedSheetTests()
    {
        SetupQueryable(new List<Product> { new() { ProductId = 1, ProductCode = "AO01", Status = RecordStatus.Active } });
        SetupQueryable(_operations);
        SetupQueryable(new List<PieceRate>());
        _operations.Add(new Operation
        {
            OperationId = 10, ProductId = 1, OperationCode = "OP01", OperationName = "Ráp vai", StationSequence = 1,
            SkillLevel = 2, StandardTimeSec = 18, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing,
        });
        Repo<Operation>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<Operation>>()))
            .Do(c => _operations.AddRange(c.Arg<IEnumerable<Operation>>()));
        _sut = new OperationService(Uow, Audit, Clock, new ClosedXmlOperationParser(), Notifier);
    }

    private static OperationExcelSheetDto Sheet(params OperationExcelRowDto[] rows) =>
        new("Công đoạn", null, false, rows);

    private static OperationExcelRowDto Row(int row, int stt, string? name, byte? skill = 2, decimal? time = 18,
        decimal? rate = null, string? parseError = null) =>
        new(row, stt, name, skill, time, null, rate, parseError);

    private async Task<IReadOnlyList<OperationImportRowDto>> PreviewRows(params OperationExcelRowDto[] rows) =>
        (await _sut.PreviewImportAsync(1, Sheet(rows), null, default)).Data!.Rows;

    [Fact]
    public async Task Preview_UnchangedAndNewRows_ClassifiedLikeFile()
    {
        var rows = await PreviewRows(Row(4, 1, "Ráp vai"), Row(5, 2, "Kiểm hàng", 1, 20));

        rows.Select(r => r.Action).Should().Equal("Unchanged", "New");
    }

    [Theory]
    [InlineData(null, 2, 18, null, "Vui lòng nhập tên công đoạn.")]
    [InlineData("  ", 2, 18, null, "Vui lòng nhập tên công đoạn.")]
    [InlineData("Ráp vai", 10, 18, null, "Bậc phải là số nguyên từ 1 đến 9.")]
    [InlineData("Ráp vai", 2, -1, null, "TGCT không được âm.")]
    [InlineData("Ráp vai", 2, 18.123, null, "TGCT tối đa 2 số lẻ.")]
    [InlineData("Ráp vai", 2, 18, -5.0, "TIỀN không được âm.")]
    [InlineData("Ráp vai", 2, 18, 100.123, "TIỀN tối đa 2 số lẻ.")]
    public async Task Preview_BadCell_ActionErrorWithReason(string? name, byte skill, double time, double? rate, string expected)
    {
        var rows = await PreviewRows(Row(4, 1, name, skill, (decimal)time, rate is null ? null : (decimal)rate));

        rows.Single().Action.Should().Be("Error");
        rows.Single().Error.Should().Be(expected);
    }

    [Fact]
    public async Task Preview_ParseErrorKept_RowStaysError()
    {
        var rows = await PreviewRows(Row(4, 1, "Ráp vai", parseError: "Cột TGCT không phải số ('abc')."));

        rows.Single().Action.Should().Be("Error");
        rows.Single().Error.Should().Contain("TGCT");
    }

    [Fact]
    public async Task Preview_DuplicateStt_BothRowsError()
    {
        var rows = await PreviewRows(Row(4, 3, "A"), Row(5, 3, "B"));

        rows.Select(r => r.Action).Should().Equal("Error", "Error");
        rows[0].Error.Should().Contain("Trùng STT 3");
    }

    [Fact]
    public async Task Preview_ValidTime_NoFalseError()
    {
        var rows = await PreviewRows(Row(4, 2, "Gắn nhãn", 1, 8.5m));

        rows.Single().Action.Should().Be("New");
        rows.Single().Error.Should().BeNull();
    }
}
