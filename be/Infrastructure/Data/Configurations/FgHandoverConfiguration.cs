using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class FgHandoverConfiguration : IEntityTypeConfiguration<FgHandover>
{
    public void Configure(EntityTypeBuilder<FgHandover> entity)
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
    }
}
