using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("AuditLog", tb => tb.HasComment("Nhật ký thay đổi"));

        entity.HasIndex(e => new { e.TableName, e.RecordId, e.ChangedAt }, "IX_AuditLog_Record");

        entity.Property(e => e.Action)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.ChangedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Reason).HasMaxLength(500);
        entity.Property(e => e.TableName)
            .HasMaxLength(50)
            .IsUnicode(false);

        entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.AuditLogs)
            .HasForeignKey(d => d.ChangedBy)
            .HasConstraintName("FK_AuditLog_ChangedBy");
    }
}
