using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> entity)
    {
        entity.HasKey(e => e.PeriodId);

        entity.ToTable("AccountingPeriod", tb => tb.HasComment("Kỳ kế toán (tháng), khóa / mở khóa"));

        entity.HasIndex(e => e.PeriodMonth, "UQ_AccountingPeriod").IsUnique();

        entity.Property(e => e.LockedAt).HasPrecision(0);
        entity.Property(e => e.PeriodMonth)
            .HasMaxLength(7)
            .IsUnicode(false)
            .IsFixedLength();
        entity.Property(e => e.Status)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("OPEN");
    }
}
