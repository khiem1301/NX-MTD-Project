using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> entity)
    {
        entity.ToTable("Attachment", tb => tb.HasComment("Tệp / ảnh đính kèm"));

        entity.HasIndex(e => new { e.OwnerType, e.OwnerId }, "IX_Attachment_Owner");

        entity.Property(e => e.ContentType)
            .HasMaxLength(100)
            .IsUnicode(false);
        entity.Property(e => e.FileName).HasMaxLength(255);
        entity.Property(e => e.FileUrl).HasMaxLength(500);
        entity.Property(e => e.IsCurrent).HasDefaultValue(true);
        entity.Property(e => e.OwnerType)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.Purpose)
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasDefaultValue("PHOTO");
        entity.Property(e => e.UploadedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
    }
}
