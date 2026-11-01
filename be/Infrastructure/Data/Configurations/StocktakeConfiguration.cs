using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class StocktakeConfiguration : IEntityTypeConfiguration<Stocktake>
{
    public void Configure(EntityTypeBuilder<Stocktake> entity)
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
    }
}
