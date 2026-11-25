using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>
/// ID động sau khi seed — test chỉ dùng thuộc tính này, tuyệt đối không hardcode 1, 2, 3…
/// </summary>
public sealed class TestContext
{
    // --- Roles (tra theo RoleCode) ---
    public Dictionary<string, Role> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);

    // --- Accounts / Employees (Minimum Test Account) ---
    public Account Admin01 { get; set; } = null!;
    public Account Admin02 { get; set; } = null!;
    public Account Manager01 { get; set; } = null!;
    public Account Accountant01 { get; set; } = null!;
    public Account Warehouse01 { get; set; } = null!;
    public Account Qaqc01 { get; set; } = null!;
    public Account Electrician01 { get; set; } = null!;
    public Account LeaderSewA { get; set; } = null!;
    public Account LeaderSewB { get; set; } = null!;
    public Account DeputySewA { get; set; } = null!;
    public Account WorkerSewA1 { get; set; } = null!;
    public Account WorkerSewA2 { get; set; } = null!;
    public Account WorkerSewB1 { get; set; } = null!;
    public Account WorkerCut01 { get; set; } = null!;
    public Account WorkerLocked { get; set; } = null!;
    public Account WorkerResigned { get; set; } = null!;

    // --- Teams ---
    public Team TeamOffice { get; set; } = null!;
    public Team TeamCut01 { get; set; } = null!;
    public Team TeamSewA { get; set; } = null!;
    public Team TeamSewB { get; set; } = null!;
    public Team TeamSewX { get; set; } = null!;

    // --- Catalog ---
    public Customer CustomerParent { get; set; } = null!;
    public Material MatFab01 { get; set; } = null!;
    public Material MatFab02 { get; set; } = null!;
    public Material MatTrimOld { get; set; } = null!;
    public Product Product001 { get; set; } = null!;
    public Product Product002 { get; set; } = null!;
    public ProductColor ColorBlack { get; set; } = null!;
    public ProductColor ColorWhite { get; set; } = null!;
    public ProductVariant VariantBlackS { get; set; } = null!;
    public ProductVariant VariantBlackM { get; set; } = null!;
    public ProductVariant VariantWhiteS { get; set; } = null!;
    public ProductVariant VariantWhiteM { get; set; } = null!;
    public ProductComponent PanelComponent { get; set; } = null!;
    public ProductComponent PanelComponentMax { get; set; } = null!;
    public ProductComponent TrimComponent { get; set; } = null!;

    // --- Operations / Piece rates ---
    public Operation Op10 { get; set; } = null!;
    public Operation Op20 { get; set; } = null!;
    public Operation Op30 { get; set; } = null!;
    public Operation Op40 { get; set; } = null!;
    public PieceRate DraftPieceRate { get; set; } = null!;
    public PieceRate PendingPieceRate { get; set; } = null!;
    public PieceRate ApprovedPieceRate { get; set; } = null!;
    public PieceRate RejectedPieceRate { get; set; } = null!;

    // --- Orders ---
    public CustomerOrder OrderNew { get; set; } = null!;
    public CustomerOrder OrderInProduction { get; set; } = null!;
    public CustomerOrder OrderInDelivery { get; set; } = null!;
    public CustomerOrder OrderPartiallyDelivered { get; set; } = null!;
    public CustomerOrder OrderDelivered { get; set; } = null!;
    public CustomerOrder OrderCompleted { get; set; } = null!;
    public CustomerOrder OrderStopped { get; set; } = null!;
    public CustomerOrder OrderCancelled { get; set; } = null!;
    public List<OrderLine> OrderProdLines { get; } = [];
    public List<OrderLineTeam> OrderLineTeams { get; } = [];
    public OrderPayment PaymentPartial { get; set; } = null!;
    public OrderPayment PaymentFull { get; set; } = null!;
    public ProductMarker MarkerNo1 { get; set; } = null!;
    public ProductMarker MarkerNo20 { get; set; } = null!;

    // --- Warehouse / cutting ---
    public WorkOrder WorkOrderOpen { get; set; } = null!;
    public WorkOrder WorkOrderCutting { get; set; } = null!;
    public WorkOrder WorkOrderDone { get; set; } = null!;
    public WorkOrder WorkOrderCancelled { get; set; } = null!;
    public MaterialReceipt ReceiptPending { get; set; } = null!;
    public MaterialReceipt ReceiptConfirmed { get; set; } = null!;
    public MaterialReceipt ReceiptReturned { get; set; } = null!;
    public MaterialIssue IssuePending { get; set; } = null!;
    public MaterialIssue IssueReceived { get; set; } = null!;
    public MaterialIssue IssueRejected { get; set; } = null!;
    public StockBalance StockFab01 { get; set; } = null!;
    public StockBalance StockFab02 { get; set; } = null!;
    public Stocktake StocktakePending { get; set; } = null!;
    public Stocktake StocktakeApproved { get; set; } = null!;
    public Stocktake StocktakeReturned { get; set; } = null!;

    // --- Production floor ---
    public WorkAllocation HandoverPending { get; set; } = null!;
    public WorkAllocation HandoverConfirmed { get; set; } = null!;
    public WorkAllocation HandoverRejected { get; set; } = null!;
    public WorkAllocation HandoverCancelledAsRejected { get; set; } = null!;
    public WorkAllocation MovementHandoverA2 { get; set; } = null!;
    public WorkAllocation MovementRetrievalA2 { get; set; } = null!;
    public OutputRecord OutputSaved { get; set; } = null!;
    public OutputRecord OutputLocked { get; set; } = null!;
    public DefectRecord DefectRework { get; set; } = null!;
    public DefectRecord DefectScrap { get; set; } = null!;
    public DefectRecord DefectOutputErr { get; set; } = null!;
    public DefectRecord DefectLocked { get; set; } = null!;

    // --- Finance ---
    public AccountingPeriod PeriodCur { get; set; } = null!;
    public AccountingPeriod PeriodPrev { get; set; } = null!;
    public AccountingPeriod PeriodPendingPayroll { get; set; } = null!;
    public AccountingPeriod PeriodRejectedPayroll { get; set; } = null!;
    public Payroll PayrollDraft { get; set; } = null!;
    public Payroll PayrollApproved { get; set; } = null!;
    public Payroll PayrollPending { get; set; } = null!;
    public Payroll PayrollRejected { get; set; } = null!;
    public Payslip PayslipWorkerSewA1 { get; set; } = null!;
    public Payslip PayslipWorkerSewA2 { get; set; } = null!;
    public List<MonthlyCost> MonthlyCostsCur { get; } = [];
    public List<MonthlyCost> MonthlyCostsPrev { get; } = [];

    // --- Attendance / requests / misc ---
    public AttendanceSyncSession SyncOverdue { get; set; } = null!;
    public DailyTimesheet TimesheetMissingPunch { get; set; } = null!;
    public List<Request> Requests { get; } = [];
    public PurchaseOrder PurchasePendingFinalisation { get; set; } = null!;
    public PurchaseOrder PurchaseUnpaid { get; set; } = null!;
    public Vehicle VehicleBusy { get; set; } = null!;
    public Vehicle VehicleIdle { get; set; } = null!;
    public VehicleTrip TripStarted { get; set; } = null!;
    public VehicleTrip TripCompleted { get; set; } = null!;
    public SystemSetting FuelNormSetting { get; set; } = null!;
    public SystemSetting AttendanceAlertSetting { get; set; } = null!;

    /// <summary>Alias ví dụ Report: context.DraftRecord ≈ DraftPieceRate.</summary>
    public PieceRate DraftRecord => DraftPieceRate;

    public const string TestPassword = "NxTest@2026";
}
