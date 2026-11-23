using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-170: số liệu trang chủ theo vai trò.</summary>
public class HomeDashboardServiceTests : ProductionTestBase
{
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
        SetupQueryable(new List<PieceRate>
        {
            new() { PieceRateId = 1, Status = PieceRateStatuses.PendingApproval },
            new() { PieceRateId = 2, Status = PieceRateStatuses.PendingApproval },
            new() { PieceRateId = 3, Status = PieceRateStatuses.Approved },
        });
        SetupQueryable(new List<Payroll> { new() { PayrollId = 1, Status = PayrollStatus.PendingApproval } });
        SetupQueryable(new List<MaterialReceipt>
        {
            new() { MaterialReceiptId = 1, Status = ReceiptStatuses.Pending },
            new() { MaterialReceiptId = 2, Status = ReceiptStatuses.Confirmed },
        });
        SetupQueryable(new List<VwTeamActingLeader>
        {
            new() { TeamId = 2, TeamCode = "MAY2", IsActingLeader = true },
            new() { TeamId = 2, TeamCode = "MAY2", IsActingLeader = true },   // 2 tổ phó cùng tổ
            new() { TeamId = 1, TeamCode = "MAY1", IsActingLeader = false },
        });
        _progress.GetOverviewAsync(false, Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<OrderProgressDto>>.Success([Order(1, "InProduction"), Order(2, "New"), Order(3, "PartiallyDelivered")]));
        _finance.GetRevenueRowsAsync("2026-10", null, null, Arg.Any<CancellationToken>())
            .Returns([new VwMonthlyRevenue { Revenue = 700 }, new VwMonthlyRevenue { Revenue = 300 }]);
        _finance.GetRevenueRowsAsync("2026-09", null, null, Arg.Any<CancellationToken>()).Returns([]);
        _costs.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CostSummaryDto>.Failure(ErrorCodes.CommonNotFound, "x"));   // chưa nhập chi phí tháng nào
        _requests.ListInboxAsync(Arg.Any<RequestQuery>(), Arg.Any<CancellationToken>()).Returns(
            Result<PagedResult<RequestListItemDto>>.Success(new PagedResult<RequestListItemDto>(
                [new RequestListItemDto(9, "DT-9", "MaterialSupplement", "PendingApproval", null, null, "TTMAY", "MAY1", Now, null)], 1, 5, 7)));
    }

    private static OrderProgressDto Order(int id, string status) =>
        new(id, $"DH-{id}", "Khách", Today, 3, status, "Received", false, null, 100, 50, 50, 40, 20, 10, 0, 0, 0, 0);

    private HomeDashboardService Sut() =>
        new(Uow, Clock, User, _progress, _costs, _finance, _requests, _attendance, _incoming, _pay, _meals);

    // ---------- Giám đốc ----------

    [Fact]
    public async Task GetAdmin_CountsPendingApprovalsAndRunningOrders()
    {
        var dto = (await Sut().GetAdminAsync(default)).Data!;

        dto.PieceRatesPending.Should().Be(2);
        dto.PayrollsPending.Should().Be(1);
        dto.OrdersRunning.Should().Be(2);   // bỏ đơn mới chờ NVL
        dto.Orders.Select(o => o.OrderId).Should().Equal(1, 3);
    }

    [Fact]
    public async Task GetAdmin_RevenueAndCostCompareWithLastMonth_CostNullWhenNotEntered()
    {
        var dto = (await Sut().GetAdminAsync(default)).Data!;

        dto.Revenue.Should().Be(new HomeMoneyDto(1000, 0));
        dto.Cost.ThisMonth.Should().BeNull();
    }

    // ---------- Quản đốc ----------

    [Fact]
    public async Task GetManager_CountsPendingReceiptsRequestsAndAbsentLeaderTeams()
    {
        var dto = (await Sut().GetManagerAsync(default)).Data!;

        dto.ReceiptsPending.Should().Be(1);
        dto.RequestsPending.Should().Be(7);
        dto.Requests.Should().ContainSingle(r => r.RequestCode == "DT-9");
        dto.LeaderAbsentTeams.Should().Equal("MAY2");
        dto.Orders.Should().HaveCount(3);   // gồm cả đơn chờ NVL
        await _requests.Received().ListInboxAsync(
            Arg.Is<RequestQuery>(q => q.Status == "PendingApproval" && q.PageSize == 5), Arg.Any<CancellationToken>());
    }

    // ---------- Kế toán ----------

    [Fact]
    public async Task GetAccountant_CountsGapsUnpaidOrdersAndMonthStatus()
    {
        var op1 = new Operation { OperationId = 1, ProductId = 10, Status = OperationStatuses.Active };
        var op2 = new Operation { OperationId = 2, ProductId = 11, Status = OperationStatuses.Active };
        SetupQueryable(new List<Operation> { op1, op2 });
        SetupQueryable(new List<PieceRate>
        {
            new() { PieceRateId = 1, OperationId = 2, Status = PieceRateStatuses.Approved, Operation = op2 },
            new() { PieceRateId = 2, OperationId = 2, Status = PieceRateStatuses.Rejected, Operation = op2 },
        });
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = 1, OrderCode = "DH-1", Status = OrderStatuses.InProduction, Customer = new Customer { CustomerName = "A" } },
            new() { OrderId = 2, OrderCode = "DH-2", Status = OrderStatuses.InProduction, FinalAmount = 5, Customer = new Customer { CustomerName = "B" } },
            new() { OrderId = 3, OrderCode = "DH-3", Status = OrderStatuses.Cancelled, Customer = new Customer { CustomerName = "C" } },
        });
        SetupQueryable(new List<PurchaseOrder>
        {
            new() { PurchaseOrderId = 1, PoCode = "PO-1", GoodsStatus = PurchaseGoodsStatuses.Received, PaymentStatus = PurchaseOrderStatuses.Unpaid },
            new() { PurchaseOrderId = 2, PoCode = "PO-2", GoodsStatus = PurchaseGoodsStatuses.Received, PaymentStatus = PurchasePaymentStatuses.PartiallyPaid },
            new() { PurchaseOrderId = 3, PoCode = "PO-3", GoodsStatus = PurchaseGoodsStatuses.Received, PaymentStatus = PurchasePaymentStatuses.Paid },
        });
        SetupQueryable(new List<AccountingPeriod> { new() { PeriodId = 1, PeriodMonth = "2026-10" } });
        SetupQueryable(new List<MonthlyCost> { new() { MonthlyCostId = 1, PeriodId = 1 } });
        SetupQueryable(new List<Payroll> { new() { PayrollId = 1, PeriodId = 1, Status = PayrollStatus.Draft } });
        _attendance.GetSummaryAsync(Arg.Any<TimesheetQuery>(), Arg.Any<TimesheetScope>(), Arg.Any<CancellationToken>())
            .Returns(Result<TimesheetSummaryDto>.Success(new TimesheetSummaryDto(10, 100, 5, 18)));

        var dto = (await Sut().GetAccountantAsync(default)).Data!;

        dto.MissingPunch.Should().Be(18);
        dto.PieceRatesMissing.Should().Be(1);   // chỉ mã hàng 10 chưa có đơn giá
        dto.PieceRatesRejected.Should().Be(1);
        dto.OrdersNotSet.Should().Be(1);        // bỏ đơn đã chốt và đơn đã hủy
        dto.Orders.Should().ContainSingle(o => o.Title == "DH-1");
        dto.PurchasesUnpaid.Should().Be(2);
        dto.CostEntered.Should().BeTrue();
        dto.PayrollStatus.Should().Be("Draft");
    }

    // ---------- Thủ kho ----------

    [Fact]
    public async Task GetWarehouse_CountsIncomingUnissuedWorkOrdersAndPendingHandovers()
    {
        _incoming.GetAsync(Arg.Any<CancellationToken>()).Returns(Result<IncomingMaterialsDto>.Success(new IncomingMaterialsDto(
            [new IncomingCustomerMaterialDto(1, 5, "DH-5", "NHAN", "Nhãn", "", "cái", 100, 0, 100, null, 0)],
            [new IncomingSupplierMaterialDto(2, "PO-2", 5, "DH-5", "Minh Phát", null, 0, 3, "VAI", "Vải", "", "m", 180, 0, 180, "ORDERED")],
            [])));
        SetupQueryable(new List<WorkOrder>
        {
            new() { WorkOrderId = 1, Status = WorkOrderStatuses.Open },
            new() { WorkOrderId = 2, Status = WorkOrderStatuses.Cutting },
            new() { WorkOrderId = 3, Status = WorkOrderStatuses.Completed },
        });
        SetupQueryable(new List<MaterialIssue> { new() { MaterialIssueId = 1, WorkOrderId = 2, Status = IssueStatuses.Received } });
        SetupQueryable(new List<CutHandover>
        {
            new() { CutHandoverId = 1, HandoverCode = "BG-1", Status = HandoverStatuses.Pending,
                    WorkOrder = new WorkOrder { Order = new CustomerOrder { OrderCode = "DH-5" } } },
        });
        SetupQueryable(new List<FgHandover>
        {
            new() { FgHandoverId = 1, HandoverCode = "TP-1", Status = HandoverStatuses.Pending, Order = new CustomerOrder { OrderCode = "DH-5" } },
        });
        SetupQueryable(new List<MaterialReturn>
        {
            new() { MaterialReturnId = 1, ReturnCode = "TR-1", Status = HandoverStatuses.Pending, ReturningTeam = new Team { TeamCode = "CAT1" } },
        });

        var dto = (await Sut().GetWarehouseAsync(default)).Data!;

        dto.Incoming.Should().Be(2);
        dto.IncomingItems.Select(i => i.Tag).Should().Equal("NCC", "Khách cấp");
        dto.WorkOrdersNoIssue.Should().Be(1);   // lệnh 2 đã có phiếu cấp, lệnh 3 đã xong
        dto.RequestsToIssue.Should().Be(7);
        (dto.CutHandovers, dto.FgHandovers, dto.Returns).Should().Be((1, 1, 1));
        dto.PendingItems.Should().HaveCount(3);
    }

    // ---------- Tổ trưởng / tổ phó ----------

    [Fact]
    public async Task GetTeamLeader_Cutting_CountsOwnTeamOnly_AndDoesNotDoubleCountRepeatedLines()
    {
        User.TeamId.Returns(7);
        User.TeamTypeCode.Returns(TeamTypes.Cutting);
        SetupQueryable(new List<MaterialIssue>
        {
            new() { MaterialIssueId = 1, ReceivingTeamId = 7, Status = IssueStatuses.PendingReceipt, WarehouseType = WarehouseTypes.Material },
            new() { MaterialIssueId = 2, ReceivingTeamId = 8, Status = IssueStatuses.PendingReceipt, WarehouseType = WarehouseTypes.Material },
            new() { MaterialIssueId = 3, WorkOrderId = 3, ReceivingTeamId = 7, Status = IssueStatuses.Received },
        });
        SetupQueryable(new List<VwWorkOrderLineProgress>
        {
            // cùng một dòng đơn lặp theo hai loại vải: kế hoạch / đã cắt chỉ tính một lần
            new() { WorkOrderId = 1, OrderLineId = 10, CuttingTeamId = 7, WorkOrderStatus = WorkOrderStatuses.Open, PlannedQty = 100, CutQty = 60, HandedToWarehouseQty = 20 },
            new() { WorkOrderId = 1, OrderLineId = 10, CuttingTeamId = 7, WorkOrderStatus = WorkOrderStatuses.Open, PlannedQty = 100, CutQty = 60, HandedToWarehouseQty = 20 },
            new() { WorkOrderId = 9, OrderLineId = 90, CuttingTeamId = 8, WorkOrderStatus = WorkOrderStatuses.Open, PlannedQty = 500, CutQty = 500, HandedToWarehouseQty = 0 },
        });
        SetupQueryable(new List<WorkOrder>
        {
            new() { WorkOrderId = 1, WorkOrderCode = "LC-1", CuttingTeamId = 7, Status = WorkOrderStatuses.Open, Order = new CustomerOrder { OrderCode = "DH-5" } },
            new() { WorkOrderId = 3, WorkOrderCode = "LC-3", CuttingTeamId = 7, Status = WorkOrderStatuses.Completed, Order = new CustomerOrder { OrderCode = "DH-5" } },
        });
        SetupQueryable(new List<MaterialReturn>());

        var dto = (await Sut().GetTeamLeaderAsync(default)).Data!;

        dto.TeamType.Should().Be(TeamTypes.Cutting);
        dto.Sewing.Should().BeNull();
        dto.Cutting!.IssuesPending.Should().Be(1);
        dto.Cutting.CutNotHanded.Should().Be(40);
        dto.Cutting.LeftoverNotReturned.Should().Be(1);   // LC-3 xong, đã nhận vải, chưa trả
        dto.Cutting.WorkOrders.Should().ContainSingle().Which.Should().Be(new HomeWorkOrderDto(1, "LC-1", "DH-5", "Open", 100, 60));
    }

    [Fact]
    public async Task GetTeamLeader_Sewing_ReportsKpiTodayAndBlockedBtpIssues()
    {
        User.TeamId.Returns(7);
        User.TeamTypeCode.Returns(TeamTypes.Sewing);
        SetupQueryable(new List<VwKpiDailyResult>
        {
            new() { SewingTeamId = 7, WorkDate = Today, DailyTarget = 100, FinishedQty = 40 },
            new() { SewingTeamId = 7, WorkDate = Today.AddDays(-1), DailyTarget = 100, FinishedQty = 90 },
            new() { SewingTeamId = 8, WorkDate = Today, DailyTarget = 999, FinishedQty = 999 },
        });
        SetupQueryable(new List<MaterialIssue>
        {
            new() { MaterialIssueId = 1, IssueCode = "PC-1", OrderId = 5, ReceivingTeamId = 7, Status = IssueStatuses.PendingReceipt, WarehouseType = WarehouseTypes.SemiFinished },
        });
        SetupQueryable(new List<MaterialIssueLine> { new() { MaterialIssueId = 1, OrderLineId = 50 } });
        SetupQueryable(new List<OrderLine> { new() { OrderLineId = 50, OrderId = 5, ProductId = 10 } });
        SetupQueryable(new List<Operation>
        {
            new() { OperationId = 1, ProductId = 10, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing },
            new() { OperationId = 2, ProductId = 10, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing },
        });
        SetupQueryable(new List<OperationAssignment>
        {
            new() { OperationId = 1, OrderId = 5, SewingTeamId = 7, Worker = new Employee { Status = RecordStatus.Active, TeamId = 7 } },
        });
        SetupQueryable(new List<WorkAllocation>
        {
            new() { WorkAllocationId = 1, SewingTeamId = 7, Status = HandoverStatuses.Pending },
            new() { WorkAllocationId = 2, SewingTeamId = 7, Status = HandoverStatuses.Confirmed },
        });
        _meals.GetTeamSuggestionAsync(7, Today, Arg.Any<CancellationToken>()).Returns(Result<TeamMealSuggestionDto>.Success(
            new TeamMealSuggestionDto(Today, 7, "MAY1", 18, 20, null, null, false)));

        var dto = (await Sut().GetTeamLeaderAsync(default)).Data!;

        dto.Cutting.Should().BeNull();
        dto.Sewing!.KpiTarget.Should().Be(100);
        dto.Sewing.KpiActual.Should().Be(40);
        dto.Sewing.BtpPending.Should().Be(1);
        dto.Sewing.UnassignedOperations.Should().Be(1);   // công đoạn 2 chưa phân công
        dto.Sewing.BtpIssues.Should().ContainSingle().Which.TagColor.Should().Be("red");
        dto.Sewing.AllocationsPending.Should().Be(1);
        (dto.Sewing.MealSuggested, dto.Sewing.MealConfirmed).Should().Be((18, 20));
    }

    // ---------- QA/QC ----------

    [Fact]
    public async Task GetQaqc_SumsOnLineAndNotReceivedOnRunningOrdersOnly()
    {
        SetupQueryable(new List<CustomerOrder>
        {
            new() { OrderId = 1, Status = OrderStatuses.InProduction },
            new() { OrderId = 2, Status = OrderStatuses.Delivered },
        });
        SetupQueryable(new List<VwOrderLineProgress>
        {
            new() { OrderLineId = 1, OrderId = 1, OrderCode = "DH-1", ProductCode = "AO", Color = "Đen", SizeCode = "M",
                    IssuedToSewingQty = 100, FinishedQty = 60, FinishedReceivedQty = 10, ScrapQty = 5 },
            new() { OrderLineId = 2, OrderId = 2, OrderCode = "DH-2", ProductCode = "AO", Color = "Đen", SizeCode = "M",
                    IssuedToSewingQty = 500, FinishedQty = 0, FinishedReceivedQty = 0, ScrapQty = 0 },
        });
        SetupQueryable(new List<FgHandover>
        {
            new() { FgHandoverId = 1, HandoverCode = "TP-1", Status = HandoverStatuses.Pending, Order = new CustomerOrder { OrderCode = "DH-1" } },
        });

        var dto = (await Sut().GetQaqcAsync(default)).Data!;

        dto.OnLineQty.Should().Be(35);          // 100 − 60 − 5, bỏ đơn đã giao
        dto.NotReceivedQty.Should().Be(50);     // 60 − 10
        dto.HandoversPending.Should().Be(1);
        dto.Lines.Should().ContainSingle().Which.Tag.Should().Be("Chờ kiểm");
    }

    // ---------- Công nhân ----------

    [Fact]
    public async Task GetWorker_ReportsEstimatedPayPayslipPendingWorkAndRecentDefects()
    {
        User.EmployeeId.Returns(42);
        _pay.GetMyEstimatedPayAsync(42, Arg.Any<MyEstimatedPayQuery>(), Arg.Any<CancellationToken>()).Returns(
            Result<MyEstimatedPayDto>.Success(new MyEstimatedPayDto(Today, Today, true,
                [new MyEstimatedPayDayDto(Today.AddDays(-1), false, 10, 0, 100, 0, 0, 100), new MyEstimatedPayDayDto(Today, false, 20, 0, 186, 0, 0, 186)],
                30, 0, 286, 0, 0, 286)));
        SetupQueryable(new List<Payslip>
        {
            new() { EmployeeId = 42, NetPay = 4_850_000, Payroll = new Payroll { Status = PayrollStatus.Approved, Period = new AccountingPeriod { PeriodMonth = "2026-09" } } },
            new() { EmployeeId = 42, NetPay = 111, Payroll = new Payroll { Status = PayrollStatus.Draft, Period = new AccountingPeriod { PeriodMonth = "2026-10" } } },
        });
        SetupQueryable(new List<WorkAllocation>
        {
            new() { WorkAllocationId = 1, WorkerId = 42, Status = HandoverStatuses.Pending, Quantity = 80,
                    Operation = new Operation { OperationName = "May cổ" }, OrderLine = new OrderLine { Order = new CustomerOrder { OrderCode = "DH-5" } } },
        });
        SetupQueryable(new List<DefectRecord>
        {
            new() { DefectRecordId = 1, ResponsibleWorkerId = 42, Status = DefectStatuses.Recorded, WorkDate = Today, DefectType = "SCRAP", Quantity = 2,
                    Operation = new Operation { OperationName = "May cổ" } },
            new() { DefectRecordId = 2, ResponsibleWorkerId = 42, Status = DefectStatuses.Recorded, WorkDate = Today.AddDays(-30), DefectType = "SCRAP", Quantity = 1,
                    Operation = new Operation { OperationName = "May cổ" } },
        });
        _meals.GetMyMealAsync(42, Today, Arg.Any<CancellationToken>())
            .Returns(Result<MyMealDto>.Success(new MyMealDto(Today, true, true, false)));

        var dto = (await Sut().GetWorkerAsync(default)).Data!;

        dto.EstimatedToday.Should().Be(186);
        dto.EstimatedMonth.Should().Be(286);
        dto.LatestPayslip.Should().Be(new HomePayslipDto("2026-09", 4_850_000));   // chỉ bảng lương đã duyệt
        dto.AllocationsPending.Should().Be(1);
        dto.Defects.Should().ContainSingle();   // bỏ phiếu quá 7 ngày
        (dto.MealHasAttendance, dto.MealWillEat, dto.MealLocked).Should().Be((true, true, false));
    }

    [Fact]
    public async Task GetWorker_NoPieceWork_EstimatedPayIsNull()
    {
        User.EmployeeId.Returns(43);
        _pay.GetMyEstimatedPayAsync(43, Arg.Any<MyEstimatedPayQuery>(), Arg.Any<CancellationToken>()).Returns(
            Result<MyEstimatedPayDto>.Success(new MyEstimatedPayDto(Today, Today, true, [], 0, 0, 0, 0, 0, 0)));
        SetupQueryable(new List<Payslip>());
        SetupQueryable(new List<WorkAllocation>());
        SetupQueryable(new List<DefectRecord>());
        _meals.GetMyMealAsync(43, Today, Arg.Any<CancellationToken>())
            .Returns(Result<MyMealDto>.Success(new MyMealDto(Today, false, false, false)));

        var dto = (await Sut().GetWorkerAsync(default)).Data!;

        dto.EstimatedToday.Should().BeNull();
        dto.EstimatedMonth.Should().BeNull();
        dto.LatestPayslip.Should().BeNull();
    }

    // ---------- Cơ điện ----------

    [Fact]
    public async Task GetElectrician_ListsOnlyOwnOpenRequests()
    {
        User.EmployeeId.Returns(60);
        SetupQueryable(new List<Request>
        {
            new() { RequestId = 1, RequestCode = "DT-1", RequestTypeCode = "SPARE_PARTS", Status = RequestStatuses.PendingProcessing, CreatedBy = 60, CreatedAt = Now },
            new() { RequestId = 2, RequestCode = "DT-2", RequestTypeCode = "SPARE_PARTS", Status = RequestStatuses.Completed, CreatedBy = 60, CreatedAt = Now },
            new() { RequestId = 3, RequestCode = "DT-3", RequestTypeCode = "SPARE_PARTS", Status = RequestStatuses.PendingApproval, CreatedBy = 61, CreatedAt = Now },
        });

        var dto = (await Sut().GetElectricianAsync(default)).Data!;

        dto.Tickets.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new HomeTicketDto(1, "DT-1", "SpareParts", "PendingProcessing", null, Now));
    }
}
