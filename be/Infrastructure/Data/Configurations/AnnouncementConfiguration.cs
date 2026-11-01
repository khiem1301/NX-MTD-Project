using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> entity)
    {
        entity.ToTable("Announcement", tb => tb.HasComment("Thông báo nhanh"));

        entity.Property(e => e.Content).HasMaxLength(2000);
        entity.Property(e => e.Priority)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("NORMAL");
        entity.Property(e => e.SentAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.TargetScope)
            .HasMaxLength(10)
            .IsUnicode(false);

        entity.HasOne(d => d.Sender).WithMany(p => p.Announcements)
            .HasForeignKey(d => d.SenderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Announcement_Sender");
    }
}
