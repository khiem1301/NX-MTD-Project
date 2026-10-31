using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data;

public partial class NxMtdDbContext : DbContext
{
    public NxMtdDbContext(DbContextOptions<NxMtdDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AccountingPeriod> AccountingPeriods { get; set; }

    public virtual DbSet<Announcement> Announcements { get; set; }

    public virtual DbSet<AnnouncementTarget> AnnouncementTargets { get; set; }

    public virtual DbSet<Attachment> Attachments { get; set; }

    public virtual DbSet<AttendanceAdjustment> AttendanceAdjustments { get; set; }

    public virtual DbSet<AttendanceLog> AttendanceLogs { get; set; }

    public virtual DbSet<AttendanceSyncSession> AttendanceSyncSessions { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BreakEvenResult> BreakEvenResults { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerOrder> CustomerOrders { get; set; }

    public virtual DbSet<CutHandover> CutHandovers { get; set; }

    public virtual DbSet<CutHandoverLine> CutHandoverLines { get; set; }

    public virtual DbSet<CuttingLay> CuttingLays { get; set; }

    public virtual DbSet<CuttingLayLine> CuttingLayLines { get; set; }

    public virtual DbSet<DailyMeal> DailyMeals { get; set; }

    public virtual DbSet<DailyTimesheet> DailyTimesheets { get; set; }

    public virtual DbSet<DefectRecord> DefectRecords { get; set; }

    public virtual DbSet<DispatchNote> DispatchNotes { get; set; }

    public virtual DbSet<DispatchNoteLine> DispatchNoteLines { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<FgHandover> FgHandovers { get; set; }

    public virtual DbSet<FgHandoverLine> FgHandoverLines { get; set; }

    public virtual DbSet<GradeEvaluation> GradeEvaluations { get; set; }

    public virtual DbSet<GradeLevel> GradeLevels { get; set; }

    public virtual DbSet<GradeLevelCode> GradeLevelCodes { get; set; }

    public virtual DbSet<KpiTarget> KpiTargets { get; set; }

    public virtual DbSet<Material> Materials { get; set; }

    public virtual DbSet<MaterialIssue> MaterialIssues { get; set; }

    public virtual DbSet<MaterialIssueLine> MaterialIssueLines { get; set; }

    public virtual DbSet<MaterialReceipt> MaterialReceipts { get; set; }

    public virtual DbSet<MaterialReceiptLine> MaterialReceiptLines { get; set; }

    public virtual DbSet<MaterialReturn> MaterialReturns { get; set; }

    public virtual DbSet<MaterialReturnLine> MaterialReturnLines { get; set; }

    public virtual DbSet<MonthlyCost> MonthlyCosts { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Operation> Operations { get; set; }

    public virtual DbSet<OperationAssignment> OperationAssignments { get; set; }

    public virtual DbSet<OperationComponent> OperationComponents { get; set; }

    public virtual DbSet<OperationGroup> OperationGroups { get; set; }

    public virtual DbSet<OrderLine> OrderLines { get; set; }

    public virtual DbSet<OrderMaterial> OrderMaterials { get; set; }

    public virtual DbSet<OutputRecord> OutputRecords { get; set; }

    public virtual DbSet<Payroll> Payrolls { get; set; }

    public virtual DbSet<Payslip> Payslips { get; set; }

    public virtual DbSet<PayslipItem> PayslipItems { get; set; }

    public virtual DbSet<PieceRate> PieceRates { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductComponent> ProductComponents { get; set; }

    public virtual DbSet<ProductComponentMaterial> ProductComponentMaterials { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }

    public virtual DbSet<QcBatch> QcBatches { get; set; }

    public virtual DbSet<Request> Requests { get; set; }

    public virtual DbSet<RequestLine> RequestLines { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<StockBalance> StockBalances { get; set; }

    public virtual DbSet<StockTransaction> StockTransactions { get; set; }

    public virtual DbSet<Stocktake> Stocktakes { get; set; }

    public virtual DbSet<StocktakeLine> StocktakeLines { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamMealConfirmation> TeamMealConfirmations { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleTrip> VehicleTrips { get; set; }

    public virtual DbSet<VwDailyMealTotal> VwDailyMealTotals { get; set; }

    public virtual DbSet<VwKpiDailyResult> VwKpiDailyResults { get; set; }

    public virtual DbSet<VwMaterialStock> VwMaterialStocks { get; set; }

    public virtual DbSet<VwMonthlyRevenue> VwMonthlyRevenues { get; set; }

    public virtual DbSet<VwOperationAllocationProgress> VwOperationAllocationProgresses { get; set; }

    public virtual DbSet<VwOrderLineProgress> VwOrderLineProgresses { get; set; }

    public virtual DbSet<VwOrderMaterialDelivery> VwOrderMaterialDeliveries { get; set; }

    public virtual DbSet<VwPayrollExportDatum> VwPayrollExportData { get; set; }

    public virtual DbSet<VwProductLastSizeRatio> VwProductLastSizeRatios { get; set; }

    public virtual DbSet<VwPurchaseOrderDelivery> VwPurchaseOrderDeliveries { get; set; }

    public virtual DbSet<VwPurchaseOrderLineStatus> VwPurchaseOrderLineStatuses { get; set; }

    public virtual DbSet<VwRecutFabricNeed> VwRecutFabricNeeds { get; set; }

    public virtual DbSet<VwStockBalance> VwStockBalances { get; set; }

    public virtual DbSet<VwTeamActingLeader> VwTeamActingLeaders { get; set; }

    public virtual DbSet<VwWorkOrderLineProgress> VwWorkOrderLineProgresses { get; set; }

    public virtual DbSet<VwWorkerDailyEstimatedPay> VwWorkerDailyEstimatedPays { get; set; }

    public virtual DbSet<VwWorkerOutputCap> VwWorkerOutputCaps { get; set; }

    public virtual DbSet<WorkAllocation> WorkAllocations { get; set; }

    public virtual DbSet<WorkOrder> WorkOrders { get; set; }

    public virtual DbSet<WorkOrderLine> WorkOrderLines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_100_CI_AS");

        // Cấu hình từng entity nằm ở Data/Configurations (IEntityTypeConfiguration<T>).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NxMtdDbContext).Assembly);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
