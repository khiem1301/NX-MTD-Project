using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> entity)
    {
        entity.ToTable("Account", tb => tb.HasComment("Tài khoản đăng nhập (1–1 nhân viên)"));

        entity.HasIndex(e => e.EmployeeId, "UQ_Account_Employee").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.IsTemporaryPassword).HasDefaultValue(true);
        entity.Property(e => e.LockReason).HasMaxLength(300);
        entity.Property(e => e.LockedAt).HasPrecision(0);
        entity.Property(e => e.PasswordHash).HasMaxLength(500);
        entity.Property(e => e.SecurityStamp).HasDefaultValueSql("(newid())");
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");

        entity.HasOne(d => d.Employee).WithOne(p => p.Account)
            .HasForeignKey<Account>(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Account_Employee");
    }
}
