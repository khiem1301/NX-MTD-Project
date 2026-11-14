using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialReturnConfiguration : IEntityTypeConfiguration<MaterialReturn>
{
    public void Configure(EntityTypeBuilder<MaterialReturn> entity)
    {
        entity.ToTable("MaterialReturn", tb => tb.HasComment("Phiếu trả NVL / phế phẩm / BTP của tổ cắt, tổ may về kho"));

        entity.HasIndex(e => e.ReturnCode, "UQ_MaterialReturn_Code").IsUnique();

        entity.Property(e => e.ConfirmedAt).HasPrecision(0);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Note).HasMaxLength(500);
        entity.Property(e => e.RejectReason).HasMaxLength(TextLimits.Reason);
        entity.Property(e => e.ReturnCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("PENDING");
        entity.Property(e => e.WarehouseType)
            .HasMaxLength(15)
            .IsUnicode(false)
            .HasDefaultValue("MATERIAL");

        entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.MaterialReturns)
            .HasForeignKey(d => d.ConfirmedBy)
            .HasConstraintName("FK_MRet_ConfirmedBy");

        entity.HasOne(d => d.MaterialIssue).WithMany(p => p.MaterialReturns)
            .HasForeignKey(d => d.MaterialIssueId)
            .HasConstraintName("FK_MRet_Issue");

        entity.HasOne(d => d.ReturningTeam).WithMany()
            .HasForeignKey(d => d.ReturningTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MRet_Team");

        entity.HasOne(d => d.Order).WithMany()
            .HasForeignKey(d => d.OrderId)
            .HasConstraintName("FK_MRet_Order");

        entity.HasOne(d => d.WorkOrder).WithMany()
            .HasForeignKey(d => d.WorkOrderId)
            .HasConstraintName("FK_MRet_WorkOrder");
    }
}
