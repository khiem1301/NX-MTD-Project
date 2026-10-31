using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> entity)
    {
        entity.ToTable("Customer", tb => tb.HasComment("Khách hàng (công ty mẹ)"));

        entity.HasIndex(e => e.CustomerName, "UQ_Customer_Name").IsUnique();

        entity.Property(e => e.CustomerName).HasMaxLength(200);
        entity.Property(e => e.DefaultAddress).HasMaxLength(500);
        entity.Property(e => e.Email)
            .HasMaxLength(100)
            .IsUnicode(false);
        entity.Property(e => e.Phone)
            .HasMaxLength(15)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
    }
}
