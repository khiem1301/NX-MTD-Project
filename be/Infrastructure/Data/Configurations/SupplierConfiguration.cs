using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> entity)
    {
        entity.ToTable("Supplier", tb => tb.HasComment("Nhà cung cấp"));

        entity.HasIndex(e => e.SupplierName, "UQ_Supplier_Name").IsUnique();

        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Email)
            .HasMaxLength(100)
            .IsUnicode(false);
        entity.Property(e => e.MaterialTypes).HasMaxLength(200);
        entity.Property(e => e.Note).HasMaxLength(500);
        entity.Property(e => e.Phone)
            .HasMaxLength(15)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.SupplierName).HasMaxLength(200);
    }
}
