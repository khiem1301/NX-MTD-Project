using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class MonthlyCostServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0);
    private const string Month = "2026-10";
    private static readonly DateOnly MonthStart = new(2026, 10, 1);
    private static readonly DateOnly MonthEnd = new(2026, 10, 31);

    private readonly IMonthlyCostRepository _repo = Substitute.For<IMonthlyCostRepository>();
    private readonly IExcelMonthlyCostParser _excel = Substitute.For<IExcelMonthlyCostParser>();
    private readonly IMonthlyCostExcelWriter _writer = Substitute.For<IMonthlyCostExcelWriter>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly MonthlyCostService _sut;

    public MonthlyCostServiceTests()
    {
        _clock.Now.Returns(Now);
        _sut = new MonthlyCostService(_repo, _excel, _writer, _audit, _clock, NullLogger<MonthlyCostService>.Instance);
    }

    private static AccountingPeriod Period(int id = 1, string month = Month, string status = RecordStatus.Open) =>
        new() { PeriodId = id, PeriodMonth = month, Status = status, StartDate = MonthStart, EndDate = MonthEnd };

    private static MonthlyCostExcelRowDto Row(int rowNumber, string? category, string? costType,
        string? productCode, decimal? amount, int? quantity = null, string? note = null, string? parseError = null) =>
        new(rowNumber, category, costType, productCode, amount, quantity, note, parseError);

    private static IReadOnlyDictionary<string, (int ProductId, string ProductName)> Products() =>
        new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["SP001"] = (1, "Áo thun nam"),
        };

    // ---------- ExportTemplateAsync (UC12) ----------

    [Fact]
    public async Task ExportTemplateAsync_BuildsFileFromWriter()
    {
        _writer.WriteTemplate().Returns(new byte[] { 1, 2, 3 });

        var result = await _sut.ExportTemplateAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be("MauChiPhiThang.xlsx");
        result.Data.Content.Should().Equal(1, 2, 3);
    }

    // ---------- GetByPeriodMonthAsync (UC12/UC33) ----------

    [Fact]
    public async Task GetByPeriodMonthAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.GetByPeriodMonthAsync("2026-13", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostPeriodMonthInvalid);
    }

    [Fact]
    public async Task GetByPeriodMonthAsync_PeriodNotFound_ReturnsNotFound()
    {
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        var result = await _sut.GetByPeriodMonthAsync(Month, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostNotFound);
    }

    [Fact]
    public async Task GetByPeriodMonthAsync_NoItems_ReturnsNotFound()
    {
        var period = Period();
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetItemsByPeriodIdAsync(period.PeriodId, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyCost>());

        var result = await _sut.GetByPeriodMonthAsync(Month, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostNotFound);
    }

    [Fact]
    public async Task GetByPeriodMonthAsync_Valid_ReturnsItemsWithMealsReconciliation()
    {
        var period = Period();
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        var items = new List<MonthlyCost>
        {
            new()
            {
                MonthlyCostId = 1, PeriodId = period.PeriodId, CostCategoryCode = MonthlyCostCategory.Meals,
                CostType = MonthlyCostType.Fixed, Amount = 20_000_000m, ImportedAt = Now,
            },
            new()
            {
                MonthlyCostId = 2, PeriodId = period.PeriodId, CostCategoryCode = MonthlyCostCategory.Fuel,
                CostType = MonthlyCostType.Fixed, Amount = 5_000_000m, ImportedAt = Now,
            },
        };
        _repo.GetItemsByPeriodIdAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(items);
        _repo.GetMealsTotalAmountAsync(period.StartDate, period.EndDate, Arg.Any<CancellationToken>())
            .Returns(21_000_000m);

        var result = await _sut.GetByPeriodMonthAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalAmount.Should().Be(25_000_000m);
        result.Data.Items.Should().HaveCount(2);
        result.Data.MealsReconciliation.ExpectedAmount.Should().Be(21_000_000m);
        result.Data.MealsReconciliation.ImportedAmount.Should().Be(20_000_000m);
        result.Data.MealsReconciliation.HasDiscrepancy.Should().BeTrue();
    }

    // ---------- PreviewImportAsync (UC12) ----------

    [Fact]
    public async Task PreviewImportAsync_InvalidFile_ReturnsValidationFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(_ => throw new InvalidDataException("sai cột"));

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostExcelInvalid);
    }

    [Fact]
    public async Task PreviewImportAsync_EmptyFile_ReturnsFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>());

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostExcelEmpty);
    }

    [Fact]
    public async Task PreviewImportAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.PreviewImportAsync(new MemoryStream(), "2026-13", CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostPeriodMonthInvalid);
    }

    [Fact]
    public async Task PreviewImportAsync_UnknownCategory_MarksRowError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, "RENT", MonthlyCostType.Fixed, null, 1_000_000m),
        });

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ErrorCount.Should().Be(1);
        result.Data.Rows.Single().RowStatus.Should().Be(MonthlyCostRowStatus.Error);
    }

    [Fact]
    public async Task PreviewImportAsync_VariableRowMissingProductCode_MarksRowError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.MaterialsBought, MonthlyCostType.Variable, null, 1_000_000m),
        });

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.Data!.ErrorCount.Should().Be(1);
        result.Data.Rows.Single().ErrorMessage.Should().Contain("mã hàng");
    }

    [Fact]
    public async Task PreviewImportAsync_FixedRowWithProductCode_MarksRowError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Fuel, MonthlyCostType.Fixed, "SP001", 1_000_000m),
        });
        _repo.GetProductsByCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Products());

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.Data!.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task PreviewImportAsync_UnknownProductCode_MarksRowError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.MaterialsBought, MonthlyCostType.Variable, "SPX", 1_000_000m),
        });
        _repo.GetProductsByCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Products());

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.Data!.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task PreviewImportAsync_NegativeAmount_MarksRowError()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Fuel, MonthlyCostType.Fixed, null, -1000m),
        });

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.Data!.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task PreviewImportAsync_DuplicateCategoryAndProduct_MarksDuplicate()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.MaterialsBought, MonthlyCostType.Variable, "SP001", 1_000_000m),
            Row(3, MonthlyCostCategory.MaterialsBought, MonthlyCostType.Variable, "SP001", 2_000_000m),
        });
        _repo.GetProductsByCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Products());

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.Data!.ValidCount.Should().Be(1);
        result.Data.DuplicateCount.Should().Be(1);
    }

    [Fact]
    public async Task PreviewImportAsync_MealsRow_ComputesReconciliationFromDailyMeal()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Meals, MonthlyCostType.Fixed, null, 18_000_000m),
        });
        _repo.GetMealsTotalAmountAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>())
            .Returns(18_500_000m);

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.MealsReconciliation.ExpectedAmount.Should().Be(18_500_000m);
        result.Data.MealsReconciliation.ImportedAmount.Should().Be(18_000_000m);
        result.Data.MealsReconciliation.HasDiscrepancy.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewImportAsync_NoMealsRow_ReconciliationHasNullImportedAmount()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Fuel, MonthlyCostType.Fixed, null, 1_000_000m),
        });
        _repo.GetMealsTotalAmountAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>()).Returns(5_000_000m);

        var result = await _sut.PreviewImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.Data!.MealsReconciliation.ImportedAmount.Should().BeNull();
        result.Data.MealsReconciliation.HasDiscrepancy.Should().BeFalse();
    }

    // ---------- ImportAsync (UC12) ----------

    [Fact]
    public async Task ImportAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.ImportAsync(new MemoryStream(), "2026-13", CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostPeriodMonthInvalid);
    }

    [Fact]
    public async Task ImportAsync_NoValidRows_ReturnsFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, "INVALID_CODE", MonthlyCostType.Fixed, null, 1_000_000m),
        });

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostExcelEmpty);
    }

    [Fact]
    public async Task ImportAsync_PeriodLocked_ReturnsFailure()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Fuel, MonthlyCostType.Fixed, null, 1_000_000m),
        });
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Period(status: RecordStatus.Locked));

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostPeriodLocked);
        result.Message.Should().Be(string.Format(Messages.AttendancePeriodLockedMessage, Month));
    }

    [Fact]
    public async Task ImportAsync_ValidRows_ReplacesExistingItemsAndSaves()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Fuel, MonthlyCostType.Fixed, null, 3_000_000m),
            Row(3, MonthlyCostCategory.MaterialsBought, MonthlyCostType.Variable, "SP001", 2_000_000m, 10),
        });
        _repo.GetProductsByCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Products());
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);

        List<MonthlyCost>? addedItems = null;
        _repo.When(r => r.AddItems(Arg.Any<IEnumerable<MonthlyCost>>()))
            .Do(c => addedItems = c.Arg<IEnumerable<MonthlyCost>>().ToList());

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ValidCount.Should().Be(2);
        result.Data.ItemCount.Should().Be(2);
        result.Data.TotalAmount.Should().Be(5_000_000m);
        result.Message.Should().Contain("2 dòng hợp lệ");

        await _repo.Received(1).RemoveItemsByPeriodIdAsync(period.PeriodId, Arg.Any<CancellationToken>());
        addedItems.Should().NotBeNull();
        addedItems!.Should().HaveCount(2);
        addedItems.Should().Contain(i =>
            i.CostCategoryCode == MonthlyCostCategory.MaterialsBought && i.ProductId == 1 && i.Quantity == 10);
        addedItems.Should().OnlyContain(i => i.PeriodId == period.PeriodId && i.ImportedAt == Now);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Tháng chưa có dữ liệu → Insert, không có giá trị cũ (GBR-15, DEV-MI-12-4).
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(MonthlyCost)
            && e.RecordId == period.PeriodId && e.Action == AuditActions.Insert && e.OldValues == null));
    }

    [Fact]
    public async Task ImportAsync_ReplacingExistingMonth_LogsUpdateWithOldTotals()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Fuel, MonthlyCostType.Fixed, null, 3_000_000m),
        });
        _repo.GetProductsByCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Products());
        var period = Period();
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetItemsByPeriodIdAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(new List<MonthlyCost>
        {
            new() { CostCategoryCode = MonthlyCostCategory.Fuel, Amount = 1_000_000m },
            new() { CostCategoryCode = MonthlyCostCategory.MaterialsBought, Amount = 500_000m },
        });

        var result = await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(MonthlyCost)
            && e.Action == AuditActions.Update && e.OldValues != null));
    }

    [Fact]
    public async Task ImportAsync_PeriodLocked_LogsNothing()
    {
        _excel.Parse(Arg.Any<Stream>()).Returns(new List<MonthlyCostExcelRowDto>
        {
            Row(2, MonthlyCostCategory.Fuel, MonthlyCostType.Fixed, null, 3_000_000m),
        });
        _repo.GetProductsByCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Products());
        _repo.GetOrCreatePeriodAsync(Month, Arg.Any<CancellationToken>()).Returns(Period(status: RecordStatus.Locked));

        await _sut.ImportAsync(new MemoryStream(), Month, CancellationToken.None);

        _audit.DidNotReceiveWithAnyArgs().Add(default!);
    }
}
