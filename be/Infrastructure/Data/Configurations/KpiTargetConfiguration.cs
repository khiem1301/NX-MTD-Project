using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class KpiTargetConfiguration : IEntityTypeConfiguration<KpiTarget>
{
    public void Configure(EntityTypeBuilder<KpiTarget> entity)
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
    }
}
