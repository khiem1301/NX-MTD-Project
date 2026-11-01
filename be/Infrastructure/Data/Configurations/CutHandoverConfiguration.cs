using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class CutHandoverConfiguration : IEntityTypeConfiguration<CutHandover>
{
    public void Configure(EntityTypeBuilder<CutHandover> entity)
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
    }
}
