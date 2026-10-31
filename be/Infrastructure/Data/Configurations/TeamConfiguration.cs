using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> entity)
    {
        entity.ToTable("Team", tb => tb.HasComment("Tổ (cắt / may / văn phòng)"));

        entity.HasIndex(e => e.TeamCode, "UQ_Team_Code").IsUnique();

        entity.HasIndex(e => e.TeamName, "UQ_Team_Name").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.TeamCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.TeamName).HasMaxLength(100);
        entity.Property(e => e.TeamType)
            .HasMaxLength(10)
            .IsUnicode(false);
    }
}
