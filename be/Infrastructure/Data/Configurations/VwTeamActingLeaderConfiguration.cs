using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwTeamActingLeaderConfiguration : IEntityTypeConfiguration<VwTeamActingLeader>
{
    public void Configure(EntityTypeBuilder<VwTeamActingLeader> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_TeamActingLeader");

        entity.Property(e => e.DeputyName).HasMaxLength(100);
        entity.Property(e => e.TeamCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.TeamType)
            .HasMaxLength(10)
            .IsUnicode(false);
    }
}
