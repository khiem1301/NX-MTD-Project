using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>DEV-ALL-116: nhập công đoạn kèm cột NHÓM CÔNG ĐOẠN, CHI TIẾT, CUỐI.</summary>
[Collection(ExcelTestCollection.Name)]
public class OperationImportGroupTests : ProductionTestBase
{
    private readonly List<Operation> _operations = [];
    private readonly List<OperationGroup> _groups = [new() { OperationGroupId = 50, ProductId = 1, GroupName = "Cũ" }];
    private readonly List<OperationComponent> _links = [];
    private readonly List<OperationGroup> _addedGroups = [];
    private readonly OperationService _sut;

    public OperationImportGroupTests()
    {
        SetupQueryable(new List<Product> { new() { ProductId = 1, ProductCode = "AO01", Status = RecordStatus.Active } });
        SetupQueryable(_operations);
        SetupQueryable(new List<PieceRate>());
        SetupQueryable(_groups);
        SetupQueryable(_links);
        SetupQueryable(new List<ProductComponent>
        {
            new() { ProductComponentId = 1, ProductId = 1, ComponentName = "Thân", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active },
            new() { ProductComponentId = 2, ProductId = 1, ComponentName = "Tay", ComponentType = ComponentTypes.Panel, Status = RecordStatus.Active },
            new() { ProductComponentId = 3, ProductId = 1, ComponentName = "Nhãn", ComponentType = ComponentTypes.Trim, Status = RecordStatus.Active },
        });
        var existing = new Operation
        {
            OperationId = 10,
            ProductId = 1,
            OperationCode = "OP01",
            OperationName = "Ráp vai",
            StationSequence = 1,
            SkillLevel = 2,
            StandardTimeSec = 18,
            OperationGroupId = 50,
            Status = OperationStatuses.Active,
            TeamType = TeamTypes.Sewing,
        };
        _operations.Add(existing);
        _links.Add(new OperationComponent { OperationId = 10, ProductComponentId = 1, ProductId = 1 });
        Repo<OperationGroup>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<OperationGroup>>()))
            .Do(c => _addedGroups.AddRange(c.Arg<IEnumerable<OperationGroup>>()));
        Repo<Operation>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<Operation>>())).Do(c => _operations.AddRange(c.Arg<IEnumerable<Operation>>()));
        _sut = new OperationService(Uow, Audit, Clock, new ClosedXmlOperationParser(), Notifier);
    }

    private static MemoryStream Workbook(params object?[][] rows)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Công đoạn");
        string[] headers = ["STT", "BƯỚC CÔNG VIỆC", "BẬC", "TGCT", "NHÓM CÔNG ĐOẠN", "CHI TIẾT", "CUỐI", "TIỀN"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
        {
            ws.Cell(4 + r, 8).Value = 1000;   // đơn giá bắt buộc (DEV-ALL-190)
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = ws.Cell(4 + r, c + 1);
                switch (rows[r][c])
                {
                    case null: break;
                    case int i: cell.Value = i; break;
                    case var v: cell.Value = v.ToString(); break;
                }
            }
        }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Parser_ReadsOptionalGroupComponentAndFinalColumns()
    {
        using var stream = Workbook([1, "Ráp vai", 2, 18, "May thân", "Thân; Tay", "x"]);

        var sheet = new ClosedXmlOperationParser().Parse(stream).Single();

        sheet.HasGroupColumn.Should().BeTrue();
        sheet.HasComponentColumn.Should().BeTrue();
        var row = sheet.Rows.Single();
        row.GroupName.Should().Be("May thân");
        row.ComponentNames.Should().Equal("Thân", "Tay");
        row.IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_GroupOrComponentChange_IsUpdateWithChangeNote()
    {
        using var stream = Workbook([1, "Ráp vai", 2, 18, "May thân", "thân; tay", null], [2, "Kiểm hàng", 1, 20, null, null, "x"]);

        var preview = (await _sut.PreviewImportAsync(1, stream, null, null, default)).Data!;

        var row = preview.Rows[0];
        row.Action.Should().Be("Update");
        row.ChangeNote.Should().Contain(string.Format(Messages.OperationImportGroupChange, "Cũ", "May thân"))
            .And.Contain(string.Format(Messages.OperationImportComponentChange, "Thân", "Thân, Tay"));
        row.ComponentNames.Should().Equal("Thân", "Tay");
        preview.FinalSttInFile.Should().Be(2);
        preview.HasGroupColumn.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_SameGroupAndComponents_IsUnchanged()
    {
        using var stream = Workbook([1, "Ráp vai", 2, 18, "cũ", "Thân", null]);

        var preview = (await _sut.PreviewImportAsync(1, stream, null, null, default)).Data!;

        preview.Rows.Single().Action.Should().Be("Unchanged");
    }

    [Fact]
    public async Task Preview_UnknownOrTrimComponent_IsWarningNotError()
    {
        using var stream = Workbook(
            [1, "Ráp vai", 2, 18, null, "Ống tay", null],
            [2, "Gắn nhãn", 1, 8, null, "Nhãn", null],
            [3, "Là", 1, 10, null, "Thân; Ống tay", null]);

        var data = (await _sut.PreviewImportAsync(1, stream, null, null, default)).Data!;

        data.Rows.Select(r => r.Action).Should().NotContain("Error");
        data.Rows.Should().OnlyContain(r => r.Error == null);
        data.WarningCount.Should().Be(3);
        data.Rows[0].Warning.Should().Contain(string.Format(Messages.OperationImportComponentUnknown, "Ống tay"));
        data.Rows[1].Warning.Should().Contain(string.Format(Messages.OperationImportComponentNotPanel, "Nhãn"));
        data.Rows[2].Warning.Should().Contain("Ống tay").And.NotContain("Thân");   // Thân gắn được, chỉ Ống tay bị bỏ
        data.Rows[2].ComponentNames.Should().Equal("Thân");
    }

    [Fact]
    public async Task Preview_TwoFinals_StillAnError()
    {
        using var stream = Workbook(
            [3, "Là", 1, 10, null, null, "x"],
            [4, "Kiểm hàng", 1, 20, null, null, "x"]);

        var rows = (await _sut.PreviewImportAsync(1, stream, null, null, default)).Data!.Rows;

        rows.Select(r => r.Action).Should().AllBe("Error");
        rows[0].Error.Should().Be(Messages.OperationImportManyFinals);
    }

    [Fact]
    public async Task Import_CreatesNewGroupOnceAndLinksComponents()
    {
        using var stream = Workbook(
            [1, "Ráp vai", 2, 18, null, "", null],
            [2, "Lộn tay", 1, 15, "May tay", "Tay", null],
            [3, "Tra tay", 3, 40, "May tay", "Tay; Thân", null]);

        var result = await _sut.ImportAsync(1, stream, null, null, null, false, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _addedGroups.Should().ContainSingle().Which.GroupName.Should().Be("May tay");
        var lon = _operations.Single(o => o.OperationCode == "OP02");
        var tra = _operations.Single(o => o.OperationCode == "OP03");
        lon.OperationGroup.Should().BeSameAs(tra.OperationGroup);
        tra.OperationComponents.Select(c => c.ProductComponentId).Should().BeEquivalentTo([2, 1]);
        var rap = _operations.Single(o => o.OperationCode == "OP01");
        rap.OperationGroupId.Should().BeNull();                            // ô nhóm trống: bỏ nhóm
        Repo<OperationComponent>().Received().RemoveRange(Arg.Is<IEnumerable<OperationComponent>>(l => l.Single().ProductComponentId == 1));
    }
}
