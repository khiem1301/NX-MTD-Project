using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.ToTable("Employee", tb =>
            {
                tb.HasComment("Nhân viên");
                tb.HasTrigger("trg_Employee_TeamLeadership");
            });

        entity.HasIndex(e => e.TeamId, "IX_Employee_Team");

        entity.HasIndex(e => e.EmployeeCode, "UQ_Employee_Code").IsUnique();

        entity.HasIndex(e => e.AttendanceCode, "UX_Employee_AttendanceCode")
            .IsUnique()
            .HasFilter("([AttendanceCode] IS NOT NULL)");

        entity.Property(e => e.AvatarUrl).HasMaxLength(500);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.CurrentGradeCode)
            .HasMaxLength(5)
            .IsUnicode(false);
        entity.Property(e => e.EmployeeCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.FullName).HasMaxLength(100);
        entity.Property(e => e.Gender)
            .HasMaxLength(1)
            .IsUnicode(false)
            .IsFixedLength();
        entity.Property(e => e.PayType)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.Phone)
            .HasMaxLength(15)
            .IsUnicode(false);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.UpdatedAt).HasPrecision(0);

        entity.HasOne(d => d.CurrentGradeCodeNavigation).WithMany(p => p.Employees)
            .HasForeignKey(d => d.CurrentGradeCode)
            .HasConstraintName("FK_Employee_Grade");

        entity.HasOne(d => d.Role).WithMany(p => p.Employees)
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Employee_Role");

        entity.HasOne(d => d.Team).WithMany(p => p.Employees)
            .HasForeignKey(d => d.TeamId)
            .HasConstraintName("FK_Employee_Team");
    }
}
