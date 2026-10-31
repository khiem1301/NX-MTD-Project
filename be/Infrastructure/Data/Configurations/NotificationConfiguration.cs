using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.ToTable("Notification", tb => tb.HasComment("Thông báo người dùng (giữ 90 ngày)"));

        entity.HasIndex(e => e.CreatedAt, "IX_Notification_CreatedAt");

        entity.HasIndex(e => new { e.RecipientId, e.CreatedAt }, "IX_Notification_Inbox").IsDescending(false, true);

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.IsRead).HasComputedColumnSql("(CONVERT([bit],case when [ReadAt] IS NULL then (0) else (1) end))", true);
        entity.Property(e => e.LinkUrl).HasMaxLength(500);
        entity.Property(e => e.Message).HasMaxLength(1000);
        entity.Property(e => e.PushStatus)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("PENDING");
        entity.Property(e => e.ReadAt).HasPrecision(0);
        entity.Property(e => e.RefTable)
            .HasMaxLength(50)
            .IsUnicode(false);
        entity.Property(e => e.SourceType)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.Title).HasMaxLength(200);

        entity.HasOne(d => d.Recipient).WithMany(p => p.Notifications)
            .HasForeignKey(d => d.RecipientId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Notification_Recipient");
    }
}
