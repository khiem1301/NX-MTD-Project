using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class BreakEvenResultConfiguration : IEntityTypeConfiguration<BreakEvenResult>
{
    public void Configure(EntityTypeBuilder<BreakEvenResult> entity)
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
    }
}
