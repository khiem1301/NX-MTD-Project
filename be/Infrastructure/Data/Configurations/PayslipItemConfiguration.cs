using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class PayslipItemConfiguration : IEntityTypeConfiguration<PayslipItem>
{
    public void Configure(EntityTypeBuilder<PayslipItem> entity)
    {
        entity.ToTable("PayslipItem", tb => tb.HasComment("Khoản trên phiếu lương"));

        entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.ItemGroup)
            .HasMaxLength(15)
            .IsUnicode(false);
        entity.Property(e => e.ItemName).HasMaxLength(100);

        entity.HasOne(d => d.Payslip).WithMany(p => p.PayslipItems)
            .HasForeignKey(d => d.PayslipId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PayslipItem_Payslip");
    }
}
