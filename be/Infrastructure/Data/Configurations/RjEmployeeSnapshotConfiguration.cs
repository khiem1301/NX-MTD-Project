using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class RjEmployeeSnapshotConfiguration : IEntityTypeConfiguration<RjEmployeeSnapshot>
{
    public void Configure(EntityTypeBuilder<RjEmployeeSnapshot> entity)
    {
        entity.ToTable("RjEmployeeSnapshot", tb =>
            tb.HasComment("Cache NV từ Ronald Jack Pro (mã chấm công + tên)"));

        entity.HasKey(e => e.AttendanceCode);

        entity.Property(e => e.FullName).HasMaxLength(200);
        entity.Property(e => e.RjEmployeeCode).HasMaxLength(50);
        entity.Property(e => e.SyncedAt).HasPrecision(0);
    }
}
