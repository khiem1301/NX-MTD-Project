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

    public virtual DbSet<VwOrderMaterialNorm> VwOrderMaterialNorms { get; set; }

    public virtual DbSet<VwPayrollExportDatum> VwPayrollExportData { get; set; }

    public virtual DbSet<VwProductLastSizeRatio> VwProductLastSizeRatios { get; set; }

    public virtual DbSet<VwPurchaseOrderDelivery> VwPurchaseOrderDeliveries { get; set; }

    public virtual DbSet<VwPurchaseOrderLineStatus> VwPurchaseOrderLineStatuses { get; set; }

    public virtual DbSet<VwStockBalance> VwStockBalances { get; set; }

    public virtual DbSet<VwTeamActingLeader> VwTeamActingLeaders { get; set; }

    public virtual DbSet<VwTeamPieceHolding> VwTeamPieceHoldings { get; set; }

    public virtual DbSet<VwWorkOrderFabricNeed> VwWorkOrderFabricNeeds { get; set; }

    public virtual DbSet<VwWorkOrderLineProgress> VwWorkOrderLineProgresses { get; set; }

    public virtual DbSet<VwWorkerDailyEstimatedPay> VwWorkerDailyEstimatedPays { get; set; }

    public virtual DbSet<VwWorkerOutputCap> VwWorkerOutputCaps { get; set; }

    public virtual DbSet<WorkAllocation> WorkAllocations { get; set; }

    public virtual DbSet<WorkOrder> WorkOrders { get; set; }

    public virtual DbSet<WorkOrderLine> WorkOrderLines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_100_CI_AS");

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Account", tb => tb.HasComment("Tài khoản đăng nhập (1–1 nhân viên)"));

            entity.HasIndex(e => e.EmployeeId, "UQ_Account_Employee").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.IsTemporaryPassword).HasDefaultValue(true);
            entity.Property(e => e.LockReason).HasMaxLength(300);
            entity.Property(e => e.LockedAt).HasPrecision(0);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.SecurityStamp).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.Employee).WithOne(p => p.Account)
                .HasForeignKey<Account>(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Account_Employee");
        });

        modelBuilder.Entity<AccountingPeriod>(entity =>
        {
            entity.HasKey(e => e.PeriodId);

            entity.ToTable("AccountingPeriod", tb => tb.HasComment("Kỳ kế toán (tháng), khóa / mở khóa"));

            entity.HasIndex(e => e.PeriodMonth, "UQ_AccountingPeriod").IsUnique();

            entity.Property(e => e.LockedAt).HasPrecision(0);
            entity.Property(e => e.PeriodMonth)
                .HasMaxLength(7)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("OPEN");
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcement", tb => tb.HasComment("Thông báo nhanh"));

            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.Priority)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("NORMAL");
            entity.Property(e => e.SentAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.TargetScope)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Sender).WithMany(p => p.Announcements)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Announcement_Sender");
        });

        modelBuilder.Entity<AnnouncementTarget>(entity =>
        {
            entity.ToTable("AnnouncementTarget", tb => tb.HasComment("Phạm vi nhận thông báo nhanh"));

            entity.HasOne(d => d.Announcement).WithMany(p => p.AnnouncementTargets)
                .HasForeignKey(d => d.AnnouncementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AnnTarget_Announcement");

            entity.HasOne(d => d.Role).WithMany(p => p.AnnouncementTargets)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_AnnTarget_Role");

            entity.HasOne(d => d.Team).WithMany(p => p.AnnouncementTargets)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_AnnTarget_Team");
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("Attachment", tb => tb.HasComment("Tệp / ảnh đính kèm"));

            entity.HasIndex(e => new { e.OwnerType, e.OwnerId }, "IX_Attachment_Owner");

            entity.Property(e => e.ContentType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.IsCurrent).HasDefaultValue(true);
            entity.Property(e => e.OwnerType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Purpose)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PHOTO");
            entity.Property(e => e.UploadedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        });

        modelBuilder.Entity<AttendanceAdjustment>(entity =>
        {
            entity.ToTable("AttendanceAdjustment", tb =>
                {
                    tb.HasComment("Chấm hộ của Kế toán");
                    tb.HasTrigger("trg_AttendanceAdjustment_PeriodLock");
                });

            entity.HasIndex(e => new { e.EmployeeId, e.WorkDate }, "UQ_AA").IsUnique();

            entity.Property(e => e.AdjustmentType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CheckIn).HasPrecision(0);
            entity.Property(e => e.CheckOut).HasPrecision(0);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RecordedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");

            entity.HasOne(d => d.Employee).WithMany(p => p.AttendanceAdjustments)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AA_Employee");

            entity.HasOne(d => d.Request).WithMany(p => p.AttendanceAdjustments)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("FK_AA_Request");
        });

        modelBuilder.Entity<AttendanceLog>(entity =>
        {
            entity.ToTable("AttendanceLog", tb =>
                {
                    tb.HasComment("Lượt quẹt vân tay (3 trường lấy từ RJ)");
                    tb.HasTrigger("trg_AttendanceLog_Guard");
                });

            entity.HasIndex(e => new { e.EmployeeId, e.WorkDate }, "IX_AL_Employee");

            entity.HasIndex(e => e.AttendanceCode, "IX_AL_Unmapped").HasFilter("([EmployeeId] IS NULL)");

            entity.HasIndex(e => new { e.AttendanceCode, e.PunchTime }, "UQ_AttendanceLog").IsUnique();

            entity.HasIndex(e => e.SourceRecordId, "UX_AL_SourceRecord")
                .IsUnique()
                .HasFilter("([SourceRecordId] IS NOT NULL)");

            entity.Property(e => e.PunchTime).HasPrecision(0);
            entity.Property(e => e.WorkDate).HasComputedColumnSql("(CONVERT([date],[PunchTime]))", true);

            entity.HasOne(d => d.Employee).WithMany(p => p.AttendanceLogs)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK_AL_Employee");

            entity.HasOne(d => d.SyncSession).WithMany(p => p.AttendanceLogs)
                .HasForeignKey(d => d.SyncSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AL_Session");
        });

        modelBuilder.Entity<AttendanceSyncSession>(entity =>
        {
            entity.HasKey(e => e.SyncSessionId);

            entity.ToTable("AttendanceSyncSession", tb => tb.HasComment("Lần đồng bộ RJ / import Excel chấm công"));

            entity.HasIndex(e => e.StartedAt, "IX_ASS_StartedAt").IsDescending();

            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.FileName).HasMaxLength(300);
            entity.Property(e => e.FinishedAt).HasPrecision(0);
            entity.Property(e => e.Source)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.StartedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("RUNNING");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog", tb => tb.HasComment("Nhật ký thay đổi"));

            entity.HasIndex(e => new { e.TableName, e.RecordId, e.ChangedAt }, "IX_AuditLog_Record");

            entity.Property(e => e.Action)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ChangedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.TableName)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ChangedBy)
                .HasConstraintName("FK_AuditLog_ChangedBy");
        });

        modelBuilder.Entity<BreakEvenResult>(entity =>
        {
            entity.ToTable("BreakEvenResult", tb => tb.HasComment("Kết quả hòa vốn theo mã hàng"));

            entity.HasIndex(e => new { e.PeriodId, e.CalculatedAt, e.ProductId }, "UQ_BER").IsUnique();

            entity.Property(e => e.AllocatedFixedCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AllocationBasis)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.BreakEvenQty).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CalculatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.CmtUnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PercentReached).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.Revenue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SurplusValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VariableCost).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Period).WithMany(p => p.BreakEvenResults)
                .HasForeignKey(d => d.PeriodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BER_Period");

            entity.HasOne(d => d.Product).WithMany(p => p.BreakEvenResults)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BER_Product");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer", tb => tb.HasComment("Khách hàng (công ty mẹ)"));

            entity.HasIndex(e => e.CustomerName, "UQ_Customer_Name").IsUnique();

            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.DefaultAddress).HasMaxLength(500);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.HasKey(e => e.OrderId);

            entity.ToTable("CustomerOrder", tb => tb.HasComment("Đơn hàng gia công"));

            entity.HasIndex(e => new { e.Status, e.DueDate }, "IX_CustomerOrder_Status");

            entity.HasIndex(e => e.OrderCode, "UQ_CustomerOrder_Code").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.IsDelayRisk).HasComputedColumnSql("(CONVERT([bit],case when [DelayRiskSince] IS NULL then (0) else (1) end))", false);
            entity.Property(e => e.MaterialStatus)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("AWAITING");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.OrderCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(25)
                .IsUnicode(false)
                .HasDefaultValue("NEW");
            entity.Property(e => e.StopReason).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerOrders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerOrder_Customer");
        });

        modelBuilder.Entity<CutHandover>(entity =>
        {
            entity.ToTable("CutHandover", tb => tb.HasComment("Bàn giao BTP vào kho"));

            entity.HasIndex(e => e.HandoverCode, "UQ_CutHandover_Code").IsUnique();

            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.HandedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.HandoverCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.CutHandovers)
                .HasForeignKey(d => d.ConfirmedBy)
                .HasConstraintName("FK_CH_ConfirmedBy");

            entity.HasOne(d => d.WorkOrder).WithMany(p => p.CutHandovers)
                .HasForeignKey(d => d.WorkOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CH_WO");
        });

        modelBuilder.Entity<CutHandoverLine>(entity =>
        {
            entity.ToTable("CutHandoverLine", tb =>
                {
                    tb.HasComment("Dòng bàn giao BTP");
                    tb.HasTrigger("trg_CutHandoverLine_Check");
                });

            entity.HasIndex(e => new { e.CutHandoverId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_CHL").IsUnique();

            entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);

            entity.HasOne(d => d.CutHandover).WithMany(p => p.CutHandoverLines)
                .HasForeignKey(d => d.CutHandoverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CHL_Handover");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.CutHandoverLines)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CHL_OrderLine");

            entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.CutHandoverLines)
                .HasForeignKey(d => d.ProductComponentMaterialId)
                .HasConstraintName("FK_CHL_PCM");
        });

        modelBuilder.Entity<CuttingLay>(entity =>
        {
            entity.ToTable("CuttingLay", tb => tb.HasComment("Bàn cắt"));

            entity.HasIndex(e => new { e.CuttingTeamId, e.WorkDate, e.ShiftCode, e.LaySequence }, "UQ_CL").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.FabricColor).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.ShiftCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("RECORDED");

            entity.HasOne(d => d.CuttingTeam).WithMany(p => p.CuttingLays)
                .HasForeignKey(d => d.CuttingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CL_Team");

            entity.HasOne(d => d.FabricMaterial).WithMany(p => p.CuttingLays)
                .HasForeignKey(d => d.FabricMaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CL_Material");

            entity.HasOne(d => d.WorkOrder).WithMany(p => p.CuttingLays)
                .HasForeignKey(d => d.WorkOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CL_WO");
        });

        modelBuilder.Entity<CuttingLayLine>(entity =>
        {
            entity.ToTable("CuttingLayLine", tb =>
                {
                    tb.HasComment("Số cắt theo size của bàn cắt");
                    tb.HasTrigger("trg_CuttingLayLine_Check");
                });

            entity.HasIndex(e => new { e.CuttingLayId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_CLL").IsUnique();

            entity.Property(e => e.AdjustReason).HasMaxLength(500);

            entity.HasOne(d => d.CuttingLay).WithMany(p => p.CuttingLayLines)
                .HasForeignKey(d => d.CuttingLayId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CLL_Lay");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.CuttingLayLines)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CLL_OrderLine");

            entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.CuttingLayLines)
                .HasForeignKey(d => d.ProductComponentMaterialId)
                .HasConstraintName("FK_CLL_PCM");
        });

        modelBuilder.Entity<DailyMeal>(entity =>
        {
            entity.ToTable("DailyMeal", tb =>
                {
                    tb.HasComment("Suất ăn ngày (gồm suất khách)");
                    tb.HasTrigger("trg_DailyMeal_GuestCutoff");
                });

            entity.HasIndex(e => e.MealDate, "UQ_DailyMeal_Date").IsUnique();

            entity.Property(e => e.AutoClosedAt).HasPrecision(0);
            entity.Property(e => e.ClosedAt).HasPrecision(0);
            entity.Property(e => e.GuestNote).HasMaxLength(300);
            entity.Property(e => e.KitchenNotifiedAt).HasPrecision(0);
            entity.Property(e => e.MealPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("OPEN");
        });

        modelBuilder.Entity<DailyTimesheet>(entity =>
        {
            entity.ToTable("DailyTimesheet", tb => tb.HasComment("Bảng công ngày"));

            entity.HasIndex(e => e.WorkDate, "IX_DT_WorkDate");

            entity.HasIndex(e => new { e.EmployeeId, e.WorkDate }, "UQ_DailyTimesheet").IsUnique();

            entity.Property(e => e.CheckIn).HasPrecision(0);
            entity.Property(e => e.CheckOut).HasPrecision(0);
            entity.Property(e => e.IsSunday).HasComputedColumnSql("(CONVERT([bit],case when datediff(day,(0),[WorkDate])%(7)=(6) then (1) else (0) end))", true);
            entity.Property(e => e.Note).HasMaxLength(300);
            entity.Property(e => e.OvertimeHours).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Source)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasComputedColumnSql("(CONVERT([varchar](15),case when [AttendanceAdjustmentId] IS NULL then 'FINGERPRINT' else 'ADJUSTMENT' end))", true);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.WorkdayValue).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.WorkedHours).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.AttendanceAdjustment).WithMany(p => p.DailyTimesheets)
                .HasForeignKey(d => d.AttendanceAdjustmentId)
                .HasConstraintName("FK_DT_Adjustment");

            entity.HasOne(d => d.Employee).WithMany(p => p.DailyTimesheets)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DT_Employee");
        });

        modelBuilder.Entity<DefectRecord>(entity =>
        {
            entity.ToTable("DefectRecord", tb =>
                {
                    tb.HasComment("Hàng lỗi Rework/Scrap và người gây lỗi");
                    tb.HasTrigger("trg_DefectRecord_Check");
                    tb.HasTrigger("trg_DefectRecord_PeriodLock");
                });

            entity.HasIndex(e => new { e.OrderLineId, e.DefectType }, "IX_DR_OrderLine");

            entity.HasIndex(e => new { e.ResponsibleWorkerId, e.WorkDate }, "IX_DR_Worker");

            entity.HasIndex(e => e.DefectCode, "UQ_DefectRecord_Code").IsUnique();

            entity.Property(e => e.DefectCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.DefectReasonCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DefectType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.EditDeadline).HasPrecision(0);
            entity.Property(e => e.ObjectedAt).HasPrecision(0);
            entity.Property(e => e.ReasonNote).HasMaxLength(500);
            entity.Property(e => e.RecordedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ShiftCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("RECORDED");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.WorkerNotifiedAt).HasPrecision(0);
            entity.Property(e => e.WorkerObjection).HasMaxLength(500);

            entity.HasOne(d => d.Operation).WithMany(p => p.DefectRecords)
                .HasForeignKey(d => d.OperationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DR_Operation");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.DefectRecords)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DR_OrderLine");

            entity.HasOne(d => d.PieceRate).WithMany(p => p.DefectRecords)
                .HasForeignKey(d => d.PieceRateId)
                .HasConstraintName("FK_DR_PieceRate");

            entity.HasOne(d => d.RecordedByNavigation).WithMany(p => p.DefectRecordRecordedByNavigations)
                .HasForeignKey(d => d.RecordedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DR_RecordedBy");

            entity.HasOne(d => d.ResponsibleWorker).WithMany(p => p.DefectRecordResponsibleWorkers)
                .HasForeignKey(d => d.ResponsibleWorkerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DR_Worker");

            entity.HasOne(d => d.SewingTeam).WithMany(p => p.DefectRecords)
                .HasForeignKey(d => d.SewingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DR_Team");
        });

        modelBuilder.Entity<DispatchNote>(entity =>
        {
            entity.ToTable("DispatchNote", tb => tb.HasComment("Phiếu xuất kho thành phẩm (kiêm lệnh giao hàng)"));

            entity.HasIndex(e => new { e.OrderId, e.Status }, "IX_DN_Order");

            entity.HasIndex(e => e.DispatchCode, "UQ_DispatchNote_Code").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
            entity.Property(e => e.DispatchCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.DispatchedAt).HasPrecision(0);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(25)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.DispatchedByNavigation).WithMany(p => p.DispatchNotes)
                .HasForeignKey(d => d.DispatchedBy)
                .HasConstraintName("FK_DN_DispatchedBy");

            entity.HasOne(d => d.Order).WithMany(p => p.DispatchNotes)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DN_Order");
        });

        modelBuilder.Entity<DispatchNoteLine>(entity =>
        {
            entity.ToTable("DispatchNoteLine", tb =>
                {
                    tb.HasComment("Dòng phiếu xuất kho");
                    tb.HasTrigger("trg_DispatchNoteLine_Check");
                });

            entity.HasIndex(e => new { e.DispatchNoteId, e.OrderLineId }, "UQ_DNL").IsUnique();

            entity.HasOne(d => d.DispatchNote).WithMany(p => p.DispatchNoteLines)
                .HasForeignKey(d => d.DispatchNoteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DNL_DN");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.DispatchNoteLines)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DNL_OrderLine");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employee", tb =>
                {
                    tb.HasComment("Nhân viên");
                    tb.HasTrigger("trg_Employee_TeamLeadership");
                });

            entity.HasIndex(e => e.TeamId, "IX_Employee_Team");

            entity.HasIndex(e => e.EmployeeCode, "UQ_Employee_Code").IsUnique();

            entity.HasIndex(e => e.AttendanceCode, "UX_Employee_AttendanceCode")
                .IsUnique()
                .HasFilter("([AttendanceCode] IS NOT NULL)");

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.CurrentGradeCode)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.PayType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Role).WithMany(p => p.Employees)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employee_Role");

            entity.HasOne(d => d.Team).WithMany(p => p.Employees)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_Employee_Team");
        });

        modelBuilder.Entity<FgHandover>(entity =>
        {
            entity.ToTable("FgHandover", tb => tb.HasComment("Bàn giao thành phẩm"));

            entity.HasIndex(e => e.HandoverCode, "UQ_FgHandover_Code").IsUnique();

            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.HandedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.HandoverCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.FgHandoverConfirmedByNavigations)
                .HasForeignKey(d => d.ConfirmedBy)
                .HasConstraintName("FK_FGH_ConfirmedBy");

            entity.HasOne(d => d.Inspector).WithMany(p => p.FgHandoverInspectors)
                .HasForeignKey(d => d.InspectorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FGH_Inspector");

            entity.HasOne(d => d.Order).WithMany(p => p.FgHandovers)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FGH_Order");

            entity.HasOne(d => d.Product).WithMany(p => p.FgHandovers)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FGH_Product");
        });

        modelBuilder.Entity<FgHandoverLine>(entity =>
        {
            entity.ToTable("FgHandoverLine", tb =>
                {
                    tb.HasComment("Dòng bàn giao thành phẩm");
                    tb.HasTrigger("trg_FgHandoverLine_Check");
                });

            entity.HasIndex(e => new { e.FgHandoverId, e.OrderLineId }, "UQ_FGHL").IsUnique();

            entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);

            entity.HasOne(d => d.FgHandover).WithMany(p => p.FgHandoverLines)
                .HasForeignKey(d => d.FgHandoverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FGHL_Handover");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.FgHandoverLines)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FGHL_OrderLine");
        });

        modelBuilder.Entity<GradeEvaluation>(entity =>
        {
            entity.ToTable("GradeEvaluation", tb => tb.HasComment("Đề xuất / đổi bậc thợ; dòng APPLIED là lịch sử bậc"));

            entity.HasIndex(e => new { e.WorkerId, e.EffectiveDate }, "IX_GradeEval_History")
                .IsDescending(false, true)
                .HasFilter("([Status]='APPLIED')");

            entity.HasIndex(e => new { e.WorkerId, e.ProposedBy, e.EvaluationDate }, "UQ_GradeEval").IsUnique();

            entity.Property(e => e.ConsideredAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.CurrentGradeCode)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.DecisionNote).HasMaxLength(500);
            entity.Property(e => e.ProposedGradeCode)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SUBMITTED");

            entity.HasOne(d => d.ProposedByNavigation).WithMany(p => p.GradeEvaluationProposedByNavigations)
                .HasForeignKey(d => d.ProposedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GradeEval_ProposedBy");

            entity.HasOne(d => d.Team).WithMany(p => p.GradeEvaluations)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_GradeEval_Team");

            entity.HasOne(d => d.Worker).WithMany(p => p.GradeEvaluationWorkers)
                .HasForeignKey(d => d.WorkerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GradeEval_Worker");
        });

        modelBuilder.Entity<GradeLevel>(entity =>
        {
            entity.ToTable("GradeLevel", tb => tb.HasComment("Bậc thợ A–D và tiền bậc theo ngày hiệu lực"));

            entity.HasIndex(e => new { e.GradeCode, e.EffectiveDate }, "UQ_GradeLevel").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.GradeCode)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.GradeName).HasMaxLength(50);
            entity.Property(e => e.MonthlyAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<KpiTarget>(entity =>
        {
            entity.ToTable("KpiTarget", tb => tb.HasComment("KPI tổ may"));

            entity.HasIndex(e => new { e.OrderId, e.ProductId, e.SewingTeamId, e.StartDate }, "UQ_Kpi").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Result)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.Order).WithMany(p => p.KpiTargets)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Kpi_Order");

            entity.HasOne(d => d.Product).WithMany(p => p.KpiTargets)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Kpi_Product");

            entity.HasOne(d => d.SewingTeam).WithMany(p => p.KpiTargets)
                .HasForeignKey(d => d.SewingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Kpi_Team");
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("Material", tb => tb.HasComment("Nguyên vật liệu"));

            entity.HasIndex(e => e.MaterialCode, "UQ_Material_Code").IsUnique();

            entity.HasIndex(e => new { e.MaterialName, e.MaterialType }, "UQ_Material_NameType").IsUnique();

            entity.Property(e => e.Category)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.MaterialCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.MaterialName).HasMaxLength(200);
            entity.Property(e => e.MaterialType)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasComputedColumnSql("(CONVERT([varchar](10),case when [Category]='RIB' OR [Category]='INTERLINING' OR [Category]='LINING' OR [Category]='MAIN_FABRIC' then 'FABRIC' else 'TRIM' end))", true);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.Unit).HasMaxLength(20);
        });

        modelBuilder.Entity<MaterialIssue>(entity =>
        {
            entity.ToTable("MaterialIssue", tb =>
                {
                    tb.HasComment("Phiếu cấp kho: NVL cho tổ cắt/may hoặc BTP cho tổ may");
                    tb.HasTrigger("trg_MaterialIssue_Check");
                });

            entity.HasIndex(e => new { e.ReceivingTeamId, e.Status }, "IX_MI_Team");

            entity.HasIndex(e => e.IssueCode, "UQ_MaterialIssue_Code").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.IssueCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ReceivedAt).HasPrecision(0);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING_RECEIPT");
            entity.Property(e => e.WarehouseType)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("MATERIAL");

            entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.MaterialIssueIssuedByNavigations)
                .HasForeignKey(d => d.IssuedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MI_IssuedBy");

            entity.HasOne(d => d.Order).WithMany(p => p.MaterialIssues)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MI_Order");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.MaterialIssueReceivedByNavigations)
                .HasForeignKey(d => d.ReceivedBy)
                .HasConstraintName("FK_MI_ReceivedBy");

            entity.HasOne(d => d.ReceivingTeam).WithMany(p => p.MaterialIssues)
                .HasForeignKey(d => d.ReceivingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MI_Team");

            entity.HasOne(d => d.Request).WithMany(p => p.MaterialIssues)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("FK_MI_Request");

            entity.HasOne(d => d.WorkOrder).WithMany(p => p.MaterialIssues)
                .HasForeignKey(d => d.WorkOrderId)
                .HasConstraintName("FK_MI_WO");
        });

        modelBuilder.Entity<MaterialIssueLine>(entity =>
        {
            entity.ToTable("MaterialIssueLine", tb =>
                {
                    tb.HasComment("Dòng phiếu cấp kho");
                    tb.HasTrigger("trg_MaterialIssueLine_Check");
                });

            entity.HasIndex(e => new { e.MaterialIssueId, e.MaterialId, e.MaterialColor, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_MIL").IsUnique();

            entity.Property(e => e.ConfirmedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);
            entity.Property(e => e.IssuedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MaterialColor).HasMaxLength(50);

            entity.HasOne(d => d.Material).WithMany(p => p.MaterialIssueLines)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK_MIL_Material");

            entity.HasOne(d => d.MaterialIssue).WithMany(p => p.MaterialIssueLines)
                .HasForeignKey(d => d.MaterialIssueId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MIL_Issue");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.MaterialIssueLines)
                .HasForeignKey(d => d.OrderLineId)
                .HasConstraintName("FK_MIL_OrderLine");

            entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.MaterialIssueLines)
                .HasForeignKey(d => d.ProductComponentMaterialId)
                .HasConstraintName("FK_MIL_PCM");
        });

        modelBuilder.Entity<MaterialReceipt>(entity =>
        {
            entity.ToTable("MaterialReceipt", tb => tb.HasComment("Phiếu nhập NVL (mới / bổ sung)"));

            entity.HasIndex(e => new { e.OrderId, e.Status }, "IX_MR_Order");

            entity.HasIndex(e => e.ReceiptCode, "UQ_MaterialReceipt_Code").IsUnique();

            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.ReceiptCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ReturnReason).HasMaxLength(500);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Source)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasComputedColumnSql("(CONVERT([varchar](10),case when [PurchaseOrderId] IS NULL then 'CUSTOMER' else 'SUPPLIER' end))", true);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.SubmittedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");

            entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.MaterialReceipts)
                .HasForeignKey(d => d.ConfirmedBy)
                .HasConstraintName("FK_MR_ConfirmedBy");

            entity.HasOne(d => d.Order).WithMany(p => p.MaterialReceipts)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_MR_Order");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.MaterialReceipts)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("FK_MR_PO");

            entity.HasOne(d => d.ShortageRequest).WithMany(p => p.MaterialReceipts)
                .HasForeignKey(d => d.ShortageRequestId)
                .HasConstraintName("FK_MR_ShortageRequest");
        });

        modelBuilder.Entity<MaterialReceiptLine>(entity =>
        {
            entity.ToTable("MaterialReceiptLine", tb =>
                {
                    tb.HasComment("Dòng phiếu nhập NVL");
                    tb.HasTrigger("trg_MaterialReceiptLine_Check");
                });

            entity.HasIndex(e => new { e.MaterialReceiptId, e.OrderMaterialId }, "UQ_MRL")
                .IsUnique()
                .HasFilter("([OrderMaterialId] IS NOT NULL)");

            entity.HasIndex(e => new { e.MaterialReceiptId, e.MaterialId, e.MaterialColor }, "UQ_MRL_Stock")
                .IsUnique()
                .HasFilter("([OrderMaterialId] IS NULL)");

            entity.Property(e => e.AcceptedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MaterialColor).HasMaxLength(50);

            entity.HasOne(d => d.Material).WithMany(p => p.MaterialReceiptLines)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK_MRL_Material");

            entity.HasOne(d => d.MaterialReceipt).WithMany(p => p.MaterialReceiptLines)
                .HasForeignKey(d => d.MaterialReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MRL_Receipt");

            entity.HasOne(d => d.OrderMaterial).WithMany(p => p.MaterialReceiptLines)
                .HasForeignKey(d => d.OrderMaterialId)
                .HasConstraintName("FK_MRL_OrderMaterial");

            entity.HasOne(d => d.OriginalReceiptLine).WithMany(p => p.InverseOriginalReceiptLine)
                .HasForeignKey(d => d.OriginalReceiptLineId)
                .HasConstraintName("FK_MRL_OrigLine");

            entity.HasOne(d => d.PurchaseOrderLine).WithMany(p => p.MaterialReceiptLines)
                .HasForeignKey(d => d.PurchaseOrderLineId)
                .HasConstraintName("FK_MRL_POL");
        });

        modelBuilder.Entity<MaterialReturn>(entity =>
        {
            entity.ToTable("MaterialReturn", tb => tb.HasComment("Phiếu trả vải thừa"));

            entity.HasIndex(e => e.ReturnCode, "UQ_MaterialReturn_Code").IsUnique();

            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.ConfirmedBy)
                .HasConstraintName("FK_MRet_ConfirmedBy");

            entity.HasOne(d => d.MaterialIssue).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.MaterialIssueId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MRet_Issue");
        });

        modelBuilder.Entity<MaterialReturnLine>(entity =>
        {
            entity.ToTable("MaterialReturnLine", tb =>
                {
                    tb.HasComment("Dòng phiếu trả vải thừa");
                    tb.HasTrigger("trg_MaterialReturnLine_Check");
                });

            entity.HasIndex(e => new { e.MaterialReturnId, e.MaterialIssueLineId }, "UQ_MRetL").IsUnique();

            entity.Property(e => e.ConfirmedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);
            entity.Property(e => e.ReturnedQty).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.MaterialIssueLine).WithMany(p => p.MaterialReturnLines)
                .HasForeignKey(d => d.MaterialIssueLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MRetL_IssueLine");

            entity.HasOne(d => d.MaterialReturn).WithMany(p => p.MaterialReturnLines)
                .HasForeignKey(d => d.MaterialReturnId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MRetL_Return");
        });

        modelBuilder.Entity<MonthlyCost>(entity =>
        {
            entity.ToTable("MonthlyCost", tb =>
                {
                    tb.HasComment("Chi phí tháng theo khoản mục");
                    tb.HasTrigger("trg_MonthlyCost_PeriodLock");
                });

            entity.HasIndex(e => new { e.PeriodId, e.CostCategoryCode, e.ProductId }, "UQ_MC").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CostCategoryCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.CostType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ImportedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Note).HasMaxLength(300);

            entity.HasOne(d => d.Period).WithMany(p => p.MonthlyCosts)
                .HasForeignKey(d => d.PeriodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MC_Period");

            entity.HasOne(d => d.Product).WithMany(p => p.MonthlyCosts)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_MC_Product");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification", tb => tb.HasComment("Thông báo người dùng (giữ 90 ngày)"));

            entity.HasIndex(e => e.CreatedAt, "IX_Notification_CreatedAt");

            entity.HasIndex(e => new { e.RecipientId, e.CreatedAt }, "IX_Notification_Inbox").IsDescending(false, true);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.IsRead).HasComputedColumnSql("(CONVERT([bit],case when [ReadAt] IS NULL then (0) else (1) end))", true);
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.PushStatus)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.ReadAt).HasPrecision(0);
            entity.Property(e => e.RefTable)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SourceType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Recipient).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RecipientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_Recipient");
        });

        modelBuilder.Entity<Operation>(entity =>
        {
            entity.ToTable("Operation", tb => tb.HasComment("Công đoạn"));

            entity.HasIndex(e => e.OperationGroupId, "IX_Operation_Group").HasFilter("([OperationGroupId] IS NOT NULL)");

            entity.HasIndex(e => new { e.ProductId, e.OperationCode }, "UQ_Operation").IsUnique();

            entity.HasIndex(e => new { e.OperationId, e.ProductId }, "UQ_Operation_IdProduct").IsUnique();

            entity.HasIndex(e => e.ProductId, "UX_Operation_FinalStep")
                .IsUnique()
                .HasFilter("([IsFinalStep]=(1) AND [Status]='ACTIVE')");

            entity.Property(e => e.OperationCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.OperationName).HasMaxLength(200);
            entity.Property(e => e.StandardTimeSec).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.TeamType)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("SEWING");

            entity.HasOne(d => d.Product).WithOne(p => p.Operation)
                .HasForeignKey<Operation>(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Operation_Product");

            entity.HasOne(d => d.OperationGroup).WithMany(p => p.Operations)
                .HasPrincipalKey(p => new { p.OperationGroupId, p.ProductId })
                .HasForeignKey(d => new { d.OperationGroupId, d.ProductId })
                .HasConstraintName("FK_Operation_Group");
        });

        modelBuilder.Entity<OperationAssignment>(entity =>
        {
            entity.ToTable("OperationAssignment", tb =>
                {
                    tb.HasComment("Phân công công đoạn");
                    tb.HasTrigger("trg_OperationAssignment_Check");
                });

            entity.HasIndex(e => new { e.SewingTeamId, e.OrderId, e.OperationId, e.WorkerId }, "UX_OA_Current")
                .IsUnique()
                .HasFilter("([ValidTo] IS NULL)");

            entity.Property(e => e.AssignedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");

            entity.HasOne(d => d.Operation).WithMany(p => p.OperationAssignments)
                .HasForeignKey(d => d.OperationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OA_Operation");

            entity.HasOne(d => d.Order).WithMany(p => p.OperationAssignments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OA_Order");

            entity.HasOne(d => d.SewingTeam).WithMany(p => p.OperationAssignments)
                .HasForeignKey(d => d.SewingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OA_Team");

            entity.HasOne(d => d.Worker).WithMany(p => p.OperationAssignments)
                .HasForeignKey(d => d.WorkerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OA_Worker");
        });

        modelBuilder.Entity<OperationComponent>(entity =>
        {
            entity.HasKey(e => new { e.OperationId, e.ProductComponentId });

            entity.ToTable("OperationComponent", tb => tb.HasComment("Công đoạn làm trên chi tiết nào"));

            entity.HasIndex(e => e.ProductComponentId, "IX_OC_Component");

            entity.HasOne(d => d.Operation).WithMany(p => p.OperationComponents)
                .HasPrincipalKey(p => new { p.OperationId, p.ProductId })
                .HasForeignKey(d => new { d.OperationId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OC_Operation");

            entity.HasOne(d => d.ProductComponent).WithMany(p => p.OperationComponents)
                .HasPrincipalKey(p => new { p.ProductComponentId, p.ProductId })
                .HasForeignKey(d => new { d.ProductComponentId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OC_Component");
        });

        modelBuilder.Entity<OperationGroup>(entity =>
        {
            entity.ToTable("OperationGroup", tb => tb.HasComment("Công đoạn to (nhóm công đoạn)"));

            entity.HasIndex(e => e.GroupName, "IX_OG_Name");

            entity.HasIndex(e => new { e.ProductId, e.GroupName }, "UQ_OG_Name").IsUnique();

            entity.HasIndex(e => new { e.OperationGroupId, e.ProductId }, "UQ_OG_Product").IsUnique();

            entity.Property(e => e.GroupName).HasMaxLength(200);

            entity.HasOne(d => d.Product).WithMany(p => p.OperationGroups)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OG_Product");
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLine", tb => tb.HasComment("Dòng đơn mã hàng–màu–size"));

            entity.HasIndex(e => e.ProductId, "IX_OrderLine_Product");

            entity.HasIndex(e => new { e.OrderId, e.ProductVariantId }, "UQ_OrderLine").IsUnique();

            entity.Property(e => e.CmtUnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderLine_Order");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.OrderLines)
                .HasPrincipalKey(p => new { p.ProductVariantId, p.ProductId })
                .HasForeignKey(d => new { d.ProductVariantId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderLine_Variant");
        });

        modelBuilder.Entity<OrderMaterial>(entity =>
        {
            entity.ToTable("OrderMaterial", tb => tb.HasComment("NVL của đơn: cần bao nhiêu, khách cấp bao nhiêu, phải mua bao nhiêu"));

            entity.HasIndex(e => new { e.OrderId, e.MaterialId, e.MaterialColor }, "UQ_OM").IsUnique();

            entity.Property(e => e.CustomerQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MaterialColor)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PurchaseQty)
                .HasComputedColumnSql("(case when [RequiredQty]>[CustomerQty] then [RequiredQty]-[CustomerQty] else (0) end)", false)
                .HasColumnType("decimal(19, 3)");
            entity.Property(e => e.RequiredQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Source)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasComputedColumnSql("(CONVERT([varchar](10),case when [CustomerQty]=(0) then 'SUPPLIER' when [CustomerQty]>=[RequiredQty] then 'CUSTOMER' else 'BOTH' end))", false);

            entity.HasOne(d => d.Material).WithMany(p => p.OrderMaterials)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OM_Material");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderMaterials)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OM_Order");
        });

        modelBuilder.Entity<OutputRecord>(entity =>
        {
            entity.ToTable("OutputRecord", tb =>
                {
                    tb.HasComment("Sản lượng công đoạn");
                    tb.HasTrigger("trg_OutputRecord_Check");
                    tb.HasTrigger("trg_OutputRecord_PeriodLock");
                });

            entity.HasIndex(e => new { e.OrderLineId, e.OperationId }, "IX_OR_OrderLine");

            entity.HasIndex(e => new { e.WorkerId, e.WorkDate }, "IX_OR_Worker");

            entity.HasIndex(e => new { e.WorkDate, e.ShiftCode, e.SewingTeamId, e.OrderLineId, e.OperationId, e.WorkerId }, "UQ_OutputRecord").IsUnique();

            entity.Property(e => e.EditDeadline).HasPrecision(0);
            entity.Property(e => e.EnteredAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ShiftCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("SAVED");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Operation).WithMany(p => p.OutputRecords)
                .HasForeignKey(d => d.OperationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OR_Operation");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.OutputRecords)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OR_OrderLine");

            entity.HasOne(d => d.PieceRate).WithMany(p => p.OutputRecords)
                .HasForeignKey(d => d.PieceRateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OR_PieceRate");

            entity.HasOne(d => d.SewingTeam).WithMany(p => p.OutputRecords)
                .HasForeignKey(d => d.SewingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OR_Team");

            entity.HasOne(d => d.Worker).WithMany(p => p.OutputRecords)
                .HasForeignKey(d => d.WorkerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OR_Worker");
        });

        modelBuilder.Entity<Payroll>(entity =>
        {
            entity.ToTable("Payroll", tb => tb.HasComment("Bảng lương tháng (import)"));

            entity.HasIndex(e => e.PeriodId, "UQ_Payroll_Period").IsUnique();

            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.SubmittedAt).HasPrecision(0);

            entity.HasOne(d => d.Period).WithOne(p => p.Payroll)
                .HasForeignKey<Payroll>(d => d.PeriodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payroll_Period");
        });

        modelBuilder.Entity<Payslip>(entity =>
        {
            entity.ToTable("Payslip", tb => tb.HasComment("Phiếu lương"));

            entity.HasIndex(e => new { e.PayrollId, e.EmployeeId }, "UQ_Payslip").IsUnique();

            entity.Property(e => e.Insurance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetPay).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAdditions).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDeductions).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WorkDays).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Employee).WithMany(p => p.Payslips)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payslip_Employee");

            entity.HasOne(d => d.Payroll).WithMany(p => p.Payslips)
                .HasForeignKey(d => d.PayrollId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payslip_Payroll");
        });

        modelBuilder.Entity<PayslipItem>(entity =>
        {
            entity.ToTable("PayslipItem", tb => tb.HasComment("Khoản trên phiếu lương"));

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ItemGroup)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.ItemName).HasMaxLength(100);

            entity.HasOne(d => d.Payslip).WithMany(p => p.PayslipItems)
                .HasForeignKey(d => d.PayslipId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PayslipItem_Payslip");
        });

        modelBuilder.Entity<PieceRate>(entity =>
        {
            entity.ToTable("PieceRate", tb =>
                {
                    tb.HasComment("Đơn giá công đoạn");
                    tb.HasTrigger("trg_PieceRate_Approved");
                });

            entity.HasIndex(e => new { e.OperationId, e.EffectiveDate }, "UQ_PieceRate").IsUnique();

            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.DecisionNote).HasMaxLength(500);
            entity.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.SubmittedAt).HasPrecision(0);

            entity.HasOne(d => d.Operation).WithMany(p => p.PieceRates)
                .HasForeignKey(d => d.OperationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PieceRate_Operation");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Product", tb => tb.HasComment("Mã hàng"));

            entity.HasIndex(e => e.ProductCode, "UQ_Product_Code").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ProductCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.SampleImageUrl).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
        });

        modelBuilder.Entity<ProductComponent>(entity =>
        {
            entity.ToTable("ProductComponent", tb => tb.HasComment("Thành phần của mã hàng: chi tiết vải (PANEL) và phụ liệu (TRIM)"));

            entity.HasIndex(e => new { e.ProductId, e.ComponentName }, "UQ_PC").IsUnique();

            entity.HasIndex(e => new { e.ProductComponentId, e.ProductId }, "UQ_PC_IdProduct").IsUnique();

            entity.Property(e => e.ComponentName).HasMaxLength(100);
            entity.Property(e => e.ComponentType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PiecesPerGarment).HasDefaultValue((short)1);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductComponents)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PC_Product");
        });

        modelBuilder.Entity<ProductComponentMaterial>(entity =>
        {
            entity.ToTable("ProductComponentMaterial", tb => tb.HasComment("Định mức NVL theo biến thể × thành phần: NVL, màu NVL, lượng / sản phẩm, hao hụt, kích thước chi tiết"));

            entity.HasIndex(e => new { e.MaterialId, e.MaterialColor }, "IX_PCM_Material");

            entity.HasIndex(e => new { e.ProductVariantId, e.ProductComponentId, e.MaterialId }, "UQ_PCM").IsUnique();

            entity.Property(e => e.LengthCm).HasColumnType("decimal(7, 2)");
            entity.Property(e => e.MaterialColor)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.QtyPerGarment).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.SpecNote).HasMaxLength(200);
            entity.Property(e => e.WastagePercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.WidthCm).HasColumnType("decimal(7, 2)");

            entity.HasOne(d => d.Material).WithMany(p => p.ProductComponentMaterials)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PCM_Material");

            entity.HasOne(d => d.ProductComponent).WithMany(p => p.ProductComponentMaterials)
                .HasPrincipalKey(p => new { p.ProductComponentId, p.ProductId })
                .HasForeignKey(d => new { d.ProductComponentId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PCM_Component");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.ProductComponentMaterials)
                .HasPrincipalKey(p => new { p.ProductVariantId, p.ProductId })
                .HasForeignKey(d => new { d.ProductVariantId, d.ProductId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PCM_Variant");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.ToTable("ProductVariant", tb => tb.HasComment("Biến thể mã hàng: màu × size"));

            entity.HasIndex(e => new { e.ProductId, e.Color, e.SizeCode }, "UQ_PV").IsUnique();

            entity.HasIndex(e => new { e.ProductVariantId, e.ProductId }, "UQ_PV_IdProduct").IsUnique();

            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.SizeCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PV_Product");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrder", tb => tb.HasComment("Đơn mua NVL kèm thanh toán (OrderId NULL = đơn mua dùng chung)"));

            entity.HasIndex(e => e.PoCode, "UQ_PurchaseOrder_Code").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.GoodsStatus)
                .HasMaxLength(25)
                .IsUnicode(false)
                .HasDefaultValue("PENDING_FINALISATION");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PaymentNote).HasMaxLength(500);
            entity.Property(e => e.PaymentReturnReason).HasMaxLength(500);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("UNPAID");
            entity.Property(e => e.PoCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Order).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_PO_Order");

            entity.HasOne(d => d.SourceRequest).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SourceRequestId)
                .HasConstraintName("FK_PO_Request");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_PO_Supplier");
        });

        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.ToTable("PurchaseOrderLine", tb =>
                {
                    tb.HasComment("Dòng đơn mua NVL");
                    tb.HasTrigger("trg_PurchaseOrderLine_Check");
                });

            entity.HasIndex(e => new { e.PurchaseOrderId, e.OrderMaterialId }, "UQ_POL")
                .IsUnique()
                .HasFilter("([OrderMaterialId] IS NOT NULL)");

            entity.HasIndex(e => new { e.PurchaseOrderId, e.MaterialId, e.MaterialColor }, "UQ_POL_Stock")
                .IsUnique()
                .HasFilter("([OrderMaterialId] IS NULL)");

            entity.Property(e => e.Amount)
                .HasComputedColumnSql("(CONVERT([decimal](18,2),[OrderedQty]*[UnitPrice]))", false)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.NotReplenishedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.OrderedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Material).WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK_POL_Material");

            entity.HasOne(d => d.OrderMaterial).WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.OrderMaterialId)
                .HasConstraintName("FK_POL_OrderMaterial");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderLines)
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POL_PO");
        });

        modelBuilder.Entity<QcBatch>(entity =>
        {
            entity.ToTable("QcBatch", tb =>
                {
                    tb.HasComment("Đợt kiểm hoàn thiện");
                    tb.HasTrigger("trg_QcBatch_PeriodLock");
                });

            entity.HasIndex(e => e.OrderLineId, "IX_QC_OrderLine");

            entity.HasIndex(e => new { e.WorkDate, e.ShiftCode, e.BatchTime, e.SewingTeamId, e.OrderLineId }, "UQ_QcBatch").IsUnique();

            entity.Property(e => e.BatchTime).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.EditDeadline).HasPrecision(0);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ShiftCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Inspector).WithMany(p => p.QcBatches)
                .HasForeignKey(d => d.InspectorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QC_Inspector");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.QcBatches)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QC_OrderLine");

            entity.HasOne(d => d.SewingTeam).WithMany(p => p.QcBatches)
                .HasForeignKey(d => d.SewingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QC_Team");
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity.ToTable("Request", tb => tb.HasComment("Đơn từ (10 loại, gồm yêu cầu NVL / BTP)"));

            entity.HasIndex(e => new { e.CreatedBy, e.Status }, "IX_Request_CreatedBy");

            entity.HasIndex(e => new { e.RequestTypeCode, e.Status }, "IX_Request_Inbox");

            entity.HasIndex(e => e.RequestCode, "UQ_Request_Code").IsUnique();

            entity.HasIndex(e => e.DefectRecordId, "UX_Request_Defect")
                .IsUnique()
                .HasFilter("([DefectRecordId] IS NOT NULL)");

            entity.HasIndex(e => e.MaterialReceiptId, "UX_Request_Receipt")
                .IsUnique()
                .HasFilter("([MaterialReceiptId] IS NOT NULL AND [RequestTypeCode]='SHORT_DELIVERY')");

            entity.Property(e => e.CompletedAt).HasPrecision(0);
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.DecidedAt).HasPrecision(0);
            entity.Property(e => e.DecisionNote).HasMaxLength(1000);
            entity.Property(e => e.RequestCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RequestTypeCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ResultNote).HasMaxLength(1000);
            entity.Property(e => e.RowVer)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ShortageResolution)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Requests)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Request_CreatedBy");

            entity.HasOne(d => d.CreatedByTeam).WithMany(p => p.Requests)
                .HasForeignKey(d => d.CreatedByTeamId)
                .HasConstraintName("FK_Request_Team");

            entity.HasOne(d => d.DefectRecord).WithOne(p => p.Request)
                .HasForeignKey<Request>(d => d.DefectRecordId)
                .HasConstraintName("FK_Request_DefectRecord");

            entity.HasOne(d => d.KpiTarget).WithMany(p => p.Requests)
                .HasForeignKey(d => d.KpiTargetId)
                .HasConstraintName("FK_Request_Kpi");

            entity.HasOne(d => d.MaterialIssue).WithMany(p => p.Requests)
                .HasForeignKey(d => d.MaterialIssueId)
                .HasConstraintName("FK_Request_MaterialIssue");

            entity.HasOne(d => d.MaterialReceipt).WithOne(p => p.Request)
                .HasForeignKey<Request>(d => d.MaterialReceiptId)
                .HasConstraintName("FK_Request_MaterialReceipt");

            entity.HasOne(d => d.Order).WithMany(p => p.Requests)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_Request_Order");

            entity.HasOne(d => d.WorkOrder).WithMany(p => p.Requests)
                .HasForeignKey(d => d.WorkOrderId)
                .HasConstraintName("FK_Request_WorkOrder");
        });

        modelBuilder.Entity<RequestLine>(entity =>
        {
            entity.ToTable("RequestLine", tb =>
                {
                    tb.HasComment("Dòng chi tiết đơn từ");
                    tb.HasTrigger("trg_RequestLine_Check");
                });

            entity.Property(e => e.ItemDescription).HasMaxLength(300);
            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReplenishQty).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Material).WithMany(p => p.RequestLines)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK_RequestLine_Material");

            entity.HasOne(d => d.MaterialReceiptLine).WithMany(p => p.RequestLines)
                .HasForeignKey(d => d.MaterialReceiptLineId)
                .HasConstraintName("FK_RequestLine_ReceiptLine");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.RequestLines)
                .HasForeignKey(d => d.OrderLineId)
                .HasConstraintName("FK_RequestLine_OrderLine");

            entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.RequestLines)
                .HasForeignKey(d => d.ProductComponentMaterialId)
                .HasConstraintName("FK_RequestLine_PCM");

            entity.HasOne(d => d.Request).WithMany(p => p.RequestLines)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RequestLine_Request");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role", tb => tb.HasComment("Vai trò (9 vai trò, gồm Tổ trưởng và Tổ phó)"));

            entity.HasIndex(e => e.RoleCode, "UQ_Role_Code").IsUnique();

            entity.Property(e => e.RoleCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RoleName).HasMaxLength(100);
        });

        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.ToTable("StockBalance", tb =>
                {
                    tb.HasComment("Tồn hiện tại theo mặt hàng và kho");
                    tb.HasTrigger("trg_StockBalance_Check");
                });

            entity.HasIndex(e => new { e.WarehouseType, e.MaterialId, e.MaterialColor, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_SB_Item").IsUnique();

            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.OnHandQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.WarehouseType)
                .HasMaxLength(15)
                .IsUnicode(false);

            entity.HasOne(d => d.Material).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK_SB_Material");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.OrderLineId)
                .HasConstraintName("FK_SB_OrderLine");

            entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.ProductComponentMaterialId)
                .HasConstraintName("FK_SB_PCM");
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.ToTable("StockTransaction", tb =>
                {
                    tb.HasComment("Thẻ kho (ghi qua usp_Stock_Post)");
                    tb.HasTrigger("trg_StockTransaction_AppendOnly");
                    tb.HasTrigger("trg_StockTransaction_PostOnly");
                });

            entity.HasIndex(e => new { e.StockBalanceId, e.CreatedAt }, "IX_ST_Balance");

            entity.HasIndex(e => new { e.SourceDocType, e.SourceDocId, e.SourceLineId, e.StockBalanceId }, "UQ_ST_SourceLine").IsUnique();

            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.QtyChange).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SourceDocType)
                .HasMaxLength(30)
                .IsUnicode(false);

            entity.HasOne(d => d.StockBalance).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.StockBalanceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ST_Balance");
        });

        modelBuilder.Entity<Stocktake>(entity =>
        {
            entity.ToTable("Stocktake", tb => tb.HasComment("Phiếu kiểm kê"));

            entity.HasIndex(e => e.StocktakeCode, "UQ_Stocktake_Code").IsUnique();

            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.ReturnReason).HasMaxLength(500);
            entity.Property(e => e.Scope).HasMaxLength(300);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING_APPROVAL");
            entity.Property(e => e.StocktakeCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.WarehouseType)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<StocktakeLine>(entity =>
        {
            entity.ToTable("StocktakeLine", tb =>
                {
                    tb.HasComment("Dòng kiểm kê");
                    tb.HasTrigger("trg_StocktakeLine_Check");
                });

            entity.HasIndex(e => new { e.StocktakeId, e.StockBalanceId }, "UQ_STL").IsUnique();

            entity.Property(e => e.BookQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.CountedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DifferenceQty)
                .HasComputedColumnSql("(isnull([CountedQty],[BookQty])-[BookQty])", false)
                .HasColumnType("decimal(19, 3)");
            entity.Property(e => e.Note).HasMaxLength(300);

            entity.HasOne(d => d.StockBalance).WithMany(p => p.StocktakeLines)
                .HasForeignKey(d => d.StockBalanceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STL_Balance");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeLines)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STL_Stocktake");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Supplier", tb => tb.HasComment("Nhà cung cấp"));

            entity.HasIndex(e => e.SupplierName, "UQ_Supplier_Name").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MaterialTypes).HasMaxLength(200);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.SupplierName).HasMaxLength(200);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey);

            entity.ToTable("SystemSetting", tb => tb.HasComment("Tham số hệ thống"));

            entity.Property(e => e.SettingKey)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SettingValue).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Team", tb => tb.HasComment("Tổ (cắt / may / văn phòng)"));

            entity.HasIndex(e => e.TeamCode, "UQ_Team_Code").IsUnique();

            entity.HasIndex(e => e.TeamName, "UQ_Team_Name").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.TeamCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TeamName).HasMaxLength(100);
            entity.Property(e => e.TeamType)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TeamMealConfirmation>(entity =>
        {
            entity.ToTable("TeamMealConfirmation", tb =>
                {
                    tb.HasComment("Xác nhận suất ăn của tổ");
                    tb.HasTrigger("trg_TeamMeal_Cutoff");
                });

            entity.HasIndex(e => new { e.DailyMealId, e.TeamId }, "UQ_TMC").IsUnique();

            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.RemindedAt).HasPrecision(0);
            entity.Property(e => e.Source)
                .HasMaxLength(15)
                .IsUnicode(false);

            entity.HasOne(d => d.DailyMeal).WithMany(p => p.TeamMealConfirmations)
                .HasForeignKey(d => d.DailyMealId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TMC_DailyMeal");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMealConfirmations)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TMC_Team");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("Vehicle", tb => tb.HasComment("Xe"));

            entity.HasIndex(e => e.PlateNumber, "UQ_Vehicle_Plate").IsUnique();

            entity.Property(e => e.FuelNormPer100Km).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.PlateNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.VehicleType).HasMaxLength(50);
        });

        modelBuilder.Entity<VehicleTrip>(entity =>
        {
            entity.ToTable("VehicleTrip", tb => tb.HasComment("Chuyến xe & nhiên liệu"));

            entity.HasIndex(e => new { e.VehicleId, e.TripDate, e.OdometerStart }, "UQ_VT").IsUnique();

            entity.Property(e => e.DriverName).HasMaxLength(100);
            entity.Property(e => e.FuelCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FuelLiters).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.Purpose).HasMaxLength(300);
            entity.Property(e => e.RecordedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("STARTED");

            entity.HasOne(d => d.DispatchNote).WithMany(p => p.VehicleTrips)
                .HasForeignKey(d => d.DispatchNoteId)
                .HasConstraintName("FK_VT_Dispatch");

            entity.HasOne(d => d.DriverEmployee).WithMany(p => p.VehicleTrips)
                .HasForeignKey(d => d.DriverEmployeeId)
                .HasConstraintName("FK_VT_Driver");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleTrips)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VT_Vehicle");
        });

        modelBuilder.Entity<VwDailyMealTotal>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_DailyMealTotal");

            entity.Property(e => e.MealPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwKpiDailyResult>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_KpiDailyResult");

            entity.Property(e => e.DailyTarget).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DefectRatePct).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.PercentAchieved).HasColumnType("decimal(9, 2)");
        });

        modelBuilder.Entity<VwMaterialStock>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_MaterialStock");

            entity.Property(e => e.AvailableQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.Category)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.MaterialCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.MaterialName).HasMaxLength(200);
            entity.Property(e => e.MaterialType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.OnHandQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.ReservedQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.Unit).HasMaxLength(20);
        });

        modelBuilder.Entity<VwMonthlyRevenue>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_MonthlyRevenue");

            entity.Property(e => e.PeriodMonth).HasMaxLength(4000);
            entity.Property(e => e.Revenue).HasColumnType("decimal(38, 2)");
        });

        modelBuilder.Entity<VwOperationAllocationProgress>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OperationAllocationProgress");
        });

        modelBuilder.Entity<VwOrderLineProgress>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OrderLineProgress");

            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.DefectRatePct).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ProductCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.SizeCode)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwOrderMaterialDelivery>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OrderMaterialDelivery");

            entity.Property(e => e.CustomerQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.ReceivedQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.RemainingQty).HasColumnType("decimal(38, 3)");
        });

        modelBuilder.Entity<VwOrderMaterialNorm>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OrderMaterialNorm");

            entity.Property(e => e.DiffQty).HasColumnType("decimal(19, 3)");
            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.NormQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RequiredQty).HasColumnType("decimal(18, 3)");
        });

        modelBuilder.Entity<VwPayrollExportDatum>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PayrollExportData");

            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.GradeAllowance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GradeCode)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.OvertimeHours).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.PayType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PeriodMonth)
                .HasMaxLength(7)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.PieceRateAmount).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.RoleName).HasMaxLength(100);
            entity.Property(e => e.ScrapDeduction).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.SundayOvertimeHours).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.SundayPieceRateBonus).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.SundayWorkDays).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.TeamName).HasMaxLength(100);
            entity.Property(e => e.WorkDays).HasColumnType("decimal(38, 2)");
        });

        modelBuilder.Entity<VwProductLastSizeRatio>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ProductLastSizeRatio");

            entity.Property(e => e.SizeRatio)
                .HasMaxLength(8000)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwPurchaseOrderDelivery>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PurchaseOrderDelivery");

            entity.Property(e => e.GoodsStatus)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.PoCode)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwPurchaseOrderLineStatus>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PurchaseOrderLineStatus");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NotReplenishedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.OrderedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.OutstandingQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.ReceivedQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwStockBalance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_StockBalance");

            entity.Property(e => e.AvailableQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.OnHandQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.ReservedQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.WarehouseType)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwTeamActingLeader>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TeamActingLeader");

            entity.Property(e => e.DeputyName).HasMaxLength(100);
            entity.Property(e => e.TeamCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TeamType)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwTeamPieceHolding>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TeamPieceHolding");
        });

        modelBuilder.Entity<VwWorkOrderFabricNeed>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_WorkOrderFabricNeed");

            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.NeedQty).HasColumnType("decimal(18, 3)");
        });

        modelBuilder.Entity<VwWorkOrderLineProgress>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_WorkOrderLineProgress");

            entity.Property(e => e.MaterialColor).HasMaxLength(50);
            entity.Property(e => e.WorkOrderCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.WorkOrderStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwWorkerDailyEstimatedPay>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_WorkerDailyEstimatedPay");

            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.EstimatedPay).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.OutputAmount).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.ScrapAmount).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.SundayBonus).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwWorkerOutputCap>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_WorkerOutputCap");
        });

        modelBuilder.Entity<WorkAllocation>(entity =>
        {
            entity.ToTable("WorkAllocation", tb =>
                {
                    tb.HasComment("Giao / thu hồi việc với công nhân");
                    tb.HasTrigger("trg_WorkAllocation_Check");
                });

            entity.HasIndex(e => new { e.SewingTeamId, e.OrderLineId, e.OperationId, e.Status }, "IX_WA_Line");

            entity.HasIndex(e => new { e.WorkerId, e.OrderLineId, e.OperationId, e.Status }, "IX_WA_Worker");

            entity.Property(e => e.ConfirmMode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);
            entity.Property(e => e.MovementType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.OnBehalfReason).HasMaxLength(500);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RetrievalReason)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.Operation).WithMany(p => p.WorkAllocations)
                .HasForeignKey(d => d.OperationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WA_Operation");

            entity.HasOne(d => d.OrderLine).WithMany(p => p.WorkAllocations)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WA_OrderLine");

            entity.HasOne(d => d.SewingTeam).WithMany(p => p.WorkAllocations)
                .HasForeignKey(d => d.SewingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WA_Team");

            entity.HasOne(d => d.Worker).WithMany(p => p.WorkAllocations)
                .HasForeignKey(d => d.WorkerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WA_Worker");
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("WorkOrder", tb => tb.HasComment("Lệnh cắt"));

            entity.HasIndex(e => e.WorkOrderCode, "UQ_WorkOrder_Code").IsUnique();

            entity.Property(e => e.CompletedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("([dbo].[fn_VnNow]())");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("OPEN");
            entity.Property(e => e.WorkOrderCode)
                .HasMaxLength(30)
                .IsUnicode(false);

            entity.HasOne(d => d.CuttingTeam).WithMany(p => p.WorkOrders)
                .HasForeignKey(d => d.CuttingTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WO_Team");

            entity.HasOne(d => d.Order).WithMany(p => p.WorkOrders)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WO_Order");

            entity.HasOne(d => d.Product).WithMany(p => p.WorkOrders)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WO_Product");

            entity.HasOne(d => d.SourceRequest).WithMany(p => p.WorkOrders)
                .HasForeignKey(d => d.SourceRequestId)
                .HasConstraintName("FK_WO_Request");
        });

        modelBuilder.Entity<WorkOrderLine>(entity =>
        {
            entity.ToTable("WorkOrderLine", tb =>
                {
                    tb.HasComment("Dòng lệnh cắt");
                    tb.HasTrigger("trg_WorkOrderLine_Check");
                });

            entity.HasIndex(e => new { e.WorkOrderId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_WOL").IsUnique();

            entity.HasOne(d => d.OrderLine).WithMany(p => p.WorkOrderLines)
                .HasForeignKey(d => d.OrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WOL_OrderLine");

            entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.WorkOrderLines)
                .HasForeignKey(d => d.ProductComponentMaterialId)
                .HasConstraintName("FK_WOL_PCM");

            entity.HasOne(d => d.WorkOrder).WithMany(p => p.WorkOrderLines)
                .HasForeignKey(d => d.WorkOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WOL_WO");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
