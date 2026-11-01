using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MonthlyCostConfiguration : IEntityTypeConfiguration<MonthlyCost>
{
    public void Configure(EntityTypeBuilder<MonthlyCost> entity)
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
    }
}
