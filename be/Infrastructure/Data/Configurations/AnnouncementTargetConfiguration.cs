using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AnnouncementTargetConfiguration : IEntityTypeConfiguration<AnnouncementTarget>
{
    public void Configure(EntityTypeBuilder<AnnouncementTarget> entity)
    {
        entity.ToTable("AnnouncementTarget", tb => tb.HasComment("Phạm vi nhận thông báo nhanh"));

        entity.HasOne(d => d.Announcement).WithMany(p => p.AnnouncementTargets)
            .HasForeignKey(d => d.AnnouncementId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_AnnTarget_Announcement");

        entity.HasOne(d => d.Role).WithMany(p => p.AnnouncementTargets)
            .HasForeignKey(d => d.RoleId)
            .HasConstraintName("FK_AnnTarget_Role");

        entity.HasOne(d => d.Team).WithMany(p => p.AnnouncementTargets)
            .HasForeignKey(d => d.TeamId)
            .HasConstraintName("FK_AnnTarget_Team");
    }
}
