using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> entity)
    {
        entity.ToTable("StockBalance", tb =>
            {
                tb.HasComment("Tồn hiện tại theo mặt hàng và kho");
                tb.HasTrigger("trg_StockBalance_Check");
            });

        entity.HasIndex(e => new { e.WarehouseType, e.MaterialId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_SB_Item").IsUnique();

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
    }
}
