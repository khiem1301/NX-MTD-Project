using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests.Dashboard;

/// <summary>
/// UTCID946–UTCID974: HomeDashboardService.
/// Assert khóa theo sheet HomeDashboardService — không nới Expected cho Actual lệch spec.
/// Clock Today/Now = 2026-11-24 (tháng này 2026-11, tháng trước 2026-10).
/// </summary>
public class HomeDashboardServiceTests : ProductionTestBase
{
    private static readonly DateTime DashNow = new(2026, 11, 24, 8, 0, 0);
    private static readonly DateOnly DashToday = new(2026, 11, 24);
    private const string ThisMonth = "2026-11";
    private const string LastMonth = "2026-10";
    private const int WorkerId = 42;
    private const int ElectricianId = 60;
    private const int TeamId = 7;

    private readonly IOrderProgressService _progress = Substitute.For<IOrderProgressService>();
    private readonly ICostSummaryService _costs = Substitute.For<ICostSummaryService>();
    private readonly IFinanceRepository _finance = Substitute.For<IFinanceRepository>();
    private readonly IRequestQueryService _requests = Substitute.For<IRequestQueryService>();
    private readonly IAttendanceService _attendance = Substitute.For<IAttendanceService>();
    private readonly IIncomingMaterialService _incoming = Substitute.For<IIncomingMaterialService>();
    private readonly IWorkerPayService _pay = Substitute.For<IWorkerPayService>();
    private readonly IMealService _meals = Substitute.For<IMealService>();

    public HomeDashboardServiceTests()
    {
        Clock.Now.Returns(DashNow);
        Clock.Today.Returns(DashToday);
        _progress.GetOverviewAsync(false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<OrderProgressDto>>.Success([]));
        _costs.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CostSummaryDto>.Failure(ErrorCodes.CommonNotFound, "chưa nhập"));
        _finance.GetRevenueRowsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<VwMonthlyRevenue>());
        _requests.ListInboxAsync(Arg.Any<RequestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<RequestListItemDto>>.Success(new PagedResult<RequestListItemDto>([], 1, 5, 0)));
        _attendance.GetSummaryAsync(Arg.Any<TimesheetQuery>(), Arg.Any<TimesheetScope>(), Arg.Any<CancellationToken>())
            .Returns(Result<TimesheetSummaryDto>.Success(new TimesheetSummaryDto(0, 0, 0, 0)));
        _incoming.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IncomingMaterialsDto>.Success(new IncomingMaterialsDto([], [], [])));
        _pay.GetMyEstimatedPayAsync(Arg.Any<int>(), Arg.Any<MyEstimatedPayQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<MyEstimatedPayDto>.Success(Pay()));
        _meals.GetMyMealAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result<MyMealDto>.Success(new MyMealDto(DashToday, false, false, false)));
        _meals.GetTeamSuggestionAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result<TeamMealSuggestionDto>.Success(
                new TeamMealSuggestionDto(DashToday, TeamId, "MAY1", 0, null, null, null, false)));
    }

    private HomeDashboardService Sut() =>
        new(Uow, Clock, User, _progress, _costs, _finance, _requests, _attendance, _incoming, _pay, _meals);

    // ---------- Giám đốc ----------

    [Fact]
    public async Task UTCID946_GetAdminAsync_PendingRatesPayrollRevenueCostAndFourRunningOrders()
    {
        // Arrange — 3 đơn giá PENDING, 1 bảng lương chờ duyệt, doanh thu/chi phí T11 và T10, 4 đơn đang chạy
        SetupQueryable(new List<PieceRate>
        {
            new() { PieceRateId = 1, Status = PieceRateStatuses.PendingApproval },
            new() { PieceRateId = 2, Status = PieceRateStatuses.PendingApproval },
            new() { PieceRateId = 3, Status = PieceRateStatuses.PendingApproval },
            new() { PieceRateId = 4, Status = PieceRateStatuses.Approved },
        });
        SetupQueryable(new List<Payroll>
        {
            new() { PayrollId = 1, Status = PayrollStatus.PendingApproval },
            new() { PayrollId = 2, Status = PayrollStatus.Draft },
        });
        _finance.GetRevenueRowsAsync(ThisMonth, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<VwMonthlyRevenue> { new() { Revenue = 1_500_000 }, new() { Revenue = 500_000 } });
        _finance.GetRevenueRowsAsync(LastMonth, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<VwMonthlyRevenue> { new() { Revenue = 1_200_000 } });
        _costs.GetAsync(ThisMonth, Arg.Any<CancellationToken>())
            .Returns(Result<CostSummaryDto>.Success(Cost(ThisMonth, 800_000)));
        _costs.GetAsync(LastMonth, Arg.Any<CancellationToken>())
            .Returns(Result<CostSummaryDto>.Success(Cost(LastMonth, 750_000)));
        _progress.GetOverviewAsync(false, Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<OrderProgressDto>>.Success(
            [
                Order(1, "InProduction"), Order(2, "InDelivery"), Order(3, "PartiallyDelivered"),
                Order(4, "InProduction"), Order(5, "New"),
            ]));

        // Act
        var result = await Sut().GetAdminAsync(CancellationToken.None);

        // Assert — PieceRatePending=3, PayrollPending=1, Revenue/Cost this & last, RunningOrders=4
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        using (new AssertionScope())
        {
            dto.PieceRatesPending.Should().Be(3);
            dto.PayrollsPending.Should().Be(1);
            dto.Revenue.Should().Be(new HomeMoneyDto(2_000_000, 1_200_000));
            dto.Cost.Should().Be(new HomeMoneyDto(800_000, 750_000));
            dto.OrdersRunning.Should().Be(4);
            dto.Orders.Select(o => o.OrderId).Should().Equal(1, 2, 3, 4);
        }
    }

    [Fact]
    public async Task UTCID947_GetAdminAsync_CostNotEnteredThisMonth_CostThisMonthIsNull()
    {
        // Arrange — chưa nhập chi phí tháng 11; tháng 10 đã có số
        SetupQueryable(new List<PieceRate>());
        SetupQueryable(new List<Payroll>());
        _costs.GetAsync(ThisMonth, Arg.Any<CancellationToken>())
            .Returns(Result<CostSummaryDto>.Failure(ErrorCodes.CommonNotFound, "chưa nhập"));
        _costs.GetAsync(LastMonth, Arg.Any<CancellationToken>())
            .Returns(Result<CostSummaryDto>.Success(Cost(LastMonth, 100)));

        // Act
        var result = await Sut().GetAdminAsync(CancellationToken.None);

        // Assert — CostThisMonth=null (không phải 0)
        result.IsSuccess.Should().BeTrue();
        result.Data!.Cost.ThisMonth.Should().BeNull();
        result.Data.Cost.LastMonth.Should().Be(100);
    }

    [Fact]
    public async Task UTCID948_GetAdminAsync_OrderStatusStoredAsInProduction_StillCounted()
    {
        // Arrange — đơn status lưu "IN_PRODUCTION"; RunningStatuses dạng Pascal
        SetupQueryable(new List<PieceRate>());
        SetupQueryable(new List<Payroll>());
        _progress.GetOverviewAsync(false, Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<OrderProgressDto>>.Success([Order(1, OrderStatuses.InProduction)]));

        // Act
        var result = await Sut().GetAdminAsync(CancellationToken.None);

        // Assert khóa spec: đơn vẫn được đếm. (Cảnh báo sheet: lệch casing → đếm 0 — không hạ Expected về 0.)
        result.IsSuccess.Should().BeTrue();
        result.Data!.OrdersRunning.Should().Be(1);
        result.Data.Orders.Should().ContainSingle(o => o.OrderId == 1);
    }

    [Fact]
    public async Task UTCID949_GetAdminAsync_RevenueThrows_FallsBackToZeroWithoutException()
    {
        // Arrange — mock service doanh thu ném exception
        SetupQueryable(new List<PieceRate>());
        SetupQueryable(new List<Payroll>());
        _finance.GetRevenueRowsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<VwMonthlyRevenue>>>(_ => throw new InvalidOperationException("revenue failed"));
        _costs.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CostSummaryDto>.Success(Cost(ThisMonth, 10)));

        // Act
        var act = async () => await Sut().GetAdminAsync(CancellationToken.None);

        // Assert khóa spec: không exception; Revenue=0 (fallback)
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        result.Data!.Revenue.Should().Be(new HomeMoneyDto(0, 0));
    }

    // ---------- Quản đốc ----------

    [Fact]
    public async Task UTCID950_GetManagerAsync_ReceiptsInboxActingLeaderAndOpenOrders()
    {
        // Arrange — 2 phiếu nhận chờ; inbox 3 việc; 1 quyền tổ trưởng tạm; 5 đơn mở
        SetupQueryable(new List<MaterialReceipt>
        {
            new() { MaterialReceiptId = 1, ReceiptCode = "PN-1", Status = ReceiptStatuses.Pending },
            new() { MaterialReceiptId = 2, ReceiptCode = "PN-2", Status = ReceiptStatuses.Pending },
            new() { MaterialReceiptId = 3, ReceiptCode = "PN-3", Status = ReceiptStatuses.Confirmed },
        });
        SetupQueryable(new List<VwTeamActingLeader>
        {
            new() { TeamId = 2, TeamCode = "CAT1", IsActingLeader = true },
            new() { TeamId = 1, TeamCode = "MAY1", IsActingLeader = false },
        });
        var inbox = new List<RequestListItemDto> { RequestItem(1), RequestItem(2), RequestItem(3) };
        _requests.ListInboxAsync(Arg.Any<RequestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<RequestListItemDto>>.Success(new PagedResult<RequestListItemDto>(inbox, 1, 5, 3)));
        _progress.GetOverviewAsync(false, Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<OrderProgressDto>>.Success(
                [Order(1, "New"), Order(2, "InProduction"), Order(3, "InDelivery"), Order(4, "PartiallyDelivered"), Order(5, "New")]));

        // Act
        var result = await Sut().GetManagerAsync(CancellationToken.None);

        // Assert — Success DTO đủ 4 khối số liệu
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        using (new AssertionScope())
        {
            dto.ReceiptsPending.Should().Be(2);
            dto.RequestsPending.Should().Be(3);
            dto.Requests.Should().HaveCount(3);
            dto.LeaderAbsentTeams.Should().Equal("CAT1");
            dto.Orders.Should().HaveCount(5);
        }
    }

    [Fact]
    public async Task UTCID951_GetManagerAsync_InboxHasSeven_ReturnsFive()
    {
        // Arrange — inbox có 7 việc; service xin trang kích thước LatestCount
        SetupQueryable(new List<MaterialReceipt>());
        SetupQueryable(new List<VwTeamActingLeader>());
        var all = Enumerable.Range(1, 7).Select(RequestItem).ToList();
        _requests.ListInboxAsync(Arg.Any<RequestQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<RequestQuery>();
                var page = all.Take(query.PageSize).ToList();
                return Result<PagedResult<RequestListItemDto>>.Success(
                    new PagedResult<RequestListItemDto>(page, query.Page, query.PageSize, all.Count));
            });

        // Act
        var result = await Sut().GetManagerAsync(CancellationToken.None);

        // Assert — inbox trả 5 (giới hạn); tổng việc vẫn là 7
        result.IsSuccess.Should().BeTrue();
        result.Data!.Requests.Should().HaveCount(5);
        result.Data.Requests.Select(r => r.RequestId).Should().Equal(1, 2, 3, 4, 5);
        result.Data.RequestsPending.Should().Be(7);
    }

    [Fact]
    public async Task UTCID952_GetManagerAsync_InboxThrows_EmptyInboxAndOtherDataRemains()
    {
        // Arrange — mock inbox ném; phiếu nhận, tổ trưởng tạm và đơn mở vẫn có
        SetupQueryable(new List<MaterialReceipt>
        {
            new() { MaterialReceiptId = 1, ReceiptCode = "PN-1", Status = ReceiptStatuses.Pending },
        });
        SetupQueryable(new List<VwTeamActingLeader>
        {
            new() { TeamId = 2, TeamCode = "CAT1", IsActingLeader = true },
        });
        _progress.GetOverviewAsync(false, Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<OrderProgressDto>>.Success([Order(1, "InProduction"), Order(2, "New")]));
        _requests.ListInboxAsync(Arg.Any<RequestQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<PagedResult<RequestListItemDto>>>>(_ => throw new InvalidOperationException("inbox failed"));

        // Act
        var act = async () => await Sut().GetManagerAsync(CancellationToken.None);

        // Assert khóa spec: Inbox = []; phần khác vẫn có dữ liệu; không exception
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        using (new AssertionScope())
        {
            dto.Requests.Should().BeEmpty();
            dto.RequestsPending.Should().Be(0);
            dto.ReceiptsPending.Should().Be(1);
            dto.LeaderAbsentTeams.Should().Equal("CAT1");
            dto.Orders.Should().HaveCount(2);
        }
    }

    // ---------- Kế toán ----------

    [Fact]
    public async Task UTCID953_GetAccountantAsync_GapsUnpaidOrdersAndMonthStatus()
    {
        // Arrange — 4 NV thiếu chấm công; 2 công đoạn thiếu đơn giá; 3 PO chưa trả; 2 đơn chưa chốt; chi phí/lương tháng
        _attendance.GetSummaryAsync(Arg.Any<TimesheetQuery>(), TimesheetScope.All, Arg.Any<CancellationToken>())
            .Returns(Result<TimesheetSummaryDto>.Success(new TimesheetSummaryDto(20, 80, 4, 4)));
        SetupQueryable(new List<Operation>
        {
            new() { OperationId = 1, ProductId = 10, Status = OperationStatuses.Active },
            new() { OperationId = 2, ProductId = 11, Status = OperationStatuses.Active },
        });
        SetupQueryable(new List<PieceRate>());
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = 2, OrderCode = "DH-2", Status = OrderStatuses.InProduction, FinalAmount = null, Customer = Customer("B") },
            new() { OrderId = 1, OrderCode = "DH-1", Status = OrderStatuses.New, FinalAmount = null, Customer = Customer("A") },
            new() { OrderId = 3, OrderCode = "DH-3", Status = OrderStatuses.InProduction, FinalAmount = 5, Customer = Customer("C") },
            new() { OrderId = 4, OrderCode = "DH-4", Status = OrderStatuses.Cancelled, FinalAmount = null, Customer = Customer("D") },
            new() { OrderId = 5, OrderCode = "DH-5", Status = OrderStatuses.Stopped, FinalAmount = null, Customer = Customer("E") },
        });
        SetupQueryable(new List<PurchaseOrder>
        {
            new() { PurchaseOrderId = 3, PoCode = "PO-3", GoodsStatus = PurchaseGoodsStatuses.Received, PaymentStatus = PurchaseOrderStatuses.Unpaid, Supplier = Supplier("S3") },
            new() { PurchaseOrderId = 2, PoCode = "PO-2", GoodsStatus = PurchaseGoodsStatuses.Ordered, PaymentStatus = PurchasePaymentStatuses.PartiallyPaid, Supplier = Supplier("S2") },
            new() { PurchaseOrderId = 1, PoCode = "PO-1", GoodsStatus = PurchaseGoodsStatuses.PartiallyReceived, PaymentStatus = PurchaseOrderStatuses.Unpaid, Supplier = Supplier("S1") },
            new() { PurchaseOrderId = 4, PoCode = "PO-4", GoodsStatus = PurchaseGoodsStatuses.Received, PaymentStatus = PurchasePaymentStatuses.Paid, Supplier = Supplier("S4") },
            new() { PurchaseOrderId = 5, PoCode = "PO-5", GoodsStatus = PurchaseGoodsStatuses.Rejected, PaymentStatus = PurchaseOrderStatuses.Unpaid, Supplier = Supplier("S5") },
        });
        SeedNovemberCostAndPayroll(PayrollStatus.PendingApproval);

        // Act
        var result = await Sut().GetAccountantAsync(CancellationToken.None);

        // Assert — đúng số liệu
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        using (new AssertionScope())
        {
            dto.MissingPunch.Should().Be(4);
            dto.PieceRatesMissing.Should().Be(2);
            dto.PieceRatesRejected.Should().Be(0);
            dto.PurchasesUnpaid.Should().Be(3);
            dto.OrdersNotSet.Should().Be(2);
            dto.CostEntered.Should().BeTrue();
            dto.PayrollStatus.Should().Be("PendingApproval");
            dto.Orders.Select(o => o.Title).Should().Equal("DH-2", "DH-1");
            dto.Purchases.Should().HaveCount(3);
        }
    }

    [Fact]
    public async Task UTCID954_GetAccountantAsync_NoUnpaidPurchaseOrders_UnpaidIsZero()
    {
        // Arrange — không có PO chưa trả (đã trả hết hoặc bị từ chối)
        SetupQueryable(new List<Operation>());
        SetupQueryable(new List<PieceRate>());
        SetupQueryable(new List<CustomerOrder>());
        SetupQueryable(new List<PurchaseOrder>
        {
            new() { PurchaseOrderId = 1, PoCode = "PO-1", GoodsStatus = PurchaseGoodsStatuses.Received, PaymentStatus = PurchasePaymentStatuses.Paid },
            new() { PurchaseOrderId = 2, PoCode = "PO-2", GoodsStatus = PurchaseGoodsStatuses.Rejected, PaymentStatus = PurchaseOrderStatuses.Unpaid },
        });
        SetupQueryable(new List<AccountingPeriod>());
        SetupQueryable(new List<MonthlyCost>());
        SetupQueryable(new List<Payroll>());

        // Act
        var result = await Sut().GetAccountantAsync(CancellationToken.None);

        // Assert — UnpaidPO=0
        result.IsSuccess.Should().BeTrue();
        result.Data!.PurchasesUnpaid.Should().Be(0);
        result.Data.Purchases.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID955_GetAccountantAsync_TimesheetThrows_MissingPunchFallsBackToZero()
    {
        // Arrange — mock timesheet ném exception; các nguồn khác vẫn có số
        _attendance.GetSummaryAsync(Arg.Any<TimesheetQuery>(), Arg.Any<TimesheetScope>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<TimesheetSummaryDto>>>(_ => throw new InvalidOperationException("timesheet failed"));
        SetupQueryable(new List<Operation>
        {
            new() { OperationId = 1, ProductId = 10, Status = OperationStatuses.Active },
        });
        SetupQueryable(new List<PieceRate>());
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = 1, OrderCode = "DH-1", Status = OrderStatuses.New, FinalAmount = null, Customer = Customer("A") },
        });
        SetupQueryable(new List<PurchaseOrder>());
        SeedNovemberCostAndPayroll(PayrollStatus.Draft);

        // Act
        var act = async () => await Sut().GetAccountantAsync(CancellationToken.None);

        // Assert khóa spec: MissingPunch=0 (fallback); không exception
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        result.Data!.MissingPunch.Should().Be(0);
    }

    // ---------- Thủ kho ----------

    [Fact]
    public async Task UTCID956_GetWarehouseAsync_IncomingWorkOrdersRequestsAndPendingHandovers()
    {
        // Arrange — 3 lô đến; 2 lệnh cắt chưa cấp NVL; 4 đơn xin; phiếu cắt/TP/trả chờ
        _incoming.GetAsync(Arg.Any<CancellationToken>()).Returns(Result<IncomingMaterialsDto>.Success(new IncomingMaterialsDto(
            [CustomerLot(1, "DH-5", "NHAN", 100)],
            [SupplierLot(2, "PO-2", "VAI", 180), SupplierLot(3, "PO-3", "CHI", 40)],
            [])));
        _requests.ListInboxAsync(Arg.Any<RequestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<RequestListItemDto>>.Success(new PagedResult<RequestListItemDto>([RequestItem(9)], 1, 1, 4)));
        SetupQueryable(new List<WorkOrder>
        {
            new() { WorkOrderId = 1, WorkOrderCode = "LC-1", Status = WorkOrderStatuses.Open },
            new() { WorkOrderId = 2, WorkOrderCode = "LC-2", Status = WorkOrderStatuses.Cutting },
            new() { WorkOrderId = 3, WorkOrderCode = "LC-3", Status = WorkOrderStatuses.Open },
            new() { WorkOrderId = 4, WorkOrderCode = "LC-4", Status = WorkOrderStatuses.Completed },
        });
        SetupQueryable(new List<MaterialIssue>
        {
            new() { MaterialIssueId = 1, IssueCode = "PC-1", WorkOrderId = 3, Status = IssueStatuses.Received, WarehouseType = WarehouseTypes.Material },
        });
        SetupQueryable(new List<CutHandover>
        {
            new() { CutHandoverId = 1, HandoverCode = "BG-1", Status = HandoverStatuses.Pending, WorkOrder = WorkOrderOf("DH-1") },
            new() { CutHandoverId = 2, HandoverCode = "BG-2", Status = HandoverStatuses.Pending, WorkOrder = WorkOrderOf("DH-2") },
            new() { CutHandoverId = 3, HandoverCode = "BG-3", Status = HandoverStatuses.Confirmed, WorkOrder = WorkOrderOf("DH-3") },
        });
        SetupQueryable(new List<FgHandover>
        {
            new() { FgHandoverId = 1, HandoverCode = "TP-1", Status = HandoverStatuses.Pending, Order = new CustomerOrder { OrderCode = "DH-5" } },
        });
        SetupQueryable(new List<MaterialReturn>
        {
            new() { MaterialReturnId = 1, ReturnCode = "TR-1", Status = HandoverStatuses.Pending, ReturningTeam = new Team { TeamCode = "CAT1" } },
        });

        // Act
        var result = await Sut().GetWarehouseAsync(CancellationToken.None);

        // Assert — đủ khối
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        using (new AssertionScope())
        {
            dto.Incoming.Should().Be(3);
            dto.IncomingItems.Select(i => i.Tag).Should().Equal("NCC", "NCC", "Khách cấp");
            dto.WorkOrdersNoIssue.Should().Be(2);
            dto.RequestsToIssue.Should().Be(4);
            dto.CutHandovers.Should().Be(2);
            dto.FgHandovers.Should().Be(1);
            dto.Returns.Should().Be(1);
            dto.PendingItems.Select(i => i.Title).Should().Equal("BG-2", "BG-1", "TP-1", "TR-1");
        }
    }

    [Fact]
    public async Task UTCID957_GetWarehouseAsync_NothingPending_AllCountsZeroAndListsEmpty()
    {
        // Arrange — không lô đến, lệnh, đơn xin, phiếu chờ
        _incoming.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IncomingMaterialsDto>.Success(new IncomingMaterialsDto([], [], [])));
        _requests.ListInboxAsync(Arg.Any<RequestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<RequestListItemDto>>.Success(new PagedResult<RequestListItemDto>([], 1, 1, 0)));
        SetupQueryable(new List<WorkOrder>());
        SetupQueryable(new List<MaterialIssue>());
        SetupQueryable(new List<CutHandover>());
        SetupQueryable(new List<FgHandover>());
        SetupQueryable(new List<MaterialReturn>());

        // Act
        var result = await Sut().GetWarehouseAsync(CancellationToken.None);

        // Assert — mọi số đếm = 0; list rỗng
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        using (new AssertionScope())
        {
            dto.Incoming.Should().Be(0);
            dto.WorkOrdersNoIssue.Should().Be(0);
            dto.RequestsToIssue.Should().Be(0);
            dto.CutHandovers.Should().Be(0);
            dto.FgHandovers.Should().Be(0);
            dto.Returns.Should().Be(0);
            dto.IncomingItems.Should().BeEmpty();
            dto.PendingItems.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID958_GetWarehouseAsync_IncomingThrows_IncomingIsZeroAndEmpty()
    {
        // Arrange — mock incoming ném exception
        _incoming.GetAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Result<IncomingMaterialsDto>>>(_ => throw new InvalidOperationException("incoming failed"));
        SetupQueryable(new List<WorkOrder>());
        SetupQueryable(new List<MaterialIssue>());
        SetupQueryable(new List<CutHandover>());
        SetupQueryable(new List<FgHandover>());
        SetupQueryable(new List<MaterialReturn>());

        // Act
        var act = async () => await Sut().GetWarehouseAsync(CancellationToken.None);

        // Assert khóa spec: Incoming = (0, []); không exception
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        result.Data!.Incoming.Should().Be(0);
        result.Data.IncomingItems.Should().BeEmpty();
    }

    // ---------- Tổ trưởng ----------

    [Fact]
    public async Task UTCID959_GetTeamLeaderAsync_Cutting_CuttingPanelHasDataSewingNull()
    {
        // Arrange — tổ trưởng tổ cắt
        User.TeamId.Returns(TeamId);
        User.TeamTypeCode.Returns(TeamTypes.Cutting);
        SetupQueryable(new List<MaterialIssue>
        {
            new() { MaterialIssueId = 1, IssueCode = "PC-1", ReceivingTeamId = TeamId, Status = IssueStatuses.PendingReceipt, WarehouseType = WarehouseTypes.Material },
        });
        SetupQueryable(new List<VwWorkOrderLineProgress>
        {
            new() { WorkOrderId = 1, OrderLineId = 10, CuttingTeamId = TeamId, WorkOrderStatus = WorkOrderStatuses.Open, PlannedQty = 100, CutQty = 60, HandedToWarehouseQty = 20 },
        });
        SetupQueryable(new List<WorkOrder>
        {
            new() { WorkOrderId = 1, WorkOrderCode = "LC-1", CuttingTeamId = TeamId, Status = WorkOrderStatuses.Open, Order = new CustomerOrder { OrderCode = "DH-5" } },
        });
        SetupQueryable(new List<MaterialReturn>());

        // Act
        var result = await Sut().GetTeamLeaderAsync(CancellationToken.None);

        // Assert — CuttingPanel có dữ liệu; SewingPanel=null
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.Sewing.Should().BeNull();
        dto.Cutting.Should().NotBeNull();
        using (new AssertionScope())
        {
            dto.TeamType.Should().Be(TeamTypes.Cutting);
            dto.Cutting!.IssuesPending.Should().Be(1);
            dto.Cutting.CutNotHanded.Should().Be(40);
            dto.Cutting.WorkOrders.Should().ContainSingle()
                .Which.Should().Be(new HomeWorkOrderDto(1, "LC-1", "DH-5", "Open", 100, 60));
        }
    }

    [Fact]
    public async Task UTCID960_GetTeamLeaderAsync_Sewing_SewingPanelHasDataCuttingNull()
    {
        // Arrange — tổ trưởng tổ may
        User.TeamId.Returns(TeamId);
        User.TeamTypeCode.Returns(TeamTypes.Sewing);
        SetupQueryable(new List<VwKpiDailyResult>
        {
            new() { SewingTeamId = TeamId, WorkDate = DashToday, DailyTarget = 120, FinishedQty = 45 },
        });
        SetupQueryable(new List<MaterialIssue>
        {
            new() { MaterialIssueId = 1, IssueCode = "PC-BTP", OrderId = 5, ReceivingTeamId = TeamId, Status = IssueStatuses.PendingReceipt, WarehouseType = WarehouseTypes.SemiFinished },
        });
        SetupQueryable(new List<MaterialIssueLine>());
        SetupQueryable(new List<OrderLine>());
        SetupQueryable(new List<Operation>());
        SetupQueryable(new List<OperationAssignment>());
        SetupQueryable(new List<WorkAllocation>
        {
            new() { WorkAllocationId = 1, SewingTeamId = TeamId, Status = HandoverStatuses.Pending },
        });
        _meals.GetTeamSuggestionAsync(TeamId, DashToday, Arg.Any<CancellationToken>()).Returns(
            Result<TeamMealSuggestionDto>.Success(new TeamMealSuggestionDto(DashToday, TeamId, "MAY1", 18, 16, null, null, false)));

        // Act
        var result = await Sut().GetTeamLeaderAsync(CancellationToken.None);

        // Assert — SewingPanel có dữ liệu; CuttingPanel=null
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        dto.Cutting.Should().BeNull();
        dto.Sewing.Should().NotBeNull();
        using (new AssertionScope())
        {
            dto.TeamType.Should().Be(TeamTypes.Sewing);
            dto.Sewing!.KpiTarget.Should().Be(120);
            dto.Sewing.KpiActual.Should().Be(45);
            dto.Sewing.BtpPending.Should().Be(1);
            dto.Sewing.AllocationsPending.Should().Be(1);
            dto.Sewing.MealSuggested.Should().Be(18);
            dto.Sewing.MealConfirmed.Should().Be(16);
        }
    }

    [Fact]
    public async Task UTCID961_GetTeamLeaderAsync_TeamIdNull_BothPanelsNull()
    {
        // Arrange — TeamId=null
        User.TeamId.Returns((int?)null);
        User.TeamTypeCode.Returns(TeamTypes.Sewing);

        // Act
        var act = async () => await Sut().GetTeamLeaderAsync(CancellationToken.None);

        // Assert — cả 2 panel = null; không exception
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        result.Data!.Cutting.Should().BeNull();
        result.Data.Sewing.Should().BeNull();
    }

    [Fact]
    public async Task UTCID962_GetTeamLeaderAsync_OfficeTeam_BothPanelsNull()
    {
        // Arrange — tổ loại Office (có TeamId, không phải cắt/may)
        User.TeamId.Returns(3);
        User.TeamTypeCode.Returns(TeamTypes.Office);

        // Act
        var result = await Sut().GetTeamLeaderAsync(CancellationToken.None);

        // Assert — cả 2 panel = null
        result.IsSuccess.Should().BeTrue();
        result.Data!.TeamType.Should().Be(TeamTypes.Office);
        result.Data.Cutting.Should().BeNull();
        result.Data.Sewing.Should().BeNull();
    }

    // ---------- QA/QC ----------

    [Fact]
    public async Task UTCID963_GetQaqcAsync_IssuedFinishedScrap_OnLineAndNotReceived()
    {
        // Arrange — đã cấp 500, hoàn thành 300, hủy 20; 100 TP chưa nhập kho
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = 1, Status = OrderStatuses.InProduction },
        });
        SetupQueryable(new List<VwOrderLineProgress>
        {
            Line(1, issued: 500, finished: 300, received: 200, scrap: 20),
        });
        SetupQueryable(new List<FgHandover>());

        // Act
        var result = await Sut().GetQaqcAsync(CancellationToken.None);

        // Assert — OnLineQty=180; FinishedNotReceived=100
        result.IsSuccess.Should().BeTrue();
        result.Data!.OnLineQty.Should().Be(180);
        result.Data.NotReceivedQty.Should().Be(100);
    }

    [Fact]
    public async Task UTCID964_GetQaqcAsync_NegativeOnLine_ClampedToZero()
    {
        // Arrange — issued − finished − scrap = −10 (90 − 80 − 20)
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = 1, Status = OrderStatuses.InProduction },
        });
        SetupQueryable(new List<VwOrderLineProgress>
        {
            Line(1, issued: 90, finished: 80, received: 80, scrap: 20),
        });
        SetupQueryable(new List<FgHandover>());

        // Act
        var result = await Sut().GetQaqcAsync(CancellationToken.None);

        // Assert — OnLineQty=0 (Max(0, …))
        result.IsSuccess.Should().BeTrue();
        result.Data!.OnLineQty.Should().Be(0);
    }

    [Fact]
    public async Task UTCID965_GetQaqcAsync_IssuedQtyThrows_OnLineFallsBackToZero()
    {
        // Arrange — nguồn số cấp phát (vw_OrderLineProgress) ném exception
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = 1, Status = OrderStatuses.InProduction },
        });
        ThrowOnQuery<VwOrderLineProgress>();
        SetupQueryable(new List<FgHandover>());

        // Act
        var act = async () => await Sut().GetQaqcAsync(CancellationToken.None);

        // Assert khóa spec: OnLineQty=0 (fallback); không exception
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        result.Data!.OnLineQty.Should().Be(0);
    }

    // ---------- Cơ điện ----------

    [Fact]
    public async Task UTCID966_GetElectricianAsync_OpenTicketsOfCurrentUserOnly()
    {
        // Arrange — 3 ticket open của user hiện tại; 2 ticket của người khác
        User.EmployeeId.Returns(ElectricianId);
        SetupQueryable(new List<Request>
        {
            Ticket(1, ElectricianId, RequestStatuses.PendingApproval),
            Ticket(2, ElectricianId, RequestStatuses.PendingProcessing),
            Ticket(3, ElectricianId, RequestStatuses.Approved),
            Ticket(4, ElectricianId + 1, RequestStatuses.PendingApproval),
            Ticket(5, ElectricianId + 1, RequestStatuses.Approved),
        });

        // Act
        var result = await Sut().GetElectricianAsync(CancellationToken.None);

        // Assert — trả 3 ticket của user hiện tại
        result.IsSuccess.Should().BeTrue();
        result.Data!.Tickets.Select(t => t.RequestId).Should().Equal(3, 2, 1);
        result.Data.Tickets.Should().OnlyContain(t => t.Code.StartsWith("DT-", StringComparison.Ordinal));
        result.Data.Tickets.Should().BeEquivalentTo(
        [
            new HomeTicketDto(3, "DT-3", "SpareParts", "Approved", null, DashNow),
            new HomeTicketDto(2, "DT-2", "SpareParts", "PendingProcessing", null, DashNow),
            new HomeTicketDto(1, "DT-1", "SpareParts", "PendingApproval", null, DashNow),
        ]);
    }

    [Fact]
    public async Task UTCID967_GetElectricianAsync_TwentyFiveOpen_ReturnsTwenty()
    {
        // Arrange — 25 ticket open của user
        User.EmployeeId.Returns(ElectricianId);
        SetupQueryable(Enumerable.Range(1, 25)
            .Select(id => Ticket(id, ElectricianId, RequestStatuses.PendingProcessing))
            .ToList());

        // Act
        var result = await Sut().GetElectricianAsync(CancellationToken.None);

        // Assert — trả 20 (giới hạn), ticket mới nhất trước
        result.IsSuccess.Should().BeTrue();
        result.Data!.Tickets.Should().HaveCount(20);
        result.Data.Tickets.Select(t => t.RequestId).Should().Equal(Enumerable.Range(6, 20).Reverse());
    }

    [Fact]
    public async Task UTCID968_GetElectricianAsync_NoTickets_EmptyList()
    {
        // Arrange — không có ticket
        User.EmployeeId.Returns(ElectricianId);
        SetupQueryable(new List<Request>());

        // Act
        var result = await Sut().GetElectricianAsync(CancellationToken.None);

        // Assert — list rỗng
        result.IsSuccess.Should().BeTrue();
        result.Data!.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID969_GetElectricianAsync_ClosedExcluded_AndRepoThrowFallsBackToEmpty()
    {
        // Arrange — ticket của user đã đóng (Completed)
        User.EmployeeId.Returns(ElectricianId);
        SetupQueryable(new List<Request>
        {
            Ticket(1, ElectricianId, RequestStatuses.Completed),
        });

        // Act
        var first = await Sut().GetElectricianAsync(CancellationToken.None);

        // Assert — ticket Closed không được trả
        first.IsSuccess.Should().BeTrue();
        first.Data!.Tickets.Should().BeEmpty();

        // Arrange — lần gọi khác: repo ticket ném exception
        ThrowOnQuery<Request>();
        var act = async () => await Sut().GetElectricianAsync(CancellationToken.None);

        // Assert khóa spec: list rỗng (fallback), không exception
        var second = (await act.Should().NotThrowAsync()).Which;
        second.IsSuccess.Should().BeTrue();
        second.Data!.Tickets.Should().BeEmpty();
    }

    // ---------- Công nhân ----------

    [Fact]
    public async Task UTCID970_GetWorkerAsync_PieceRateTodayPayslipAllocationsDefectAndMeal()
    {
        // Arrange — khoán, sản lượng hôm nay, payslip tháng 10, 2 allocation PENDING, 1 defect 3 ngày trước, suất ăn hôm nay
        User.EmployeeId.Returns(WorkerId);
        var todayPay = PayDay(DashToday, 186);
        _pay.GetMyEstimatedPayAsync(WorkerId, Arg.Any<MyEstimatedPayQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<MyEstimatedPayDto>.Success(Pay(todayPay)));
        SetupQueryable(new List<Payslip>
        {
            new()
            {
                EmployeeId = WorkerId, NetPay = 4_850_000,
                Payroll = new Payroll { Status = PayrollStatus.Approved, Period = new AccountingPeriod { PeriodMonth = LastMonth } },
            },
            new()
            {
                EmployeeId = WorkerId, NetPay = 9_999_999,
                Payroll = new Payroll { Status = PayrollStatus.Draft, Period = new AccountingPeriod { PeriodMonth = ThisMonth } },
            },
        });
        SetupQueryable(new List<WorkAllocation>
        {
            Allocation(1, HandoverStatuses.Pending, 80),
            Allocation(2, HandoverStatuses.Pending, 40),
            Allocation(3, HandoverStatuses.Confirmed, 10),
        });
        SetupQueryable(new List<DefectRecord>
        {
            Defect(1, DashToday.AddDays(-3), 2),
            Defect(2, DashToday.AddDays(-30), 1),
        });
        _meals.GetMyMealAsync(WorkerId, DashToday, Arg.Any<CancellationToken>())
            .Returns(Result<MyMealDto>.Success(new MyMealDto(DashToday, true, true, false)));

        // Act
        var result = await Sut().GetWorkerAsync(CancellationToken.None);

        // Assert — EstPay > 0, Payslip, PendingAlloc=2, Defects7d=1, Meal
        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!;
        using (new AssertionScope())
        {
            dto.EstimatedToday.Should().BeGreaterThan(0);
            dto.EstimatedMonth.Should().BeGreaterThan(0);
            dto.EstimatedToday.Should().Be(186);
            dto.LatestPayslip.Should().Be(new HomePayslipDto(LastMonth, 4_850_000));
            dto.AllocationsPending.Should().Be(2);
            dto.Defects.Should().ContainSingle();
            (dto.MealHasAttendance, dto.MealWillEat, dto.MealLocked).Should().Be((true, true, false));
        }
    }

    [Fact]
    public async Task UTCID971_GetWorkerAsync_NotPieceRate_EstimatedPayIsNull()
    {
        // Arrange — worker không hưởng lương khoán (không có ngày sản lượng)
        User.EmployeeId.Returns(WorkerId);
        _pay.GetMyEstimatedPayAsync(WorkerId, Arg.Any<MyEstimatedPayQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<MyEstimatedPayDto>.Success(Pay()));
        SetupQueryable(new List<Payslip>());
        SetupQueryable(new List<WorkAllocation>());
        SetupQueryable(new List<DefectRecord>());

        // Act
        var result = await Sut().GetWorkerAsync(CancellationToken.None);

        // Assert — EstPay=null
        result.IsSuccess.Should().BeTrue();
        result.Data!.EstimatedToday.Should().BeNull();
        result.Data.EstimatedMonth.Should().BeNull();
    }

    [Fact]
    public async Task UTCID972_GetWorkerAsync_NoOutputToday_TodayIsZero()
    {
        // Arrange — có ngày khoán khác, không có sản lượng hôm nay
        User.EmployeeId.Returns(WorkerId);
        var yesterday = PayDay(DashToday.AddDays(-1), 100);
        _pay.GetMyEstimatedPayAsync(WorkerId, Arg.Any<MyEstimatedPayQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<MyEstimatedPayDto>.Success(Pay(yesterday)));
        SetupQueryable(new List<Payslip>());
        SetupQueryable(new List<WorkAllocation>());
        SetupQueryable(new List<DefectRecord>());

        // Act
        var result = await Sut().GetWorkerAsync(CancellationToken.None);

        // Assert — Today=0
        result.IsSuccess.Should().BeTrue();
        result.Data!.EstimatedToday.Should().Be(0);
    }

    [Fact]
    public async Task UTCID973_GetWorkerAsync_DefectOnDay7Counted_Day8Excluded()
    {
        // Arrange — defect đúng 7 ngày (2026-11-17) và 8 ngày (2026-11-16)
        User.EmployeeId.Returns(WorkerId);
        SetupQueryable(new List<Payslip>());
        SetupQueryable(new List<WorkAllocation>());
        SetupQueryable(new List<DefectRecord>
        {
            Defect(1, DashToday.AddDays(-7), 1),
            Defect(2, DashToday.AddDays(-8), 4),
        });

        // Act
        var result = await Sut().GetWorkerAsync(CancellationToken.None);

        // Assert — đếm defect 7 ngày, loại defect 8 ngày
        result.IsSuccess.Should().BeTrue();
        result.Data!.Defects.Should().ContainSingle()
            .Which.Meta.Should().Be(DashToday.AddDays(-7).ToString("dd/MM"));
    }

    [Fact]
    public async Task UTCID974_GetWorkerAsync_PayslipThrows_PayslipNullWithoutException()
    {
        // Arrange — mock nguồn phiếu lương ném exception; sản lượng và việc chờ vẫn có
        User.EmployeeId.Returns(WorkerId);
        _pay.GetMyEstimatedPayAsync(WorkerId, Arg.Any<MyEstimatedPayQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<MyEstimatedPayDto>.Success(Pay(PayDay(DashToday, 186))));
        SetupQueryable(new List<WorkAllocation> { Allocation(1, HandoverStatuses.Pending, 20), Allocation(2, HandoverStatuses.Pending, 10) });
        SetupQueryable(new List<DefectRecord>());
        ThrowOnQuery<Payslip>();

        // Act
        var act = async () => await Sut().GetWorkerAsync(CancellationToken.None);

        // Assert khóa spec: Payslip=null (fallback); không exception
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsSuccess.Should().BeTrue();
        result.Data!.LatestPayslip.Should().BeNull();
        result.Data.EstimatedToday.Should().BeGreaterThan(0);
        result.Data.AllocationsPending.Should().Be(2);
    }

    // ---------- Hỗ trợ ----------

    private void SeedNovemberCostAndPayroll(string payrollStatus)
    {
        SetupQueryable(new List<AccountingPeriod> { new() { PeriodId = 11, PeriodMonth = ThisMonth } });
        SetupQueryable(new List<MonthlyCost> { new() { MonthlyCostId = 1, PeriodId = 11 } });
        SetupQueryable(new List<Payroll> { new() { PayrollId = 1, PeriodId = 11, Status = payrollStatus } });
    }

    private void ThrowOnQuery<T>() where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns<IQueryable<T>>(_ => throw new InvalidOperationException(typeof(T).Name + " failed"));
        Uow.Repository<T>().Returns(repo);
    }

    private static OrderProgressDto Order(int id, string status) =>
        new(id, $"DH-{id}", "Khách", DashToday, 3, status, "Received", false, null, 100, 50, 50, 40, 20, 10, 0, 0, 0, 0);

    private static RequestListItemDto RequestItem(int id) =>
        new(id, $"DT-{id}", "MaterialSupplement", "PendingApproval", null, null, "TT", "CAT1", DashNow, null);

    private static CostSummaryDto Cost(string month, decimal grand) => new(
        1, month, false, 0, [], 0, [], [], 0, false, null, grand, new MealsReconciliationDto(0, null, false));

    private static Customer Customer(string name) => new() { CustomerName = name };

    private static Supplier Supplier(string name) => new() { SupplierName = name };

    private static WorkOrder WorkOrderOf(string orderCode) =>
        new() { Order = new CustomerOrder { OrderCode = orderCode } };

    private static IncomingCustomerMaterialDto CustomerLot(int id, string orderCode, string material, decimal outstanding) =>
        new(id, 5, orderCode, material, material, "", "cái", outstanding, 0, outstanding, null, 0);

    private static IncomingSupplierMaterialDto SupplierLot(int id, string poCode, string material, decimal outstanding) =>
        new(id, poCode, 5, "DH-5", "Minh Phát", null, 0, id, material, material, "", "m", outstanding, 0, outstanding, "ORDERED");

    private static VwOrderLineProgress Line(int orderId, int issued, int finished, int received, int scrap) => new()
    {
        OrderLineId = orderId, OrderId = orderId, OrderCode = $"DH-{orderId}", ProductCode = "AO", Color = "Đen", SizeCode = "M",
        IssuedToSewingQty = issued, FinishedQty = finished, FinishedReceivedQty = received, ScrapQty = scrap,
    };

    private static Request Ticket(int id, int createdBy, string status) => new()
    {
        RequestId = id, RequestCode = $"DT-{id}", RequestTypeCode = RequestTypes.SpareParts,
        Status = status, CreatedBy = createdBy, CreatedAt = DashNow,
    };

    private static MyEstimatedPayDayDto PayDay(DateOnly date, decimal pay) =>
        new(date, false, 10, 0, pay, 0, 0, pay);

    private static MyEstimatedPayDto Pay(params MyEstimatedPayDayDto[] days) =>
        new(new DateOnly(DashToday.Year, DashToday.Month, 1), DashToday, true, days,
            days.Sum(d => d.OutputQty), 0, days.Sum(d => d.EstimatedPay), 0, 0, days.Sum(d => d.EstimatedPay));

    private static WorkAllocation Allocation(int id, string status, int quantity) => new()
    {
        WorkAllocationId = id, WorkerId = WorkerId, Status = status, Quantity = quantity,
        Operation = new Operation { OperationName = "May cổ" },
        OrderLine = new OrderLine { Order = new CustomerOrder { OrderCode = "DH-5" } },
    };

    private static DefectRecord Defect(int id, DateOnly workDate, int quantity) => new()
    {
        DefectRecordId = id, ResponsibleWorkerId = WorkerId, Status = DefectStatuses.Recorded,
        WorkDate = workDate, DefectType = DefectTypes.Scrap, Quantity = quantity,
        Operation = new Operation { OperationName = "May cổ" },
    };
}
