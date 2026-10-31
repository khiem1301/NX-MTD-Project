using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AttendanceSyncSessionConfiguration : IEntityTypeConfiguration<AttendanceSyncSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSyncSession> entity)
    {
        entity.HasKey(e => e.SyncSessionId);

        entity.ToTable("AttendanceSyncSession", tb => tb.HasComment("Lần đồng bộ RJ / import Excel chấm công"));

        entity.HasIndex(e => e.StartedAt, "IX_ASS_StartedAt").IsDescending();

        entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
        entity.Property(e => e.FileName).HasMaxLength(300);
        entity.Property(e => e.FinishedAt).HasPrecision(0);
        entity.Property(e => e.Source)
            .HasMaxLength(15)
            .IsUnicode(false);
        entity.Property(e => e.StartedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Status)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("RUNNING");
    }
}
