using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductComponentConfiguration : IEntityTypeConfiguration<ProductComponent>
{
    public void Configure(EntityTypeBuilder<ProductComponent> entity)
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
    }
}
