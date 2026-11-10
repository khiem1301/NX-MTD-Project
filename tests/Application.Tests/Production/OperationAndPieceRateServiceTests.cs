using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>KI-01 (UC25, UC26): công đoạn của mã hàng và đơn giá công đoạn có duyệt.</summary>
[Collection(ExcelTestCollection.Name)]
public class OperationAndPieceRateServiceTests : ProductionTestBase
{
    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "MH001", ProductName = "Áo", Status = RecordStatus.Active }];
    private readonly List<Operation> _operations = [];
    private readonly List<PieceRate> _rates = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<WorkAllocation> _allocations = [];
    private readonly List<VwWorkerOutputCap> _caps = [];
    private readonly PieceRateService _rateService;
    private readonly OperationService _operationService;

    public OperationAndPieceRateServiceTests()
    {
        SetupQueryable(_products);
        SetupQueryable(_operations);
        SetupQueryable(_rates);
        SetupQueryable(_lines);
        SetupQueryable(_allocations);
        SetupQueryable(_caps);
        _rateService = new PieceRateService(Uow, Audit, Notifier, Clock);
        _operationService = new OperationService(Uow, Audit, Clock, new ClosedXmlOperationParser(), Notifier);
    }

    private Operation AddOperation(int id, string code = "OP10", bool final = false, string status = OperationStatuses.Active,
        short sequence = 1)
    {
        var op = new Operation
        {
            OperationId = id, ProductId = 1, OperationCode = code, OperationName = "Công đoạn " + code,
            StationSequence = sequence, TeamType = TeamTypes.Sewing, IsFinalStep = final, Status = status,
            Product = _products[0],
        };
        _operations.Add(op);
        Repo<Operation>().GetByIdAsync(id).Returns(op);
        return op;
    }

    private PieceRate AddRate(int id, int operationId, decimal rate, DateOnly date, string status = PieceRateStatuses.Approved)
    {
        var op = _operations.Single(o => o.OperationId == operationId);
        var r = new PieceRate { PieceRateId = id, OperationId = operationId, Rate = rate, EffectiveDate = date, Status = status, Operation = op };
        _rates.Add(r);
        Repo<PieceRate>().GetByIdAsync(id).Returns(r);
        return r;
    }

    private static SaveOperationDto OpDto(string code = "OP20", bool final = false, int productId = 1) =>
        new(productId, code, "May cổ", 2, null, null, null, final);

    // ========== Công đoạn ==========

    [Fact]
    public async Task CreateOperation_Valid_IsActiveSewingAndLogsInsert()
    {
        Operation? added = null;
        Repo<Operation>().When(r => r.AddAsync(Arg.Any<Operation>())).Do(c => added = c.Arg<Operation>());

        var result = await _operationService.CreateAsync(OpDto(" op20 "), default);

        result.IsSuccess.Should().BeTrue();
        added!.OperationCode.Should().Be("OP20");   // chuẩn hóa chữ hoa, bỏ khoảng trắng
        added.Status.Should().Be(OperationStatuses.Active);
        added.TeamType.Should().Be(TeamTypes.Sewing);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Operation) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task CreateOperation_UnknownProduct_ReturnsNotFound()
    {
        var result = await _operationService.CreateAsync(OpDto(productId: 99), default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task CreateOperation_DuplicateCodeInSameProduct_ReturnsDuplicate()
    {
        AddOperation(1, "OP10");

        var result = await _operationService.CreateAsync(OpDto("op10"), default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationCodeDuplicate);
    }

    [Fact]
    public async Task CreateOperation_NewFinalStep_MovesFinalFlagFromTheOldOne()
    {
        var old = AddOperation(1, "OP90", final: true);

        var result = await _operationService.CreateAsync(OpDto("OP91", final: true), default);

        result.IsSuccess.Should().BeTrue();
        old.IsFinalStep.Should().BeFalse();
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.RecordId == 1 && e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task UpdateOperation_TickingFinal_MovesFinalFlagAndKeepsExactlyOne()
    {
        var old = AddOperation(1, "OP90", final: true);
        var next = AddOperation(2, "OP95", sequence: 2);

        var result = await _operationService.UpdateAsync(2, OpDto("OP95", final: true), default);

        result.IsSuccess.Should().BeTrue();
        old.IsFinalStep.Should().BeFalse();
        next.IsFinalStep.Should().BeTrue();
        _operations.Count(o => o.IsFinalStep).Should().Be(1);
    }

    [Fact]
    public async Task DiscontinueOperation_ActiveFinalStep_IsAllowed()
    {
        var op = AddOperation(1, "OP90", final: true);

        var result = await _operationService.DiscontinueAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        op.Status.Should().Be(OperationStatuses.Discontinued);
    }

    [Fact]
    public async Task UpdateOperation_TickingFinal_SavesTheOldFinalBeforeTheNewOne()
    {
        AddOperation(2, "OP95", final: true, sequence: 2);
        AddOperation(1, "OP90");
        var order = new List<string>();
        Uow.When(u => u.SaveChangesAsync()).Do(_ => order.Add(string.Join(",", _operations.Where(o => o.IsFinalStep).Select(o => o.OperationCode))));

        await _operationService.UpdateAsync(1, OpDto("OP90", final: true), default);

        order.Should().StartWith([""]);   // lần lưu đầu: công đoạn cũ đã bỏ cờ, chưa công đoạn nào là cuối
        _operations.Count(o => o.IsFinalStep).Should().Be(1);
    }

    [Fact]
    public async Task CreateOperation_FinalStepAllowedWhenPreviousOneIsDiscontinued()
    {
        AddOperation(1, "OP90", final: true, status: OperationStatuses.Discontinued);

        var result = await _operationService.CreateAsync(OpDto("OP91", final: true), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateOperation_KeepingItsOwnFinalStep_IsAllowed_AndKeepsProduct()
    {
        var op = AddOperation(1, "OP90", final: true);

        var result = await _operationService.UpdateAsync(1, OpDto("OP90", final: true, productId: 99), default);

        result.IsSuccess.Should().BeTrue();
        op.ProductId.Should().Be(1);   // mã hàng của công đoạn không đổi
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Update && e.OldValues != null));
    }

    [Fact]
    public async Task ReactivateOperation_WhenAnotherFinalStepIsActive_IsRejected()
    {
        AddOperation(1, "OP90", final: true);
        var old = AddOperation(2, "OP95", final: true, status: OperationStatuses.Discontinued);

        var result = await _operationService.ReactivateAsync(2, default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationFinalStepExists);
        old.Status.Should().Be(OperationStatuses.Discontinued);
    }

    [Fact]
    public async Task DiscontinueOperation_SetsStatusAndLogs_AndIsIdempotent()
    {
        var op = AddOperation(1);

        await _operationService.DiscontinueAsync(1, default);
        await _operationService.DiscontinueAsync(1, default);

        op.Status.Should().Be(OperationStatuses.Discontinued);
        Audit.Received(1).Add(Arg.Any<AuditEntry>());   // lần gọi lại không ghi log thêm
    }

    [Fact]
    public async Task ListOperations_ReturnsPascalCaseAndCurrentApprovedRate()
    {
        AddOperation(1, "OP10", sequence: 1);
        AddOperation(2, "OP20", sequence: 2, status: OperationStatuses.Discontinued);
        AddRate(1, 1, 1_000m, Today.AddDays(-30));
        AddRate(2, 1, 1_200m, Today.AddDays(-1));
        AddRate(3, 1, 9_999m, Today.AddDays(5));   // chưa tới ngày hiệu lực

        var active = (await _operationService.ListAsync(1, false, default)).Data!;
        var all = (await _operationService.ListAsync(1, true, default)).Data!;

        active.Should().ContainSingle().Which.CurrentRate.Should().Be(1_200m);
        active[0].Status.Should().Be("Active");
        active[0].TeamType.Should().Be("Sewing");
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListOperations_NextRate_IsLatestRateThatIsNotYetCurrent()
    {
        AddOperation(1, "OP10", sequence: 1);   // đơn giá mới nhất đã duyệt và đang hiệu lực: không có "đơn giá sắp tới"
        AddOperation(2, "OP20", sequence: 2);   // có bản Nháp mới hơn đơn giá hiện hành
        AddOperation(3, "OP30", sequence: 3);   // chưa có đơn giá nào
        AddOperation(4, "OP40", sequence: 4);   // bản mới nhất bị từ chối
        AddRate(1, 1, 1_000m, Today.AddDays(-10));
        AddRate(2, 2, 1_000m, Today.AddDays(-10));
        AddRate(3, 2, 1_300m, Today.AddDays(20), PieceRateStatuses.Draft);
        AddRate(4, 4, 800m, Today.AddDays(-10));
        AddRate(5, 4, 900m, Today.AddDays(3), PieceRateStatuses.Rejected).DecisionNote = "Vượt khung";

        var items = (await _operationService.ListAsync(1, false, default)).Data!.ToDictionary(o => o.OperationId);

        items[1].NextRate.Should().BeNull();
        items[1].CurrentRate.Should().Be(1_000m);
        items[2].CurrentRate.Should().Be(1_000m);
        items[2].NextRate.Should().BeEquivalentTo(new OperationRateDto(3, 1_300m, Today.AddDays(20), "Draft", null));
        items[3].CurrentRate.Should().BeNull();
        items[3].NextRate.Should().BeNull();
        items[4].NextRate!.Status.Should().Be("Rejected");
        items[4].NextRate!.RejectReason.Should().Be("Vượt khung");
        items.Values.Should().OnlyContain(o => o.UpcomingRate == null);
    }

    [Fact]
    public async Task ListOperations_UpcomingRate_IsNearestApprovedFutureRate()
    {
        AddOperation(1, "OP10", sequence: 1);
        AddRate(1, 1, 1_000m, Today.AddDays(-10));
        AddRate(2, 1, 1_500m, Today.AddDays(30));                                  // đã duyệt, xa hơn
        AddRate(3, 1, 1_200m, Today.AddDays(5));                                   // đã duyệt, gần nhất
        AddRate(4, 1, 1_100m, Today.AddDays(2), PieceRateStatuses.PendingApproval); // chưa duyệt: không tính
        AddRate(5, 1, 1_300m, Today.AddDays(1), PieceRateStatuses.Rejected);        // bị từ chối: không tính

        var item = (await _operationService.ListAsync(1, false, default)).Data!.Single();

        item.CurrentRate.Should().Be(1_000m);
        item.UpcomingRate.Should().BeEquivalentTo(new OperationRateDto(3, 1_200m, Today.AddDays(5), "Approved", null));
    }

    // ========== Đơn giá ==========

    [Fact]
    public async Task ApproveMany_ApprovesOnlyPendingRatesAndNotifiesAccountantOnce()
    {
        AddOperation(1, "OP10");
        AddRate(1, 1, 100m, Today.AddDays(1), PieceRateStatuses.PendingApproval);
        AddRate(2, 1, 110m, Today.AddDays(2), PieceRateStatuses.PendingApproval);
        AddRate(3, 1, 120m, Today.AddDays(3), PieceRateStatuses.Draft);

        var result = await _rateService.ApproveManyAsync(new ApproveManyPieceRatesDto([1, 2, 3, 3], " ok "), default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(2);
        _rates.Where(r => r.PieceRateId is 1 or 2).Should().OnlyContain(r => r.Status == PieceRateStatuses.Approved
            && r.ApprovedAt == Now && r.DecisionNote == "ok");
        _rates.Single(r => r.PieceRateId == 3).Status.Should().Be(PieceRateStatuses.Draft);
        Audit.Received(2).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(PieceRate) && e.Action == AuditActions.Approve));
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Accountant, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveMany_NothingPending_ReturnsInvalidStatus()
    {
        AddOperation(1, "OP10");
        AddRate(1, 1, 100m, Today, PieceRateStatuses.Approved);

        var result = await _rateService.ApproveManyAsync(new ApproveManyPieceRatesDto([1], null), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateInvalidStatus);
    }

    [Fact]
    public async Task ApproveMany_EmptySelection_ReturnsValidationError()
    {
        var result = await _rateService.ApproveManyAsync(new ApproveManyPieceRatesDto([], null), default);

        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task CreateRate_Valid_StartsAsDraftAndLogs()
    {
        AddOperation(1);
        PieceRate? added = null;
        Repo<PieceRate>().When(r => r.AddAsync(Arg.Any<PieceRate>())).Do(c => added = c.Arg<PieceRate>());

        var result = await _rateService.CreateAsync(new SavePieceRateDto(1, 1_500m, Today), default);

        result.IsSuccess.Should().BeTrue();
        added!.Status.Should().Be(PieceRateStatuses.Draft);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(PieceRate) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task CreateRate_DiscontinuedOperation_IsRejected()
    {
        AddOperation(1, status: OperationStatuses.Discontinued);

        var result = await _rateService.CreateAsync(new SavePieceRateDto(1, 1_500m, Today), default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationNotActive);
    }

    [Fact]
    public async Task CreateRate_SameEffectiveDateTwice_IsRejected()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, Today, PieceRateStatuses.Draft);

        var result = await _rateService.CreateAsync(new SavePieceRateDto(1, 1_500m, Today), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateDuplicateDate);
    }

    [Theory]
    [InlineData(PieceRateStatuses.Draft, true)]
    [InlineData(PieceRateStatuses.Rejected, true)]
    [InlineData(PieceRateStatuses.PendingApproval, false)]
    [InlineData(PieceRateStatuses.Approved, false)]
    public async Task UpdateRate_OnlyDraftOrRejectedIsEditable(string status, bool editable)
    {
        AddOperation(1);
        var rate = AddRate(1, 1, 1_000m, Today, status);

        var result = await _rateService.UpdateAsync(1, new SavePieceRateDto(1, 2_000m, Today), default);

        result.IsSuccess.Should().Be(editable);
        rate.Rate.Should().Be(editable ? 2_000m : 1_000m);
        if (!editable) result.ErrorCode.Should().Be(ErrorCodes.PieceRateInvalidStatus);
    }

    [Fact]
    public async Task UpdateRate_AfterRejection_GoesBackToDraftAndClearsReason()
    {
        AddOperation(1);
        var rate = AddRate(1, 1, 1_000m, Today, PieceRateStatuses.Rejected);
        rate.DecisionNote = "Giá cao";

        await _rateService.UpdateAsync(1, new SavePieceRateDto(1, 900m, Today), default);

        rate.Status.Should().Be(PieceRateStatuses.Draft);
        rate.DecisionNote.Should().BeNull();
    }

    [Fact]
    public async Task Submit_Draft_BecomesPendingAndNotifiesAdmin()
    {
        AddOperation(1);
        var rate = AddRate(1, 1, 1_000m, Today, PieceRateStatuses.Draft);

        var result = await _rateService.SubmitAsync(1, default);

        result.IsSuccess.Should().BeTrue();
        rate.Status.Should().Be(PieceRateStatuses.PendingApproval);
        rate.SubmittedAt.Should().Be(Now);
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_AlreadyPending_IsRejected()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, Today, PieceRateStatuses.PendingApproval);

        (await _rateService.SubmitAsync(1, default)).ErrorCode.Should().Be(ErrorCodes.PieceRateInvalidStatus);
    }

    [Fact]
    public async Task Approve_Pending_BecomesApprovedLogsApproveAndNotifiesAccountant()
    {
        AddOperation(1);
        var rate = AddRate(1, 1, 1_000m, Today, PieceRateStatuses.PendingApproval);

        var result = await _rateService.ApproveAsync(1, new ApprovePieceRateDto(" ok "), default);

        result.IsSuccess.Should().BeTrue();
        rate.Status.Should().Be(PieceRateStatuses.Approved);
        rate.ApprovedAt.Should().Be(Now);
        rate.DecisionNote.Should().Be("ok");
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Approve));
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Accountant, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_AlreadyDecided_CannotBeChanged()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, Today, PieceRateStatuses.Rejected);

        (await _rateService.ApproveAsync(1, new ApprovePieceRateDto(null), default))
            .ErrorCode.Should().Be(ErrorCodes.PieceRateInvalidStatus);   // GBR-05
    }

    [Fact]
    public async Task Reject_Pending_StoresReasonAndLogsRejectWithReason()
    {
        AddOperation(1);
        var rate = AddRate(1, 1, 1_000m, Today, PieceRateStatuses.PendingApproval);

        var result = await _rateService.RejectAsync(1, new RejectPieceRateDto("  Giá cao hơn thị trường "), default);

        result.IsSuccess.Should().BeTrue();
        rate.Status.Should().Be(PieceRateStatuses.Rejected);
        rate.DecisionNote.Should().Be("Giá cao hơn thị trường");
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Reject && e.Reason == "Giá cao hơn thị trường"));
    }

    // ========== Đơn giá hiệu lực (khóa vào sản lượng) ==========

    [Fact]
    public async Task FindEffective_PicksLatestApprovedNotAfterDate_IgnoringDraftPendingAndFuture()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, new DateOnly(2026, 9, 1));
        AddRate(2, 1, 1_100m, new DateOnly(2026, 10, 1));                                  // đúng bản cần lấy
        AddRate(3, 1, 1_300m, new DateOnly(2026, 10, 10), PieceRateStatuses.PendingApproval);   // chưa duyệt
        AddRate(4, 1, 1_400m, new DateOnly(2026, 10, 15), PieceRateStatuses.Draft);
        AddRate(5, 1, 1_500m, new DateOnly(2026, 11, 1));                                  // chưa tới hiệu lực

        var effective = await _rateService.FindEffectiveAsync(1, Today, default);

        effective!.PieceRateId.Should().Be(2);
        effective.Rate.Should().Be(1_100m);
    }

    [Fact]
    public async Task FindEffective_NoApprovedRate_ReturnsNull()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, Today, PieceRateStatuses.PendingApproval);

        (await _rateService.FindEffectiveAsync(1, Today, default)).Should().BeNull();
    }

    [Fact]
    public async Task FindEffectiveMany_SameRuleAsSingle_ForEveryOperationAndSkipsUnapproved()
    {
        AddOperation(1);
        AddOperation(2);
        AddOperation(3);
        AddRate(1, 1, 1_000m, new DateOnly(2026, 9, 1));
        AddRate(2, 1, 1_100m, new DateOnly(2026, 10, 1));
        AddRate(3, 1, 1_500m, new DateOnly(2026, 11, 1));                                  // chưa tới hiệu lực
        AddRate(4, 2, 800m, new DateOnly(2026, 10, 5));
        AddRate(5, 3, 900m, Today, PieceRateStatuses.PendingApproval);                     // chưa duyệt

        var rates = await _rateService.FindEffectiveManyAsync([1, 2, 3], Today, default);

        rates.Keys.Should().BeEquivalentTo([1, 2]);
        rates[1].Should().Be(new EffectivePieceRate(2, 1_100m));
        rates[2].Should().Be(new EffectivePieceRate(4, 800m));
    }

    [Fact]
    public async Task ListRates_FiltersByPascalCaseStatusAndReturnsPascalCase()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, new DateOnly(2026, 9, 1));
        AddRate(2, 1, 1_100m, new DateOnly(2026, 10, 1), PieceRateStatuses.PendingApproval);

        var result = await _rateService.ListAsync(new PieceRateQuery { Status = "PendingApproval" }, default);

        var item = result.Data!.Items.Should().ContainSingle().Subject;
        item.PieceRateId.Should().Be(2);
        item.Status.Should().Be("PendingApproval");
        item.ProductCode.Should().Be("MH001");
    }

    // ---------- Biên và các bảng bị ảnh hưởng (DEV-ALL-50) ----------

    private OrderLine OpenLine(int id, string status = OrderStatuses.InProduction)
    {
        var line = new OrderLine { OrderLineId = id, OrderId = id, ProductId = 1, Order = new CustomerOrder { OrderId = id, Status = status } };
        _lines.Add(line);
        return line;
    }

    [Fact]
    public async Task Discontinue_WorkerStillHoldsWorkOfOpenOrder_IsRejected()
    {
        var op = AddOperation(1);
        OpenLine(50);
        _caps.Add(new VwWorkerOutputCap { OperationId = 1, OrderLineId = 50, WorkerId = 7, CapQty = 10, OutputQty = 4, RemainingQty = 6 });

        var result = await _operationService.DiscontinueAsync(1, default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationHasOpenWork);
        op.Status.Should().Be(OperationStatuses.Active);
    }

    [Fact]
    public async Task Discontinue_PendingHandoverOfOpenOrder_IsRejected()
    {
        AddOperation(1);
        OpenLine(50);
        _allocations.Add(new WorkAllocation { OperationId = 1, OrderLineId = 50, Status = HandoverStatuses.Pending });

        (await _operationService.DiscontinueAsync(1, default)).ErrorCode.Should().Be(ErrorCodes.OperationHasOpenWork);
    }

    [Fact]
    public async Task Discontinue_WorkDoneOrOrderClosedOrRejectedHandover_IsAllowed()
    {
        var op = AddOperation(1);
        OpenLine(50);
        OpenLine(60, OrderStatuses.Cancelled);
        _caps.Add(new VwWorkerOutputCap { OperationId = 1, OrderLineId = 50, WorkerId = 7, CapQty = 10, OutputQty = 10, RemainingQty = 0 });
        _caps.Add(new VwWorkerOutputCap { OperationId = 1, OrderLineId = 60, WorkerId = 7, CapQty = 10, OutputQty = 4, RemainingQty = 6 });
        _allocations.Add(new WorkAllocation { OperationId = 1, OrderLineId = 50, Status = HandoverStatuses.Rejected });

        (await _operationService.DiscontinueAsync(1, default)).IsSuccess.Should().BeTrue();

        op.Status.Should().Be(OperationStatuses.Discontinued);
    }

    [Fact]
    public async Task List_PendingRateIsShownEvenWhenAnotherRateHasLaterDate()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, Today.AddDays(3), PieceRateStatuses.PendingApproval);
        AddRate(2, 1, 1_100m, Today.AddDays(9), PieceRateStatuses.Rejected).DecisionNote = "Cao";

        var items = (await _operationService.ListAsync(1, false, default)).Data!;

        items[0].NextRate!.PieceRateId.Should().Be(1);
    }

    [Fact]
    public async Task Create_ProductDiscontinued_IsRejected()
    {
        _products[0].Status = RecordStatus.Discontinued;

        var result = await _operationService.CreateAsync(new SaveOperationDto(1, "OP01", "May", 1, null, null, "Sewing", false), default);

        result.ErrorCode.Should().Be(ErrorCodes.ProductNotActive);
    }

    [Fact]
    public async Task UpdateRate_OperationDiscontinued_IsRejected()
    {
        AddOperation(1, status: OperationStatuses.Discontinued);
        var rate = AddRate(1, 1, 1_000m, Today, PieceRateStatuses.Draft);
        Repo<PieceRate>().GetByIdAsync(1).Returns(rate);

        var result = await _rateService.UpdateAsync(1, new SavePieceRateDto(1, 1_200m, Today), default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationNotActive);
        rate.Rate.Should().Be(1_000m);
    }

    [Theory]
    [InlineData(PieceRateStatuses.Draft)]
    [InlineData(PieceRateStatuses.PendingApproval)]
    public async Task SubmitAndApprove_OperationDiscontinued_AreRejected(string status)
    {
        AddOperation(1, status: OperationStatuses.Discontinued);
        var rate = AddRate(1, 1, 1_000m, Today, status);

        var result = status == PieceRateStatuses.Draft
            ? await _rateService.SubmitAsync(1, default)
            : await _rateService.ApproveAsync(1, new ApprovePieceRateDto(null), default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationNotActive);
        rate.Status.Should().Be(status);
    }

    [Fact]
    public async Task ApproveMany_SkipsDiscontinuedOperation()
    {
        AddOperation(1);
        AddOperation(2, "OP20", status: OperationStatuses.Discontinued);
        AddRate(1, 1, 1_000m, Today, PieceRateStatuses.PendingApproval);
        AddRate(2, 2, 1_000m, Today, PieceRateStatuses.PendingApproval);

        (await _rateService.ApproveManyAsync(new ApproveManyPieceRatesDto([1, 2], null), default)).Data.Should().Be(1);
    }

    [Fact]
    public async Task SubmitMany_SubmitsOnlyDraftsOfActiveOperations_NotifiesAdminOnce()
    {
        AddOperation(1);
        AddOperation(2, "OP20", status: OperationStatuses.Discontinued);
        var draft = AddRate(1, 1, 1_000m, Today, PieceRateStatuses.Draft);
        var other = AddRate(2, 2, 1_000m, Today, PieceRateStatuses.Draft);
        var approved = AddRate(3, 1, 1_000m, Today.AddDays(-5));

        var result = await _rateService.SubmitManyAsync(new SubmitPieceRatesDto([1, 2, 3]), default);

        result.Data.Should().Be(1);
        draft.Status.Should().Be(PieceRateStatuses.PendingApproval);
        draft.SubmittedAt.Should().Be(Now);
        other.Status.Should().Be(PieceRateStatuses.Draft);
        approved.Status.Should().Be(PieceRateStatuses.Approved);
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitMany_NothingToSubmit_IsRejected()
    {
        AddOperation(1);
        AddRate(1, 1, 1_000m, Today.AddDays(-5));

        (await _rateService.SubmitManyAsync(new SubmitPieceRatesDto([1]), default)).ErrorCode.Should().Be(ErrorCodes.PieceRateInvalidStatus);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("0.01", true)]
    [InlineData("1200.50", true)]
    [InlineData("1200.555", false)]
    [InlineData("100000000", true)]
    [InlineData("100000000.01", false)]
    public void PieceRateValidator_RateBounds(string rate, bool valid) =>
        new SavePieceRateDtoValidator().Validate(new SavePieceRateDto(1, decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), Today))
            .IsValid.Should().Be(valid);

    [Fact]
    public void PieceRateValidator_MissingDate_IsRejected() =>
        new SavePieceRateDtoValidator().Validate(new SavePieceRateDto(1, 100m, default)).IsValid.Should().BeFalse();

    public static TheoryData<int[], bool> SubmitManyIds => new()
    {
        { [], false },
        { [0], false },
        { [1, 1], false },
        { [1, 2], true },
        { Enumerable.Range(1, PieceRateLimits.MaxRatesPerSubmit).ToArray(), true },
        { Enumerable.Range(1, PieceRateLimits.MaxRatesPerSubmit + 1).ToArray(), false },
    };

    [Theory]
    [MemberData(nameof(SubmitManyIds))]
    public void SubmitManyValidator_Bounds(int[] ids, bool valid) =>
        new SubmitPieceRatesDtoValidator().Validate(new SubmitPieceRatesDto(ids)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("OP01", true)]
    [InlineData(" op-1.a_b ", true)]
    [InlineData("Công đoạn", false)]
    [InlineData("OP 01", false)]
    public void SaveOperationValidator_CodeFormat(string code, bool valid) =>
        new SaveOperationDtoValidator().Validate(new SaveOperationDto(1, code, "May", 1, null, null, "Sewing", false)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("Sewing", true)]
    [InlineData("Cutting", false)]
    public void SaveOperationValidator_FinalStepMustBeSewing(string teamType, bool valid) =>
        new SaveOperationDtoValidator().Validate(new SaveOperationDto(1, "OP01", "May", 1, null, null, teamType, true)).IsValid.Should().Be(valid);

    // ========== Công đoạn ↔ chi tiết vải (DEV-ALL-86) ==========

    private const int Body = 10, Label = 11;

    private void SetupComponents() => SetupQueryable(new List<ProductComponent>
    {
        new() { ProductComponentId = Body, ProductId = 1, ComponentName = "Thân", ComponentType = ComponentTypes.Panel },
        new() { ProductComponentId = Label, ProductId = 1, ComponentName = "Nhãn", ComponentType = ComponentTypes.Trim },
    });

    [Fact]
    public async Task CreateOperation_LinksAPanelComponent()
    {
        SetupComponents();
        Operation? added = null;
        Repo<Operation>().When(r => r.AddAsync(Arg.Any<Operation>())).Do(c => added = c.Arg<Operation>());

        var result = await _operationService.CreateAsync(OpDto() with { ComponentIds = [Body, Body] }, default);

        result.IsSuccess.Should().BeTrue();
        added!.OperationComponents.Should().ContainSingle().Which.ProductComponentId.Should().Be(Body);
    }

    [Theory]
    [InlineData(Label)]   // phụ liệu không gắn công đoạn
    [InlineData(99)]      // chi tiết của mã hàng khác / không tồn tại
    public async Task CreateOperation_ComponentThatIsNotAPanelOfTheProduct_IsRejected(int component)
    {
        SetupComponents();

        var result = await _operationService.CreateAsync(OpDto() with { ComponentIds = [component] }, default);

        result.Kind.Should().Be(ResultKind.Validation);
        result.Message.Should().Be(Messages.OperationComponentInvalid);
    }
}

public class ProductionOperationValidatorTests
{
    [Fact]
    public void Operation_RequiresCodeNameAndPositiveSequence()
    {
        var validator = new SaveOperationDtoValidator();

        validator.Validate(new SaveOperationDto(1, "OP10", "May cổ", 1, null, null, null, false)).IsValid.Should().BeTrue();
        validator.Validate(new SaveOperationDto(1, "", "May cổ", 1, null, null, null, false)).IsValid.Should().BeFalse();
        validator.Validate(new SaveOperationDto(1, "OP10", "", 1, null, null, null, false)).IsValid.Should().BeFalse();
        validator.Validate(new SaveOperationDto(1, "OP10", "May cổ", 0, null, null, null, false)).IsValid.Should().BeFalse();
        validator.Validate(new SaveOperationDto(1, "OP10", "May cổ", 1, null, null, "Office", false)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void PieceRate_RejectNeedsReason_AndRateMustBePositive()
    {
        new RejectPieceRateDtoValidator().Validate(new RejectPieceRateDto(" ")).IsValid.Should().BeFalse();
        new RejectPieceRateDtoValidator().Validate(new RejectPieceRateDto("Giá cao")).IsValid.Should().BeTrue();
        new SavePieceRateDtoValidator().Validate(new SavePieceRateDto(1, -1, new DateOnly(2026, 10, 1))).IsValid.Should().BeFalse();
        new SavePieceRateDtoValidator().Validate(new SavePieceRateDto(1, 0, new DateOnly(2026, 10, 1))).IsValid.Should().BeFalse();   // Excel cũng coi 0 là chưa có đơn giá
    }

}
