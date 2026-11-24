using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// UTCID787–UTCID819: SpecImportService (Preview / Import / Check). Assert khóa theo
/// sheet SpecImportService trong UNIT_TEST_CASES_REBASELINE_v3.1 — không nới Expected cho Actual lệch spec.
/// </summary>
public class SpecImportServiceTests : ProductionTestBase
{
    private const int ProductId = 1;
    private const int OrderId = 7;
    private const string Body = "Thân trước";
    private const string Red = "Đỏ";
    private const string Gold = "Vàng";

    private static readonly DateTime FixedNow = new(2026, 11, 24);
    private static readonly DateOnly FixedToday = new(2026, 11, 24);

    private readonly IProductSpecService _specs = Substitute.For<IProductSpecService>();
    private readonly IOrderMarkerService _markers = Substitute.For<IOrderMarkerService>();
    private readonly IExcelSpecWorkbookParser _parser = Substitute.For<IExcelSpecWorkbookParser>();
    private readonly List<ProductColor> _colors =
    [
        new() { ProductColorId = 1, ProductId = ProductId, Color = Red, CreatedAt = FixedNow },
    ];
    private readonly List<ProductColor> _txAdded = [];
    private readonly List<string> _colorsEverAdded = [];
    private bool _inTx;

    public SpecImportServiceTests()
    {
        Clock.Now.Returns(FixedNow);
        Clock.Today.Returns(FixedToday);

        SetupQueryable(new List<Product> { new() { ProductId = ProductId, ProductCode = "AO01", Status = RecordStatus.Active } });
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = OrderId, OrderCode = "DH01", Status = OrderStatuses.InProduction },
        });
        SetupQueryable(new List<Material> { Mat(1, "M1"), Mat(2, "M2") });
        WireColors();
        WireTransactions();

        _specs.GetAsync(ProductId, Arg.Any<CancellationToken>()).Returns(Result<ProductSpecDto>.Success(Spec()));
        _specs.CheckAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _specs.WriteAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(ProductId));
        _markers.GetAsync(OrderId, Arg.Any<CancellationToken>()).Returns(Result<OrderMarkersDto>.Success(OrderMarkers()));
        _markers.CheckAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _markers.WriteAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(OrderId));
    }

    private SpecImportService Sut() => new(Uow, _specs, _markers, _parser,
        new SaveProductSpecDtoValidator(), new SaveOrderMarkersDtoValidator(), Clock);

    [Fact]
    public async Task UTCID787_PreviewAsync_Stream_ValidFile_CanImportWithoutSaving()
    {
        // Arrange — product 1 đủ màu/NVL; parser trả workbook có thành phần mới; replace=false
        var stream = Stream();
        var workbook = Book(components: [Panel(4, "Cổ", 1)]);
        _parser.Parse(stream).Returns(workbook);

        // Act
        var result = await Sut().PreviewAsync(ProductId, null, stream, CancellationToken.None, replace: false);

        // Assert — Success, CanImport, plan không tạo màu (createColors:false) nên không SaveChanges
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.CanImport.Should().BeTrue();
        result.Data.Components.Should().ContainSingle(c => c.ComponentName == "Cổ" && c.Action == SpecImportBuilder.ActionNew);
        await Uow.DidNotReceive().SaveChangesAsync();
        _colorsEverAdded.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID788_PreviewAsync_Workbook_MatchesStreamOverload()
    {
        // Arrange
        var stream = Stream();
        var workbook = Book(components: [Panel(4, "Cổ", 1)]);
        _parser.Parse(stream).Returns(workbook);

        // Act
        var fromStream = await Sut().PreviewAsync(ProductId, null, stream, CancellationToken.None, replace: false);
        var fromBook = await Sut().PreviewAsync(ProductId, null, workbook, CancellationToken.None, replace: false);

        // Assert
        fromStream.IsSuccess.Should().BeTrue(fromStream.Message);
        fromBook.IsSuccess.Should().BeTrue(fromBook.Message);
        fromBook.Data.Should().BeEquivalentTo(fromStream.Data);
        fromBook.Data!.CanImport.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID789_PreviewAsync_Workbook_EmptySheets_EmptyPreview()
    {
        // Arrange — sheet có mặt, 0 dòng dữ liệu
        var workbook = EmptySheets();

        // Act
        var result = await Sut().PreviewAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        ShouldBeEmptyPreview(result.Data!);
    }

    [Fact]
    public async Task UTCID790_PreviewAsync_Workbook_Null_ArgumentNullOrFailure()
    {
        // Arrange
        var sut = Sut();

        // Act
        var (thrown, result) = await Capture(() => sut.PreviewAsync(ProductId, null, (SpecWorkbookDto)null!, CancellationToken.None));

        // Assert — ArgumentNullException hoặc Failure. ⚠ Dereference → NullReferenceException.
        AssertNullArgOrFailure(thrown, result);
    }

    [Fact]
    public async Task UTCID791_PreviewAsync_Stream_NewColor_SkipsCrossErrorsAndCanImport()
    {
        // Arrange — màu Vàng chưa có; CheckAsync sẽ lệch nếu bị gọi (colorsToCreate > 0 thì phải bỏ qua)
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(norms: [NormExcel(4, Gold, Body, "M1")]));
        _specs.CheckAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.ProductSpecInvalid, "Nhóm bìa và định mức lệch nhau."));

        // Act
        var result = await Sut().PreviewAsync(ProductId, null, stream, CancellationToken.None);

        // Assert — một màu sẽ tạo, dòng Vàng không Error, CrossErrors bỏ qua → CanImport (preview lạc quan, đúng spec)
        result.IsSuccess.Should().BeTrue(result.Message);
        var row = result.Data!.Norms.Should().ContainSingle().Subject;
        row.Color.Should().Be(Gold);
        row.Action.Should().Be(SpecImportBuilder.ActionNew);
        row.Error.Should().BeNull();
        result.Data.CrossErrors.Should().BeEmpty();
        result.Data.CanImport.Should().BeTrue();
        await _specs.DidNotReceive().CheckAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID792_PreviewAsync_Stream_EmptySheets_EmptyPreview()
    {
        // Arrange
        var stream = Stream();
        _parser.Parse(stream).Returns(EmptySheets());

        // Act
        var result = await Sut().PreviewAsync(ProductId, null, stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        ShouldBeEmptyPreview(result.Data!);
    }

    [Fact]
    public async Task UTCID793_PreviewAsync_Stream_PartName_CaseInsensitive()
    {
        // Arrange — bảng thành phần; tên part/sheet "components" và "Components" phải như nhau
        var workbook = Book(components: [Panel(4, "Cổ", 1)]);
        _parser.Parse(Arg.Any<Stream>()).Returns(workbook);

        // Act + Assert — nhận diện không phân biệt hoa thường. ⚠ Parts phân biệt hoa thường → bỏ "Components".
        foreach (var partName in new[] { "components", "Components" })
        {
            var result = await Sut().PreviewAsync(ProductId, null, Stream(), CancellationToken.None, [partName]);

            result.IsSuccess.Should().BeTrue($"part \"{partName}\" phải được nhận không phân biệt hoa thường. {result.Message}");
            result.Data!.HasComponents.Should().BeTrue();
            result.Data.Components.Should().ContainSingle(c => c.ComponentName == "Cổ");
        }
    }

    [Fact]
    public async Task UTCID794_PreviewAsync_Stream_NormMarkerMismatch_CrossErrorsBlockImport()
    {
        // Arrange — đủ màu (colorsToCreate=0); định mức đổi NVL trong khi nhóm bìa giữ nguyên → Check báo lệch
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(
            norms: [NormExcel(4, Red, Body, "M2")],
            groups: [GroupExcel(5, 1, [Body], [Red])]));
        const string mismatch = "Nhóm bìa và định mức lệch nhau.";
        _specs.CheckAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.ProductSpecInvalid, mismatch));

        // Act
        var result = await Sut().PreviewAsync(ProductId, null, stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.CrossErrors.Should().Contain(mismatch);
        result.Data.CanImport.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID795_PreviewAsync_Stream_ParserThrows_ReturnsFailure()
    {
        // Arrange — stream rác, parser ném lỗi đọc Excel
        var stream = new MemoryStream([9, 9, 9]);
        const string parseError = "File Excel sai mẫu: stream rác.";
        _parser.Parse(stream).Returns(_ => throw new InvalidDataException(parseError));

        // Act — exception phải được nuốt thành Failure, không thoát ra
        var result = await Sut().PreviewAsync(ProductId, null, stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.SpecImportInvalid);
        result.Message.Should().Be(parseError);
    }

    [Fact]
    public async Task UTCID796_PreviewAsync_Stream_UnknownProduct_NotFound()
    {
        // Arrange
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(components: [Panel(4, "Cổ", 1)]));

        // Act
        var result = await Sut().PreviewAsync(999, null, stream, CancellationToken.None);

        // Assert
        ShouldBeNotFound(result);
    }

    [Fact]
    public async Task UTCID797_PreviewAsync_Stream_Null_ArgumentNullOrFailure()
    {
        // Arrange — parser từ chối stream null (ArgumentNullException là ArgumentException, service bắt thành Failure)
        _parser.Parse(null!).Returns(_ => throw new ArgumentNullException("stream"));

        // Act
        var (thrown, result) = await Capture(() => Sut().PreviewAsync(ProductId, null, (Stream)null!, CancellationToken.None));

        // Assert
        AssertNullArgOrFailure(thrown, result);
    }

    [Fact]
    public async Task UTCID798_ImportAsync_Stream_WritesSpecThenMarkersInOneTransaction()
    {
        // Arrange — đổi định mức và thêm bìa; Write/Check/Write đều Success
        var stream = Stream();
        _parser.Parse(stream).Returns(ChangedSpecAndMarker());
        var trace = new List<string>();
        Uow.When(u => u.BeginTransactionAsync()).Do(_ => trace.Add("begin"));
        _specs.When(s => s.WriteAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>()))
            .Do(_ => trace.Add("spec.write"));
        _markers.When(m => m.CheckAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>()))
            .Do(_ => trace.Add("marker.check"));
        _markers.When(m => m.WriteAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>()))
            .Do(_ => trace.Add("marker.write"));
        Uow.When(u => u.CommitTransactionAsync()).Do(_ => trace.Add("commit"));

        // Act
        var result = await Sut().ImportAsync(ProductId, OrderId, stream, CancellationToken.None);

        // Assert — một TX: spec.Write một lần, rồi marker.Check, rồi marker.Write, rồi Commit
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().Be(new SpecImportResultDto(1, 1, 0, 1));
        await _specs.Received(1).WriteAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
        trace.Should().Equal("begin", "spec.write", "marker.check", "marker.write", "commit");
        await Uow.Received(1).CommitTransactionAsync();
        await Uow.DidNotReceive().RollbackTransactionAsync();
    }

    [Fact]
    public async Task UTCID799_ImportAsync_Stream_NewColor_CreatesProductColorAndStripsSize()
    {
        // Arrange — Vàng chưa có ở mã hàng
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(norms: [NormExcel(4, Gold, Body, "M1")]));
        SaveProductSpecDto? written = null;
        _specs.WriteAsync(ProductId, Arg.Do<SaveProductSpecDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(ProductId));

        // Act
        var result = await Sut().ImportAsync(ProductId, null, stream, CancellationToken.None);

        // Assert — ProductColor Vàng, không biến thể size; norm ghi màu Vàng; size "?" bị loại; Commit
        result.IsSuccess.Should().BeTrue(result.Message);
        _colors.Should().Contain(c => c.ProductId == ProductId && c.Color == Gold && c.CreatedAt == FixedNow);
        written.Should().NotBeNull();
        written!.Norms.Should().Contain(n => n.Color == Gold && n.ComponentName == Body && n.MaterialId == 1);
        written.Variants.Should().OnlyContain(v => v.SizeCode != SpecImportBuilder.ColorOnlySize && v.Color != Gold);
        await Uow.Received(1).CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID800_ImportAsync_Workbook_MatchesStreamOverload()
    {
        // Arrange — cùng nội dung với case Normal đầu (UTCID798)
        var stream = Stream();
        var workbook = ChangedSpecAndMarker();
        _parser.Parse(stream).Returns(workbook);

        // Act
        var fromStream = await Sut().ImportAsync(ProductId, OrderId, stream, CancellationToken.None);
        var fromBook = await Sut().ImportAsync(ProductId, OrderId, workbook, CancellationToken.None);

        // Assert
        fromStream.IsSuccess.Should().BeTrue(fromStream.Message);
        fromBook.IsSuccess.Should().BeTrue(fromBook.Message);
        fromBook.Data.Should().Be(fromStream.Data);
        fromBook.Message.Should().Be(fromStream.Message);
        await _specs.Received(2).WriteAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
        await _markers.Received(2).CheckAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>());
        await _markers.Received(2).WriteAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>());
        await Uow.Received(2).CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID801_ImportAsync_Workbook_Empty_NoPartialWrite()
    {
        // Arrange — 0 dòng dữ liệu
        var workbook = EmptySheets();

        // Act
        var result = await Sut().ImportAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert — Success không đổi gì, hoặc Failure vì không có dữ liệu; không ghi spec/bìa
        if (result.IsSuccess)
            result.Data.Should().Be(new SpecImportResultDto(0, 0, 0, 0));
        else
            result.ErrorCode.Should().Be(ErrorCodes.SpecImportInvalid);
        await _specs.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
        await _markers.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>());
        _colors.Select(c => c.Color).Should().Equal(Red);
    }

    [Fact]
    public async Task UTCID802_ImportAsync_Workbook_Null_NoTransaction()
    {
        // Arrange
        var began = false;
        Uow.When(u => u.BeginTransactionAsync()).Do(_ => began = true);
        var sut = Sut();

        // Act
        var (thrown, result) = await Capture(() => sut.ImportAsync(ProductId, null, (SpecWorkbookDto)null!, CancellationToken.None));

        // Assert — ArgumentNullException hoặc Failure, và chưa mở transaction.
        using (new AssertionScope())
        {
            AssertNullArgOrFailure(thrown, result);
            began.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UTCID803_ImportAsync_Stream_NoOrderMarkers_DoesNotCallMarkerService()
    {
        // Arrange — không có bảng bìa của đơn, không chọn đơn
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(components: [Panel(4, "Cổ", 1)]));

        // Act
        var result = await Sut().ImportAsync(ProductId, null, stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        await _markers.DidNotReceive().GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _markers.DidNotReceive().CheckAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>());
        await _markers.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID804_ImportAsync_Stream_NewColorAndComponentError_RollsBackColor()
    {
        // Arrange — Vàng nằm trên dòng định mức, kèm 1 dòng thành phần lỗi → CanImport=false
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(
            components: [new SpecComponentExcelRow(4, "Cổ", ComponentTypes.Panel, 0, null)],
            norms: [NormExcel(5, Gold, "Cổ", "M1")]));

        // Act
        var result = await Sut().ImportAsync(ProductId, null, stream, CancellationToken.None);

        // Assert — Failure, Rollback, ProductColor Vàng tạo trong TX không còn
        result.IsSuccess.Should().BeFalse();
        _colorsEverAdded.Should().Contain(Gold);
        _colors.Select(c => c.Color).Should().NotContain(Gold);
        await Uow.Received(1).RollbackTransactionAsync();
        await Uow.DidNotReceive().CommitTransactionAsync();
        await _specs.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID805_ImportAsync_Stream_UnknownMaterial_FailsAsRowError()
    {
        // Arrange — M99 không có trong danh mục đang dùng (CheckAsync sẽ báo NewMaterial; Import phải Failure)
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(norms: [NormExcel(4, Red, Body, "M99")]));

        // Act
        var result = await Sut().ImportAsync(ProductId, null, stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.SpecImportInvalid);
        result.Message.Should().NotBe(string.Format(Messages.MaterialNotInCatalog, "M99"));
        await _specs.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID806_ImportAsync_Stream_MarkerCheckFails_RollsBackSpec()
    {
        // Arrange
        var stream = Stream();
        _parser.Parse(stream).Returns(ChangedSpecAndMarker());
        _markers.CheckAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.OrderMarkerInvalid, "Bìa lệch định mức."));

        // Act
        var result = await Sut().ImportAsync(ProductId, OrderId, stream, CancellationToken.None);

        // Assert — Failure, Rollback, spec không được commit
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Bìa lệch định mức.");
        await Uow.Received(1).RollbackTransactionAsync();
        await Uow.DidNotReceive().CommitTransactionAsync();
        await _markers.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID807_ImportAsync_Stream_SpecWriteThrows_RollsBack()
    {
        // Arrange
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(components: [Panel(4, Body, 3)]));
        _specs.WriteAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<int>>(new InvalidOperationException("write failed")));

        // Act
        var (thrown, result) = await Capture(() => Sut().ImportAsync(ProductId, null, stream, CancellationToken.None));

        // Assert — exception hoặc Failure thoát ra, và Rollback
        if (thrown is null)
            result!.IsSuccess.Should().BeFalse();
        else
            thrown.Should().BeOfType<InvalidOperationException>();
        await Uow.Received(1).RollbackTransactionAsync();
        await Uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID808_ImportAsync_Stream_ParserThrows_FailsWithoutLeavingTransactionOpen()
    {
        // Arrange
        var stream = new MemoryStream([9, 9, 9]);
        _parser.Parse(stream).Returns(_ => throw new InvalidDataException("File Excel sai mẫu: stream rác."));
        var began = false;
        var rolled = false;
        Uow.When(u => u.BeginTransactionAsync()).Do(_ => began = true);
        Uow.When(u => u.RollbackTransactionAsync()).Do(_ => rolled = true);

        // Act
        var (thrown, result) = await Capture(() => Sut().ImportAsync(ProductId, null, stream, CancellationToken.None));

        // Assert — Failure parse; không mở TX hoặc đã Rollback
        thrown.Should().BeNull();
        result!.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.SpecImportInvalid);
        (began == false || rolled).Should().BeTrue();
        await Uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID809_ImportAsync_Stream_UnknownProduct_NotFound()
    {
        // Arrange
        var stream = Stream();
        _parser.Parse(stream).Returns(Book(components: [Panel(4, "Cổ", 1)]));

        // Act
        var result = await Sut().ImportAsync(999, null, stream, CancellationToken.None);

        // Assert
        ShouldBeNotFound(result);
    }

    [Fact]
    public async Task UTCID810_CheckAsync_ExistingProduct_NoPendingAndCanImport()
    {
        // Arrange — đủ màu và NVL, có một thành phần mới
        var workbook = Book(components: [Panel(4, "Cổ", 1)]);

        // Act
        var result = await Sut().CheckAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        var actions = result.Data!.Components.Select(c => c.Action)
            .Concat(result.Data.Norms.Select(n => n.Action))
            .Concat(result.Data.MarkerGroups.Select(g => g.Action))
            .Concat(result.Data.OrderMarkers.Select(m => m.Action));
        actions.Should().NotContain(SpecImportBuilder.ActionPending);
        actions.Should().NotContain(SpecImportBuilder.ActionNewMaterial);
        result.Data.CanImport.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID811_CheckAsync_NewProduct_AssumedColorIsPending()
    {
        // Arrange — mã hàng chưa tạo; màu trong file được giả định
        var workbook = Book(
            components: [Panel(4, "Cổ", 1)],
            norms: [NormExcel(5, Gold, "Cổ", "M1")]);

        // Act
        var result = await Sut().CheckAsync(null, null, workbook, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Norms.Should().ContainSingle();
        result.Data.Norms[0].Color.Should().Be(Gold);
        result.Data.Norms[0].Action.Should().Be(SpecImportBuilder.ActionPending);
        await _specs.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID812_CheckAsync_MissingColor_NormIsPending()
    {
        // Arrange — product 1 chưa có Vàng
        var workbook = Book(norms: [NormExcel(4, Gold, Body, "M1")]);

        // Act
        var result = await Sut().CheckAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        var row = result.Data!.Norms.Should().ContainSingle().Subject;
        row.Color.Should().Be(Gold);
        row.Action.Should().Be(SpecImportBuilder.ActionPending);
        row.Error.Should().Be(string.Format(Messages.SpecImportPendingColor, Gold));
    }

    [Fact]
    public async Task UTCID813_CheckAsync_MissingMaterial_NewMaterial()
    {
        // Arrange — M99 chưa có; AssumeMaterials gán Id âm để dòng không thành Error
        var workbook = Book(norms: [NormExcel(4, Red, Body, "M99")]);

        // Act
        var result = await Sut().CheckAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        var row = result.Data!.Norms.Should().ContainSingle().Subject;
        row.MaterialCode.Should().Be("M99");
        row.Action.Should().Be(SpecImportBuilder.ActionNewMaterial);
        row.Error.Should().Be(string.Format(Messages.MaterialNotInCatalog, "M99"));
        row.MaterialName.Should().BeNull();
    }

    [Fact]
    public async Task UTCID814_CheckAsync_AssumedColor_MarkerIsPending()
    {
        // Arrange — nhóm bìa và bìa của đơn tham chiếu màu Vàng chưa có; chưa chọn đơn
        var workbook = Book(
            groups: [GroupExcel(4, 1, [Body], [Gold])],
            markers: [MarkerExcel(5, 1, "1B")]);

        // Act
        var result = await Sut().CheckAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        var group = result.Data!.MarkerGroups.Should().ContainSingle().Subject;
        group.Colors.Should().Contain(Gold);
        group.Action.Should().Be(SpecImportBuilder.ActionPending);
        result.Data.OrderMarkers.Should().ContainSingle();
        result.Data.OrderMarkers[0].Action.Should().Be(SpecImportBuilder.ActionPending);
    }

    [Fact]
    public async Task UTCID815_CheckAsync_PendingRow_CanImportFollowsMarkAssumed()
    {
        // Arrange — chỉ một dòng định mức màu Vàng: trước MarkAssumed là New, sau đó là Pending
        var workbook = Book(norms: [NormExcel(4, Gold, Body, "M1")]);

        // Act
        var result = await Sut().CheckAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert — CanImport phải tính lại sau khi dòng thành Pending. ⚠ MarkAssumed không cập nhật CanImport.
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Norms.Should().ContainSingle(n => n.Action == SpecImportBuilder.ActionPending);
        result.Data.CanImport.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID816_CheckAsync_NoOrder_HasOrderMarkersOnlyWhenOrderExists()
    {
        // Arrange — mã chưa tạo, không có đơn, file vẫn có bảng bìa
        var workbook = Book(markers: [MarkerExcel(4, 1, "1A")]);

        // Act
        var result = await Sut().CheckAsync(null, null, workbook, CancellationToken.None);

        // Assert — HasOrderMarkers chỉ true khi có đơn. ⚠ MarkAssumed luôn gán true.
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.OrderId.Should().BeNull();
        result.Data.HasOrderMarkers.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID817_CheckAsync_AssumedColor_SkipsCrossErrors()
    {
        // Arrange — còn màu giả định, định mức và nhóm bìa lệch nhau nếu có chạy Check
        var workbook = Book(
            norms: [NormExcel(4, Gold, Body, "M2")],
            groups: [GroupExcel(5, 1, [Body], [Gold])]);
        _specs.CheckAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.ProductSpecInvalid, "Nhóm bìa và định mức lệch nhau."));

        // Act
        var result = await Sut().CheckAsync(ProductId, null, workbook, CancellationToken.None);

        // Assert — CrossErrors chỉ tính khi đủ màu và NVL thật
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.CrossErrors.Should().BeEmpty();
        await _specs.DidNotReceive().CheckAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID818_CheckAsync_UnknownProduct_NotFound()
    {
        // Arrange
        var workbook = Book(components: [Panel(4, "Cổ", 1)]);

        // Act
        var result = await Sut().CheckAsync(999, null, workbook, CancellationToken.None);

        // Assert — Failure NotFound. ⚠ `productId ?? 0` chỉ áp vào preview sau khi đã tìm thấy mã, không được coi 999 là mã mới.
        ShouldBeNotFound(result);
    }

    [Fact]
    public async Task UTCID819_CheckAsync_NullWorkbook_ArgumentNullOrFailure()
    {
        // Arrange
        var sut = Sut();

        // Act
        var (thrown, result) = await Capture(() => sut.CheckAsync(ProductId, null, null!, CancellationToken.None));

        // Assert — ArgumentNullException hoặc Failure. ⚠ Dereference → NullReferenceException.
        AssertNullArgOrFailure(thrown, result);
    }

    private void WireColors()
    {
        var repo = Substitute.For<IGenericRepository<ProductColor>>();
        repo.Query().Returns(_ => _colors.AsTestAsyncQueryable());
        repo.AddRangeAsync(Arg.Do<IEnumerable<ProductColor>>(rows =>
        {
            foreach (var row in rows.ToList())
            {
                _colors.Add(row);
                _colorsEverAdded.Add(row.Color);
                if (_inTx) _txAdded.Add(row);
            }
        })).Returns(Task.CompletedTask);
        Uow.Repository<ProductColor>().Returns(repo);
    }

    private void WireTransactions()
    {
        Uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        Uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        Uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        Uow.SaveChangesAsync().Returns(1);
        Uow.When(u => u.BeginTransactionAsync()).Do(_ =>
        {
            _inTx = true;
            _txAdded.Clear();
        });
        Uow.When(u => u.CommitTransactionAsync()).Do(_ =>
        {
            _txAdded.Clear();
            _inTx = false;
        });
        Uow.When(u => u.RollbackTransactionAsync()).Do(_ =>
        {
            foreach (var row in _txAdded) _colors.Remove(row);
            _txAdded.Clear();
            _inTx = false;
        });
    }

    private static void ShouldBeEmptyPreview(SpecImportPreviewDto preview)
    {
        preview.Components.Should().BeEmpty();
        preview.Norms.Should().BeEmpty();
        preview.MarkerGroups.Should().BeEmpty();
        preview.OrderMarkers.Should().BeEmpty();
    }

    private static void ShouldBeNotFound<T>(Result<T> result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        result.Message.Should().Be(Messages.NotFound);
    }

    private static void AssertNullArgOrFailure<T>(Exception? thrown, Result<T>? result)
    {
        if (thrown is not null)
            thrown.Should().BeOfType<ArgumentNullException>();
        else
            result!.IsSuccess.Should().BeFalse();
    }

    private static async Task<(Exception? Thrown, Result<T>? Result)> Capture<T>(Func<Task<Result<T>>> act)
    {
        try
        {
            return (null, await act());
        }
        catch (Exception ex)
        {
            return (ex, null);
        }
    }

    private static ProductSpecDto Spec() => new(ProductId,
        [new ProductComponentDto(1, Body, "Panel", 2, "Active")],
        [new ProductVariantDto(10, Red, "S", "Active"), new ProductVariantDto(11, Red, "M", "Active")],
        [new ProductNormDto(100, Body, Red, 1, "M1", "NVL M1", "m", Red, null)],
        [new ProductMarkerComponentDto(1, Body)],
        [new ProductMarkerFabricDto(1, Red, 1, "M1", "NVL M1", Red)]);

    private static OrderMarkersDto OrderMarkers() => new(OrderId,
    [
        new OrderMarkerProductDto(ProductId, "AO01", ["S", "M"], [],
        [
            new OrderMarkerDto(500, 1, "1A", 2.2m, 1.5m, "Active",
                [new MarkerRatioDto("S", 1), new MarkerRatioDto("M", 2)]),
        ]),
    ]);

    /// <summary>Đổi số chi tiết + đổi NVL của định mức + thêm bìa 1B. Kết quả gộp: 1 thành phần, 1 định mức, 0 nhóm, 1 bìa.</summary>
    private static SpecWorkbookDto ChangedSpecAndMarker() => Book(
        components: [Panel(4, Body, 3)],
        norms: [NormExcel(5, Red, Body, "M2")],
        markers: [MarkerExcel(6, 1, "1B")],
        sizeColumns: ["S", "M"]);

    private static SpecWorkbookDto EmptySheets() => new(true, [], true, [], true, [], false, [], []);

    private static SpecWorkbookDto Book(
        SpecComponentExcelRow[]? components = null,
        SpecNormExcelRow[]? norms = null,
        SpecMarkerGroupExcelRow[]? groups = null,
        SpecOrderMarkerExcelRow[]? markers = null,
        string[]? sizeColumns = null) =>
        new(
            components is { Length: > 0 }, components ?? [],
            norms is { Length: > 0 }, norms ?? [],
            groups is { Length: > 0 }, groups ?? [],
            markers is { Length: > 0 }, markers ?? [],
            sizeColumns ?? (markers is { Length: > 0 } ? ["S", "M"] : []));

    private static MemoryStream Stream() => new([1, 2, 3]);

    private static Material Mat(int id, string code) => new()
    {
        MaterialId = id,
        MaterialCode = code,
        MaterialName = $"NVL {code}",
        Color = Red,
        Unit = "m",
        MaterialType = MaterialTypes.Fabric,
        Status = MaterialStatuses.Active,
    };

    private static SpecComponentExcelRow Panel(int row, string name, short pieces) =>
        new(row, name, ComponentTypes.Panel, pieces, null);

    private static SpecNormExcelRow NormExcel(int row, string color, string component, string code) =>
        new(row, color, component, code, null, null);

    private static SpecMarkerGroupExcelRow GroupExcel(int row, int markerNo, string[] components, string[] colors) =>
        new(row, markerNo, components, colors, null);

    private static SpecOrderMarkerExcelRow MarkerExcel(int row, int markerNo, string code) =>
        new(row, "AO01", markerNo, code, 2.0m, 1.5m, "ACTIVE",
            [new MarkerRatioDto("S", 1), new MarkerRatioDto("M", 1)], null);
}
