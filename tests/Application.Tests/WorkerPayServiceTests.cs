using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>MI-10 (UC59): sản lượng + lương tạm tính của chính công nhân may khoán sản phẩm.</summary>
public class WorkerPayServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);       // thứ Tư
    private static readonly DateOnly Sunday = new(2026, 10, 18);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<VwWorkerDailyEstimatedPay> _pay = [];
    private readonly List<OutputRecord> _outputs = [];
    private readonly List<DefectRecord> _defects = [];
    private readonly WorkerPayService _sut;

    public WorkerPayServiceTests()
    {
        _clock.Today.Returns(Today);
        SetupQueryable(_pay);
        SetupQueryable(_outputs);
        SetupQueryable(_defects);
        _sut = new WorkerPayService(_uow, new OutputQueryService(_uow), _clock);
    }

    private void SetupQueryable<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private static VwWorkerDailyEstimatedPay Day(int worker, DateOnly date, int qty, decimal amount, int scrapQty = 0,
        decimal scrapAmount = 0m, bool sunday = false, decimal sundayBonus = 0m) => new()
    {
        WorkerId = worker, EmployeeCode = $"E{worker}", FullName = $"CN {worker}", WorkDate = date, IsSunday = sunday,
        OutputQty = qty, ScrapQty = scrapQty, OutputAmount = amount, SundayBonus = sundayBonus, ScrapAmount = scrapAmount,
        EstimatedPay = amount + sundayBonus - scrapAmount,
    };

    private static Operation Op(string code, string name, short sequence) =>
        new() { OperationCode = code, OperationName = name, StationSequence = sequence };

    private static OrderLine Line() => new()
    {
        Order = new CustomerOrder { OrderCode = "DH001" }, ProductVariant = TestVariant.Make(1),
    };

    // ---------- GetMyEstimatedPayAsync ----------

    [Fact]
    public async Task GetMyEstimatedPay_SumsOwnDaysAndMarksResultAsEstimate()
    {
        _pay.Add(Day(1, new DateOnly(2026, 10, 19), qty: 100, amount: 500_000m));
        _pay.Add(Day(1, Sunday, qty: 80, amount: 400_000m, scrapQty: 2, scrapAmount: 20_000m, sunday: true,
            sundayBonus: 400_000m));

        var result = await _sut.GetMyEstimatedPayAsync(1, new MyEstimatedPayQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.IsEstimate.Should().BeTrue();   // GBR-12: không phải lương chính thức
        dto.Days.Select(d => d.WorkDate).Should().Equal(Sunday, new DateOnly(2026, 10, 19));   // tăng dần theo ngày
        dto.TotalOutputQty.Should().Be(180);
        dto.TotalScrapQty.Should().Be(2);
        dto.TotalOutputAmount.Should().Be(900_000m);
        dto.TotalSundayBonus.Should().Be(400_000m);
        dto.TotalScrapAmount.Should().Be(20_000m);
        dto.TotalEstimatedPay.Should().Be(500_000m + 780_000m);   // (400k+400k−20k) + 500k
        dto.Days.Single(d => d.IsSunday).EstimatedPay.Should().Be(780_000m);
    }

    [Fact]
    public async Task GetMyEstimatedPay_OtherWorkersDays_AreNeverIncluded()
    {
        _pay.Add(Day(1, Today, qty: 10, amount: 50_000m));
        _pay.Add(Day(2, Today, qty: 999, amount: 9_999_000m));

        var result = await _sut.GetMyEstimatedPayAsync(1, new MyEstimatedPayQuery(), CancellationToken.None);

        result.Data!.Days.Should().ContainSingle();
        result.Data.TotalEstimatedPay.Should().Be(50_000m);
    }

    [Fact]
    public async Task GetMyEstimatedPay_DefaultRange_IsCurrentMonthUpToToday()
    {
        _pay.Add(Day(1, new DateOnly(2026, 9, 30), qty: 1, amount: 1_000m));   // tháng trước: bị loại
        _pay.Add(Day(1, new DateOnly(2026, 10, 1), qty: 2, amount: 2_000m));
        _pay.Add(Day(1, new DateOnly(2026, 10, 22), qty: 3, amount: 3_000m));  // sau hôm nay: bị loại

        var result = await _sut.GetMyEstimatedPayAsync(1, new MyEstimatedPayQuery(), CancellationToken.None);

        result.Data!.From.Should().Be(new DateOnly(2026, 10, 1));
        result.Data.To.Should().Be(Today);
        result.Data.Days.Should().ContainSingle().Which.OutputQty.Should().Be(2);
    }

    [Fact]
    public async Task GetMyEstimatedPay_ExplicitRange_IsInclusive()
    {
        _pay.Add(Day(1, new DateOnly(2026, 9, 1), qty: 1, amount: 100m));
        _pay.Add(Day(1, new DateOnly(2026, 9, 30), qty: 1, amount: 100m));
        _pay.Add(Day(1, new DateOnly(2026, 10, 1), qty: 1, amount: 100m));

        var result = await _sut.GetMyEstimatedPayAsync(1,
            new MyEstimatedPayQuery { From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 30) },
            CancellationToken.None);

        result.Data!.Days.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMyEstimatedPay_NotPieceRateWorker_ReturnsEmptyWithNoDataMessage()
    {
        // vw_WorkerDailyEstimatedPay chỉ có dòng cho PIECE_RATE nên người khác không có dòng nào.
        var result = await _sut.GetMyEstimatedPayAsync(7, new MyEstimatedPayQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Days.Should().BeEmpty();
        result.Data.TotalEstimatedPay.Should().Be(0m);
        result.Message.Should().Be(Messages.NoData);
    }

    // ---------- GetMyDayDetailAsync ----------

    [Fact]
    public async Task GetMyDayDetail_ReturnsOutputLinesWithLockedRateAndAmount()
    {
        _outputs.Add(new OutputRecord
        {
            WorkerId = 1, WorkDate = Today, ShiftCode = "MORNING", Quantity = 50, PieceRate = new PieceRate { Rate = 1_200m },
            Operation = Op("OP20", "Vắt sổ", 2), OrderLine = Line(),
        });
        _outputs.Add(new OutputRecord
        {
            WorkerId = 1, WorkDate = Today, ShiftCode = "MORNING", Quantity = 40, PieceRate = new PieceRate { Rate = 800m },
            Operation = Op("OP10", "May cổ", 1), OrderLine = Line(),
        });
        _outputs.Add(new OutputRecord   // của người khác và của ngày khác: không được lẫn vào
        {
            WorkerId = 2, WorkDate = Today, ShiftCode = "MORNING", Quantity = 1, PieceRate = new PieceRate { Rate = 1m },
            Operation = Op("OP10", "May cổ", 1), OrderLine = Line(),
        });
        _outputs.Add(new OutputRecord
        {
            WorkerId = 1, WorkDate = Today.AddDays(-1), ShiftCode = "MORNING", Quantity = 1, PieceRate = new PieceRate { Rate = 1m },
            Operation = Op("OP10", "May cổ", 1), OrderLine = Line(),
        });

        var result = await _sut.GetMyDayDetailAsync(1, Today, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Outputs.Select(o => o.OperationCode).Should().Equal("OP10", "OP20");   // theo thứ tự công đoạn
        result.Data.Outputs.Single(o => o.OperationCode == "OP20").Amount.Should().Be(60_000m);   // 50 × 1.200
        result.Data.Outputs.Sum(o => o.Amount).Should().Be(60_000m + 32_000m);
    }

    [Fact]
    public async Task GetMyDayDetail_ScrapLines_ExcludeReworkAndCancelled()
    {
        DefectRecord Defect(string type, string status, int qty, decimal? rate) => new()
        {
            ResponsibleWorkerId = 1, WorkDate = Today, DefectType = type, Status = status, Quantity = qty,
            PieceRate = rate is null ? null : new PieceRate { Rate = rate.Value }, Operation = Op("OP10", "May cổ", 1), OrderLine = Line(),
        };
        _defects.Add(Defect("SCRAP", "RECORDED", 3, 2_000m));        // tính
        _defects.Add(Defect("SCRAP", "CANCELLED", 5, 2_000m));       // đã hủy: bỏ
        _defects.Add(Defect("REWORK", "RECORDED", 4, null));         // làm lại không trừ tiền: bỏ

        var result = await _sut.GetMyDayDetailAsync(1, Today, CancellationToken.None);

        var scrap = result.Data!.Scraps.Should().ContainSingle().Subject;
        scrap.Quantity.Should().Be(3);
        scrap.Amount.Should().Be(6_000m);
    }

    [Fact]
    public async Task GetMyDayDetail_NothingOnThatDay_ReturnsEmptyLists()
    {
        var result = await _sut.GetMyDayDetailAsync(1, Today, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Outputs.Should().BeEmpty();
        result.Data.Scraps.Should().BeEmpty();
    }
}

public class WorkerPayValidatorTests
{
    private readonly MyEstimatedPayQueryValidator _sut = new();

    [Fact]
    public void NoDates_IsValid() => _sut.Validate(new MyEstimatedPayQuery()).IsValid.Should().BeTrue();

    [Fact]
    public void ToBeforeFrom_IsInvalid() =>
        _sut.Validate(new MyEstimatedPayQuery { From = new DateOnly(2026, 10, 10), To = new DateOnly(2026, 10, 9) })
            .IsValid.Should().BeFalse();

    [Fact]
    public void RangeWiderThanLimit_IsInvalid() =>
        _sut.Validate(new MyEstimatedPayQuery { From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 6, 1) })
            .IsValid.Should().BeFalse();

    [Fact]
    public void ExactlyMaxDays_IsValid()
    {
        var from = new DateOnly(2026, 7, 1);
        _sut.Validate(new MyEstimatedPayQuery { From = from, To = from.AddDays(MyEstimatedPayQueryValidator.MaxRangeDays - 1) })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void OnlyOneDateGiven_IsValid() =>
        _sut.Validate(new MyEstimatedPayQuery { From = new DateOnly(2026, 10, 1) }).IsValid.Should().BeTrue();
}
