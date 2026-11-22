using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES §1 — OperationService UTCID01–UTCID70.</summary>
[Collection(ExcelTestCollection.Name)]
[Trait("Category", "HighComplexity")]
public sealed class OperationServiceHighComplexityTests : ProductionTestBase
{
    private static readonly DateTime SpecNow = new(2026, 11, 22, 10, 0, 0);
    private static readonly DateOnly SpecToday = new(2026, 11, 22);

    /// <summary>Business error codes from spec not yet in <see cref="ErrorCodes"/> (Group B asserts).</summary>
    private const string ExpectedDuplicateSequence = "OPERATION_SEQUENCE_DUPLICATE";
    private const string ExpectedFinalStepRequired = "OPERATION_FINAL_STEP_REQUIRED";

    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "MH001", Status = RecordStatus.Active }];
    private readonly List<Operation> _operations = [];
    private readonly List<PieceRate> _rates = [];
    private readonly List<OperationGroup> _groups = [];
    private readonly List<ProductComponent> _components = [];
    private readonly List<OperationComponent> _opComponents = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<WorkAllocation> _allocations = [];
    private readonly List<VwWorkerOutputCap> _caps = [];
    private readonly List<OperationAssignment> _assignments = [];
    private readonly List<OutputRecord> _outputs = [];
    private readonly List<DefectRecord> _defects = [];

    private readonly IExcelOperationParser _excel = Substitute.For<IExcelOperationParser>();
    private readonly OperationService _sut;
    private int _nextOpId = 1;
    private int _nextGroupId = 1;

    public OperationServiceHighComplexityTests()
    {
        Clock.Now.Returns(SpecNow);
        Clock.Today.Returns(SpecToday);
        Uow.SaveChangesAsync().Returns(1);

        SetupQueryable(_products);
        SetupQueryable(_operations);
        SetupQueryable(_rates);
        SetupQueryable(_groups);
        SetupQueryable(_components);
        SetupQueryable(_opComponents);
        SetupQueryable(_orderLines);
        SetupQueryable(_allocations);
        SetupQueryable(_caps);
        SetupQueryable(_assignments);
        SetupQueryable(_outputs);
        SetupQueryable(_defects);

        Repo<Operation>().When(r => r.AddAsync(Arg.Any<Operation>())).Do(c => CaptureAddedOperation(c.Arg<Operation>()));
        Repo<Operation>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<Operation>>())).Do(c =>
        {
            foreach (var op in c.Arg<IEnumerable<Operation>>()) CaptureAddedOperation(op);
        });
        // EF-less mock: Remove must update backing list (Import replace)
        Repo<Operation>().When(r => r.Remove(Arg.Any<Operation>())).Do(c => _operations.Remove(c.Arg<Operation>()));
        Repo<PieceRate>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<PieceRate>>())).Do(c =>
        {
            foreach (var rate in c.Arg<IEnumerable<PieceRate>>()) _rates.Add(rate);
        });
        Repo<OperationGroup>().When(r => r.AddAsync(Arg.Any<OperationGroup>())).Do(c =>
        {
            var g = c.Arg<OperationGroup>();
            if (g.OperationGroupId == 0) g.OperationGroupId = _nextGroupId++;
            _groups.Add(g);
        });

        _sut = new OperationService(Uow, Audit, Clock, _excel, Notifier);
    }

    private Product Product(int id = 1) => _products.Single(p => p.ProductId == id);

    /// <summary>Giả lập EF: gán Id + fix-up FK từ navigation khi Add.</summary>
    private void CaptureAddedOperation(Operation op)
    {
        if (op.OperationId == 0) op.OperationId = _nextOpId++;
        if (op.OperationGroup is not null)
            op.OperationGroupId = op.OperationGroup.OperationGroupId;
        if (!_operations.Contains(op)) _operations.Add(op);
    }

    private Operation AddOp(int id, string code, short seq = 10, bool final = false, string status = OperationStatuses.Active,
        string? name = null)
    {
        var op = new Operation
        {
            OperationId = id, ProductId = 1, Product = Product(), OperationCode = code,
            OperationName = name ?? "Công đoạn " + code, StationSequence = seq,
            TeamType = TeamTypes.Sewing, IsFinalStep = final, Status = status,
            OperationComponents = [],
        };
        _operations.Add(op);
        Repo<Operation>().GetByIdAsync(id).Returns(op);
        if (_nextOpId <= id) _nextOpId = id + 1;
        return op;
    }

    private PieceRate AddRate(int id, int operationId, decimal rate, DateOnly date, string status = PieceRateStatuses.Approved)
    {
        var op = _operations.Single(o => o.OperationId == operationId);
        var r = new PieceRate { PieceRateId = id, OperationId = operationId, Rate = rate, EffectiveDate = date, Status = status, Operation = op };
        _rates.Add(r);
        return r;
    }

    private static SaveOperationDto Dto(string code = "OP99", short seq = 99, int productId = 1, string name = "May cổ",
        decimal? std = 45m, bool final = false, string? group = null, IReadOnlyList<int>? componentIds = null) =>
        new(productId, code, name, seq, 1, std, "Sewing", final, group, componentIds);

    private static MemoryStream StreamStub() => new([0x50, 0x4B]);

    private static OperationExcelRowDto ExcelRow(int stt, string name = "May", bool final = false, string? parseError = null,
        decimal? rate = null, IReadOnlyList<string>? componentNames = null, string? group = null) =>
        new(1, stt, name, 1, 5m, null, rate, parseError, group, componentNames, final);

    private static OperationExcelSheetDto Sheet(string name, IEnumerable<OperationExcelRowDto> rows, bool hasRate = true,
        bool hasGroup = false, bool hasComponent = false) =>
        new(name, "MH001", hasRate, rows.ToList(), hasGroup, hasComponent);

    private void MockExcelSheets(params OperationExcelSheetDto[] sheets) =>
        _excel.Parse(Arg.Any<Stream>()).Returns(sheets);

    private static OperationExcelSheetDto ThreeRowImportSheet(bool lastFinal = true) =>
        Sheet("CongDoan",
        [
            ExcelRow(10, "May 1"),
            ExcelRow(20, "May 2"),
            ExcelRow(30, "May 3", final: lastFinal, rate: 100m),
        ]);

    private OrderLine OpenLine(int lineId, string orderStatus = OrderStatuses.InProduction) =>
        new()
        {
            OrderLineId = lineId, OrderId = lineId,
            Order = new CustomerOrder { OrderId = lineId, Status = orderStatus },
        };

    private void SeedUtc01List()
    {
        AddOp(1, "OP01", 1);
        AddOp(2, "OP02", 2);
        AddOp(3, "OP03", 3, status: OperationStatuses.Discontinued);
        AddRate(1, 1, 1_500m, new DateOnly(2026, 11, 1));
    }

    // ==================== ListAsync ====================

    [Fact]
    public async Task UTCID01_ListAsync_ExcludeDiscontinued_ReturnsActiveOnly()
    {
        // Arrange
        SeedUtc01List();

        // Act
        var result = await _sut.ListAsync(1, includeDiscontinued: false, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.Select(o => o.OperationCode).Should().Equal("OP01", "OP02");
        result.Data[0].CurrentRate.Should().Be(1_500m);
    }

    [Fact]
    public async Task UTCID02_ListAsync_IncludeDiscontinued_ReturnsThreeWithOp03Discontinued()
    {
        // Arrange
        SeedUtc01List();

        // Act
        var result = await _sut.ListAsync(1, includeDiscontinued: true, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        result.Data!.Single(o => o.OperationCode == "OP03").Status.Should().Be("Discontinued");
    }

    [Fact]
    public async Task UTCID03_ListAsync_ProductIdZero_ReturnsValidationAndDoesNotQueryOperations()
    {
        // Arrange
        Repo<Operation>().ClearReceivedCalls();

        // Act
        var result = await _sut.ListAsync(0, false, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.OperationInvalid);
        Repo<Operation>().DidNotReceive().Query();
    }

    [Fact]
    public async Task UTCID04_ListAsync_ProductIdNegative_ReturnsValidation()
    {
        // Arrange

        // Act
        var result = await _sut.ListAsync(-1, false, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.OperationInvalid);
    }

    [Fact]
    public async Task UTCID05_ListAsync_ProductExistsNoOperations_ReturnsEmptySuccess()
    {
        // Arrange

        // Act
        var result = await _sut.ListAsync(1, false, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID06_ListAsync_RateEffectiveToday_IsCurrentUpcomingNull()
    {
        // Arrange
        AddOp(1, "OP01", 1);
        AddRate(1, 1, 2_000m, SpecToday);

        // Act
        var result = await _sut.ListAsync(1, false, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var op = result.Data!.Single();
        op.CurrentRate.Should().Be(2_000m);
        op.UpcomingRate.Should().BeNull();
    }

    [Fact]
    public async Task UTCID07_ListAsync_FutureApprovedAndPending_NextAndUpcomingDiffer()
    {
        // Arrange
        AddOp(1, "OP01", 1);
        AddRate(1, 1, 1_000m, SpecToday.AddDays(-10));
        AddRate(2, 1, 1_200m, new DateOnly(2026, 12, 1));
        AddRate(3, 1, 1_400m, new DateOnly(2026, 12, 15), PieceRateStatuses.PendingApproval);

        // Act
        var result = await _sut.ListAsync(1, false, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var op = result.Data!.Single();
        op.UpcomingRate!.EffectiveDate.Should().Be(new DateOnly(2026, 12, 1));
        op.NextRate!.Status.Should().Be("PendingApproval");
        op.NextRate.EffectiveDate.Should().Be(new DateOnly(2026, 12, 15));
        op.NextRate.Rate.Should().NotBe(op.UpcomingRate.Rate);
    }

    [Fact]
    public async Task UTCID08_ListAsync_UnknownProduct_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await _sut.ListAsync(999, false, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    // ==================== CreateAsync ====================

    [Fact]
    public async Task UTCID09_CreateAsync_Valid_CreatesWithTransactionAuditAndTwoSaves()
    {
        // Arrange
        var dto = Dto("OP10", 10, name: "May cổ");

        // Act
        var result = await _sut.CreateAsync(dto, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
        await Uow.Received(1).BeginTransactionAsync();
        await Uow.Received(2).SaveChangesAsync();
        await Uow.Received(1).CommitTransactionAsync();
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Operation) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task UTCID10_CreateAsync_NewFinalStep_MovesFinalFromExisting()
    {
        // Arrange
        var old = AddOp(20, "OP20", 20, final: true);

        // Act
        var result = await _sut.CreateAsync(Dto("OP30", 30, final: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        old.IsFinalStep.Should().BeFalse();
        _operations.Single(o => o.OperationCode == "OP30").IsFinalStep.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID11_CreateAsync_WithGroupAndComponents_LinksCorrectly()
    {
        // Arrange — seed Group + Components; giữ CaptureAddedOperation (không ghi đè AddAsync chỉ capture)
        _groups.Add(new OperationGroup { OperationGroupId = 5, ProductId = 1, GroupName = "Lắp ráp" });
        if (_nextGroupId <= 5) _nextGroupId = 6;
        _components.AddRange(
        [
            new ProductComponent { ProductComponentId = 1, ProductId = 1, ComponentName = "Thân trước", ComponentType = ComponentTypes.Panel },
            new ProductComponent { ProductComponentId = 2, ProductId = 1, ComponentName = "Tay", ComponentType = ComponentTypes.Panel },
        ]);

        // Act
        var result = await _sut.CreateAsync(Dto("OP11", 11, group: "Lắp ráp", componentIds: [1, 2]), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _operations.Single(o => o.OperationCode == "OP11");
        created.OperationGroupId.Should().Be(5);
        created.OperationComponents.Should().HaveCount(2);
        created.OperationComponents.Select(l => l.ProductComponentId).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task UTCID12_CreateAsync_TrimsOperationCode()
    {
        // Arrange
        Operation? created = null;
        Repo<Operation>().When(r => r.AddAsync(Arg.Any<Operation>())).Do(c => created = c.Arg<Operation>());

        // Act
        var result = await _sut.CreateAsync(Dto("  OP11  ", 11), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        created!.OperationCode.Should().Be("OP11");
    }

    [Fact]
    public async Task UTCID13_CreateAsync_OperationCodeMaxLength50_Succeeds()
    {
        // Arrange
        var code = new string('A', 50);

        // Act
        var result = await _sut.CreateAsync(Dto(code, 13), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID14_CreateAsync_OperationCode51Chars_ReturnsValidationNoSave()
    {
        // Arrange
        var code = new string('B', 51);
        Repo<Operation>().ClearReceivedCalls();
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.CreateAsync(Dto(code, 14), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID15_CreateAsync_EmptyOperationCode_ReturnsValidation()
    {
        // Arrange

        // Act
        var result = await _sut.CreateAsync(Dto("", 15), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID16_CreateAsync_StandardTimeZero_ReturnsValidation()
    {
        // Arrange

        // Act
        var result = await _sut.CreateAsync(Dto("OP16", 16, std: 0m), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID17_CreateAsync_StandardTimeNegative_ReturnsValidation()
    {
        // Arrange

        // Act
        var result = await _sut.CreateAsync(Dto("OP17", 17, std: -1m), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID18_CreateAsync_EmptyComponents_SucceedsWithoutLinks()
    {
        // Arrange
        Operation? created = null;
        Repo<Operation>().When(r => r.AddAsync(Arg.Any<Operation>())).Do(c => created = c.Arg<Operation>());

        // Act
        var result = await _sut.CreateAsync(Dto("OP18", 18, componentIds: []), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        created!.OperationComponents.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID19_CreateAsync_NullDto_ThrowsOrValidationNoTransaction()
    {
        // Arrange
        Uow.ClearReceivedCalls();
        Exception? caught = null;
        Result<int>? result = null;

        // Act
        try { result = await _sut.CreateAsync(null!, default); }
        catch (Exception ex) { caught = ex; }

        // Assert
        if (caught is ArgumentNullException or NullReferenceException)
        {
            await Uow.DidNotReceive().BeginTransactionAsync();
            return;
        }

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        await Uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID20_CreateAsync_NullOperationCode_ReturnsValidation()
    {
        // Arrange
        var dto = Dto("OP20", 20) with { OperationCode = null! };

        // Act
        var result = await _sut.CreateAsync(dto, default);

        // Assert — expected Validation Failure (⚠ current code may throw NRE → test FAIL)
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID21_CreateAsync_UnknownProduct_ReturnsNotFoundNoAdd()
    {
        // Arrange
        Repo<Operation>().ClearReceivedCalls();

        // Act
        var result = await _sut.CreateAsync(Dto("OP21", 21, productId: 999), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        await Repo<Operation>().DidNotReceive().AddAsync(Arg.Any<Operation>());
    }

    [Fact]
    public async Task UTCID22_CreateAsync_DuplicateCode_ReturnsDuplicateNoSave()
    {
        // Arrange
        AddOp(1, "OP10", 10);
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.CreateAsync(Dto("OP10", 11), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationCodeDuplicate);
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID23_CreateAsync_DuplicateStationSequence_ReturnsDuplicateSequence()
    {
        // Arrange
        AddOp(1, "OP10", 10);

        // Act
        var result = await _sut.CreateAsync(Dto("OP23", 10), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ExpectedDuplicateSequence);
    }

    [Fact]
    public async Task UTCID24_CreateAsync_SecondSaveThrowsDbUpdate_RollsBackNoCommitKeepsOldFinal()
    {
        // Arrange
        var oldFinal = AddOp(1, "OP20", 20, final: true);
        var call = 0;
        Uow.When(u => u.SaveChangesAsync()).Do(_ =>
        {
            call++;
            if (call == 2) throw new DbUpdateException("fail");
        });

        // Act
        Func<Task> act = () => _sut.CreateAsync(Dto("OP24", 24, final: true), default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await Uow.Received(1).RollbackTransactionAsync();
        await Uow.DidNotReceive().CommitTransactionAsync();
        oldFinal.IsFinalStep.Should().BeTrue();
    }

    // ==================== UpdateAsync ====================

    [Fact]
    public async Task UTCID25_UpdateAsync_Valid_UpdatesFieldsAndAudits()
    {
        // Arrange
        var op = AddOp(5, "OP10", 10);

        // Act
        var result = await _sut.UpdateAsync(5, Dto("OP10", 10, name: "May cổ mới", std: 50m), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        op.OperationName.Should().Be("May cổ mới");
        op.StandardTimeSec.Should().Be(50m);
        Audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.TableName == nameof(Operation)));
    }

    [Fact]
    public async Task UTCID26_UpdateAsync_SetFinalStep_MovesFinalFlag()
    {
        // Arrange
        var op = AddOp(5, "OP10", 10);
        var other = AddOp(6, "OP20", 20, final: true);

        // Act
        var result = await _sut.UpdateAsync(5, Dto("OP10", 10, final: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        op.IsFinalStep.Should().BeTrue();
        other.IsFinalStep.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID27_UpdateAsync_SameCodeOnSelf_Succeeds()
    {
        // Arrange
        AddOp(5, "OP10", 10);

        // Act
        var result = await _sut.UpdateAsync(5, Dto("OP10", 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID28_UpdateAsync_DtoProductIdIgnored_KeepsOriginalProduct()
    {
        // Arrange
        var op = AddOp(5, "OP10", 10);

        // Act
        var result = await _sut.UpdateAsync(5, Dto("OP10", 10, productId: 2), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        op.ProductId.Should().Be(1);
    }

    [Fact]
    public async Task UTCID29_UpdateAsync_DiscontinuedSetFinal_DoesNotMoveFinal()
    {
        // Arrange
        var discontinued = AddOp(5, "OP10", 10, status: OperationStatuses.Discontinued);
        var finalActive = AddOp(6, "OP20", 20, final: true);

        // Act
        var result = await _sut.UpdateAsync(5, Dto("OP10", 10, final: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        discontinued.IsFinalStep.Should().BeFalse();
        finalActive.IsFinalStep.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID30_UpdateAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await _sut.UpdateAsync(999, Dto("OP10", 10), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID31_UpdateAsync_IdZero_ReturnsNotFoundOrValidation()
    {
        // Arrange

        // Act
        var result = await _sut.UpdateAsync(0, Dto("OP10", 10), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().BeOneOf(ResultKind.NotFound, ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID32_UpdateAsync_DuplicateCodeOtherOp_ReturnsDuplicate()
    {
        // Arrange
        AddOp(5, "OP10", 10);
        AddOp(6, "OP20", 20);

        // Act
        var result = await _sut.UpdateAsync(5, Dto("OP20", 10), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationCodeDuplicate);
    }

    [Fact]
    public async Task UTCID33_UpdateAsync_NullOperationCode_ReturnsValidation()
    {
        // Arrange
        AddOp(5, "OP10", 10);
        var dto = Dto("OP10", 10) with { OperationCode = null! };

        // Act
        var result = await _sut.UpdateAsync(5, dto, default);

        // Assert — expected Validation Failure (⚠ current code may throw NRE → test FAIL)
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID34_UpdateAsync_ConcurrencyException_RollsBack()
    {
        // Arrange
        AddOp(5, "OP10", 10);
        Uow.When(u => u.SaveChangesAsync()).Do(_ => throw new DbUpdateConcurrencyException("conflict"));

        // Act
        Func<Task> act = () => _sut.UpdateAsync(5, Dto("OP10", 10, name: "Mới"), default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await Uow.Received().RollbackTransactionAsync();
    }

    // ==================== DiscontinueAsync ====================

    [Fact]
    public async Task UTCID35_DiscontinueAsync_ActiveNoOpenWork_SetsDiscontinuedAndAudits()
    {
        // Arrange
        var op = AddOp(5, "OP10", 10);

        // Act
        var result = await _sut.DiscontinueAsync(5, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        op.Status.Should().Be(OperationStatuses.Discontinued);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Operation)));
    }

    [Fact]
    public async Task UTCID36_DiscontinueAsync_AlreadyDiscontinued_IdempotentNoSave()
    {
        // Arrange
        AddOp(5, "OP10", 10, status: OperationStatuses.Discontinued);
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.DiscontinueAsync(5, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID37_DiscontinueAsync_PendingWorkAllocation_ReturnsHasOpenWork()
    {
        // Arrange
        AddOp(5, "OP10", 10);
        _orderLines.Add(OpenLine(50));
        _allocations.Add(new WorkAllocation { OperationId = 5, OrderLineId = 50, Status = HandoverStatuses.Pending });

        // Act
        var result = await _sut.DiscontinueAsync(5, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationHasOpenWork);
    }

    [Fact]
    public async Task UTCID38_DiscontinueAsync_RemainingQtyOnOpenOrder_ReturnsHasOpenWork()
    {
        // Arrange
        AddOp(5, "OP10", 10);
        _orderLines.Add(OpenLine(50));
        _caps.Add(new VwWorkerOutputCap { OperationId = 5, OrderLineId = 50, RemainingQty = 1 });

        // Act
        var result = await _sut.DiscontinueAsync(5, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationHasOpenWork);
    }

    [Fact]
    public async Task UTCID39_DiscontinueAsync_RemainingOnClosedOrder_Succeeds()
    {
        // Arrange
        var op = AddOp(5, "OP10", 10);
        _orderLines.Add(OpenLine(60, OrderStatuses.Completed));
        _caps.Add(new VwWorkerOutputCap { OperationId = 5, OrderLineId = 60, RemainingQty = 5 });

        // Act
        var result = await _sut.DiscontinueAsync(5, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        op.Status.Should().Be(OperationStatuses.Discontinued);
    }

    [Fact]
    public async Task UTCID40_DiscontinueAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await _sut.DiscontinueAsync(999, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID41_DiscontinueAsync_OnlyActiveFinalStep_ReturnsFinalStepRequired()
    {
        // Arrange
        AddOp(6, "OP20", 20, final: true);

        // Act
        var result = await _sut.DiscontinueAsync(6, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ExpectedFinalStepRequired);
    }

    // ==================== ReactivateAsync ====================

    [Fact]
    public async Task UTCID42_ReactivateAsync_DiscontinuedNotFinal_BecomesActive()
    {
        // Arrange
        var op = AddOp(3, "OP03", 3, status: OperationStatuses.Discontinued);

        // Act
        var result = await _sut.ReactivateAsync(3, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        op.Status.Should().Be(OperationStatuses.Active);
    }

    [Fact]
    public async Task UTCID43_ReactivateAsync_DiscontinuedFinal_NoOtherFinal_Succeeds()
    {
        // Arrange
        var op = AddOp(3, "OP03", 3, final: true, status: OperationStatuses.Discontinued);

        // Act
        var result = await _sut.ReactivateAsync(3, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        op.Status.Should().Be(OperationStatuses.Active);
        op.IsFinalStep.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID44_ReactivateAsync_AlreadyActive_IdempotentNoSave()
    {
        // Arrange
        AddOp(10, "OP10", 10);
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.ReactivateAsync(10, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID45_ReactivateAsync_DiscontinuedFinal_ConflictWithActiveFinal_ReturnsError()
    {
        // Arrange
        AddOp(20, "OP20", 20, final: true);
        AddOp(3, "OP03", 3, final: true, status: OperationStatuses.Discontinued);

        // Act
        var result = await _sut.ReactivateAsync(3, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationFinalStepExists);
    }

    [Fact]
    public async Task UTCID46_ReactivateAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange

        // Act
        var result = await _sut.ReactivateAsync(999, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    // ==================== PreviewImportAsync ====================

    [Fact]
    public async Task UTCID47_PreviewImportAsync_ValidThreeRows_SucceedsNoSave()
    {
        // Arrange
        MockExcelSheets(ThreeRowImportSheet());
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Should().HaveCount(3);
        result.Data.Rows.Should().OnlyContain(r => r.Action == "New" || r.Action == "Update");
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID48_PreviewImportAsync_NullSheetName_UsesFirstSheet()
    {
        // Arrange
        MockExcelSheets(
            Sheet("SheetA", [ExcelRow(1)]),
            Sheet("SheetB", [ExcelRow(2)]));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), null, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.SheetName.Should().Be("SheetA");
    }

    [Fact]
    public async Task UTCID49_PreviewImportAsync_HeaderOnlyZeroRows_SucceedsEmpty()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan", []));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID50_PreviewImportAsync_DuplicateStt_MarksBothError()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan", [ExcelRow(10), ExcelRow(10, name: "Dup")]));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Should().HaveCount(2);
        result.Data.Rows.Should().OnlyContain(r => r.Action == "Error");
        result.Data.Rows.Should().OnlyContain(r => r.Error!.Contains("Trùng STT"));
    }

    [Fact]
    public async Task UTCID51_PreviewImportAsync_MultipleFinals_MarksFinalRowsError()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan",
        [
            ExcelRow(10, final: true),
            ExcelRow(20, final: true),
        ]));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Where(r => r.Action == "Error").Should().HaveCountGreaterThan(0);
        result.Data.Rows.Where(r => r.Action == "Error").Should().OnlyContain(r => r.Error == Messages.OperationImportManyFinals);
    }

    [Fact]
    public async Task UTCID52_PreviewImportAsync_UnknownComponent_ValidRowWithWarning()
    {
        // Arrange
        _components.Add(new ProductComponent { ProductComponentId = 1, ProductId = 1, ComponentName = "Tay", ComponentType = ComponentTypes.Panel });
        MockExcelSheets(Sheet("CongDoan", [ExcelRow(10, componentNames: ["Cầu vai"])], hasComponent: true));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var row = result.Data!.Rows.Single();
        row.Action.Should().NotBe("Error");
        row.Warning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UTCID53_PreviewImportAsync_Stt32767_ValidRow()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan", [ExcelRow(32767)]));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Single().Stt.Should().Be(32767);
        result.Data.Rows.Single().Action.Should().NotBe("Error");
    }

    [Fact]
    public async Task UTCID54_PreviewImportAsync_Stt32768_MarksError()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan", [ExcelRow(32768)]));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Single().Action.Should().Be("Error");
    }

    [Fact]
    public async Task UTCID55_PreviewImportAsync_ParserThrows_ReturnsExcelInvalid()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Throws(new InvalidDataException("bad xlsx"));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationExcelInvalid);
    }

    [Fact]
    public async Task UTCID56_PreviewImportAsync_SheetNotFound_ReturnsSheetNotFound()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan", [ExcelRow(10)]));

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), "XYZ", null, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationImportSheetNotFound);
    }

    [Fact]
    public async Task UTCID57_PreviewImportAsync_EmptySheetList_ReturnsExcelInvalid()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Returns(Array.Empty<OperationExcelSheetDto>());

        // Act
        var result = await _sut.PreviewImportAsync(1, StreamStub(), null, null, default);

        // Assert — expected ExcelInvalid Failure (⚠ current code may throw ArgumentOutOfRangeException → test FAIL)
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationExcelInvalid);
    }

    [Fact]
    public async Task UTCID58_PreviewImportAsync_UnknownProduct_ReturnsNotFound()
    {
        // Arrange
        MockExcelSheets(ThreeRowImportSheet());

        // Act
        var result = await _sut.PreviewImportAsync(999, StreamStub(), "CongDoan", null, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task UTCID59_PreviewImportAsync_NullStream_ThrowsOrExcelInvalid()
    {
        // Arrange — parser phải phản ứng với stream null (không để mặc định trả null → NRE giả)
        _excel.Parse(null!).Throws(new ArgumentNullException("stream"));

        // Act
        Func<Task<Result<OperationImportPreviewDto>>> act = () =>
            _sut.PreviewImportAsync(1, null!, "CongDoan", null, default);

        // Assert — ArgumentNullException hoặc Failure (ExcelInvalid) đều hợp lệ theo bảng
        try
        {
            var result = await act();
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be(ErrorCodes.OperationExcelInvalid);
        }
        catch (ArgumentNullException)
        {
            // Acceptable per spec.
        }
    }

    // ==================== ImportAsync ====================

    [Fact]
    public async Task UTCID60_ImportAsync_ThreeValidRows_CreatesThreeNoRatesCommit()
    {
        // Arrange
        MockExcelSheets(ThreeRowImportSheet());
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, null, submitRates: false, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Created.Should().Be(3);
        _rates.Should().BeEmpty();
        await Uow.Received(1).CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID61_ImportAsync_SubmitRates_CreatesPendingRatesAndNotifiesAdminOnce()
    {
        // Arrange — mỗi dòng phải có Rate (ThreeRowImportSheet chỉ set rate ở dòng 30)
        MockExcelSheets(Sheet("CongDoan",
        [
            ExcelRow(10, "May 1", rate: 100m),
            ExcelRow(20, "May 2", rate: 100m),
            ExcelRow(30, "May 3", final: true, rate: 100m),
        ]));
        var effective = new DateOnly(2026, 12, 1);

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, effective, submitRates: true, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _rates.Should().HaveCount(3);
        _rates.Should().OnlyContain(r => r.Status == PieceRateStatuses.PendingApproval && r.EffectiveDate == effective);
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID62_ImportAsync_Replace_RemovesUnusedOp99()
    {
        // Arrange
        AddOp(99, "OP99", 99);
        MockExcelSheets(ThreeRowImportSheet());

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, null, false, default, replace: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Removed.Should().Be(1);
        _operations.Should().NotContain(o => o.OperationCode == "OP99");
    }

    [Fact]
    public async Task UTCID63_ImportAsync_Replace_Op99Used_NotRemovedReportsBlocked()
    {
        // Arrange
        AddOp(99, "OP99", 99);
        _outputs.Add(new OutputRecord { OperationId = 99 });
        MockExcelSheets(ThreeRowImportSheet());

        // Act
        var preview = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default, replace: true);

        // Assert
        preview.IsSuccess.Should().BeTrue();
        preview.Data!.Blocked.Should().ContainSingle(b => b.StartsWith("OP99"));
        _operations.Should().ContainSingle(o => o.OperationCode == "OP99");
    }

    [Fact]
    public async Task UTCID64_ImportAsync_AllRowsErrorReplace_FailureNoValidRows()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan",
        [
            ExcelRow(10, parseError: "Lỗi 1"),
            ExcelRow(20, parseError: "Lỗi 2"),
            ExcelRow(30, parseError: "Lỗi 3"),
        ]));

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, null, false, default, replace: true);

        // Assert — Failure "không còn dòng hợp lệ" map sang ErrorCodes.OperationExcelEmpty của dự án
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationExcelEmpty);
    }

    [Fact]
    public async Task UTCID65_ImportAsync_SubmitRatesEffectiveToday_UsesToday()
    {
        // Arrange
        MockExcelSheets(ThreeRowImportSheet());

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, SpecToday, submitRates: true, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _rates.Should().OnlyContain(r => r.EffectiveDate == SpecToday);
    }

    [Fact]
    public async Task UTCID66_ImportAsync_SubmitRatesNullEffective_UsesDefaultToday()
    {
        // Arrange
        MockExcelSheets(ThreeRowImportSheet());

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, null, submitRates: true, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _rates.Should().OnlyContain(r => r.EffectiveDate == SpecToday);
    }

    [Fact]
    public async Task UTCID67_ImportAsync_UnknownComponent_KeepsOldComponentWithWarning()
    {
        // Arrange
        var op = AddOp(10, "OP10", 10);
        _components.Add(new ProductComponent { ProductComponentId = 2, ProductId = 1, ComponentName = "Tay", ComponentType = ComponentTypes.Panel });
        _opComponents.Add(new OperationComponent { OperationId = 10, ProductId = 1, ProductComponentId = 2 });
        op.OperationComponents = _opComponents.Where(c => c.OperationId == 10).ToList();
        MockExcelSheets(Sheet("CongDoan", [ExcelRow(10, componentNames: ["Cầu vai"])], hasComponent: true));

        // Act
        var preview = await _sut.PreviewImportAsync(1, StreamStub(), "CongDoan", null, default);

        // Assert
        preview.IsSuccess.Should().BeTrue();
        preview.Data!.Rows.Single().Warning.Should().NotBeNullOrEmpty();
        preview.Data.WarningCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UTCID68_ImportAsync_TwoFinalRows_ReturnsFinalStepFailureNoSave()
    {
        // Arrange
        MockExcelSheets(Sheet("CongDoan", [ExcelRow(10, final: true), ExcelRow(20, final: true)]));
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, null, false, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationFinalStepExists);
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID69_ImportAsync_ParserThrows_ReturnsExcelInvalidNoTransaction()
    {
        // Arrange
        _excel.Parse(Arg.Any<Stream>()).Throws(new InvalidDataException("bad"));
        Uow.ClearReceivedCalls();

        // Act
        var result = await _sut.ImportAsync(1, StreamStub(), "CongDoan", null, null, false, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationExcelInvalid);
        await Uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID70_ImportAsync_SaveChangesThrows_RollsBackNotifierNotCalled()
    {
        // Arrange
        MockExcelSheets(ThreeRowImportSheet());
        Uow.When(u => u.SaveChangesAsync()).Do(_ => throw new DbUpdateException("db"));
        Notifier.ClearReceivedCalls();

        // Act
        Func<Task> act = () => _sut.ImportAsync(1, StreamStub(), "CongDoan", null, SpecToday, submitRates: true, default);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await Uow.Received().RollbackTransactionAsync();
        await Notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
    }
}