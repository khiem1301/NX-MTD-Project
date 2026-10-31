using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class CustomerOrderConfiguration : IEntityTypeConfiguration<CustomerOrder>
{
    public void Configure(EntityTypeBuilder<CustomerOrder> entity)
    {
        entity.HasKey(e => e.OrderId);

        entity.ToTable("CustomerOrder", tb => tb.HasComment("Đơn hàng gia công"));

        entity.HasIndex(e => new { e.Status, e.DueDate }, "IX_CustomerOrder_Status");

        entity.HasIndex(e => e.OrderCode, "UQ_CustomerOrder_Code").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.IsDelayRisk).HasComputedColumnSql("(CONVERT([bit],case when [DelayRiskSince] IS NULL then (0) else (1) end))", false);
        entity.Property(e => e.MaterialStatus)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("AWAITING");
        entity.Property(e => e.Note).HasMaxLength(1000);
        entity.Property(e => e.OrderCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.Status)
            .HasMaxLength(25)
            .IsUnicode(false)
            .HasDefaultValue("NEW");
        entity.Property(e => e.StopReason).HasMaxLength(500);
        entity.Property(e => e.UpdatedAt).HasPrecision(0);

        entity.HasOne(d => d.Customer).WithMany(p => p.CustomerOrders)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CustomerOrder_Customer");
    }
}
