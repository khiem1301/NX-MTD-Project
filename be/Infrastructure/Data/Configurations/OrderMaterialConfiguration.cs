using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OrderMaterialConfiguration : IEntityTypeConfiguration<OrderMaterial>
{
    public void Configure(EntityTypeBuilder<OrderMaterial> entity)
    {
        entity.ToTable("OrderMaterial", tb => tb.HasComment("NVL của đơn: cần bao nhiêu, khách cấp bao nhiêu, phải mua bao nhiêu"));

        entity.HasIndex(e => new { e.OrderId, e.MaterialId }, "UQ_OM").IsUnique();

        entity.Property(e => e.CustomerQty).HasColumnType("decimal(18, 3)");
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
    }
}
