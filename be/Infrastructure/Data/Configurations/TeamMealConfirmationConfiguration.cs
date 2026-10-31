using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class TeamMealConfirmationConfiguration : IEntityTypeConfiguration<TeamMealConfirmation>
{
    public void Configure(EntityTypeBuilder<TeamMealConfirmation> entity)
    {
        entity.ToTable("TeamMealConfirmation", tb =>
            {
                tb.HasComment("Xác nhận suất ăn của tổ");
                tb.HasTrigger("trg_TeamMeal_Cutoff");
            });

        entity.HasIndex(e => new { e.DailyMealId, e.TeamId }, "UQ_TMC").IsUnique();

        entity.Property(e => e.ConfirmedAt).HasPrecision(0);
        entity.Property(e => e.RemindedAt).HasPrecision(0);
        entity.Property(e => e.Source)
            .HasMaxLength(15)
            .IsUnicode(false);

        entity.HasOne(d => d.DailyMeal).WithMany(p => p.TeamMealConfirmations)
            .HasForeignKey(d => d.DailyMealId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_TMC_DailyMeal");

        entity.HasOne(d => d.Team).WithMany(p => p.TeamMealConfirmations)
            .HasForeignKey(d => d.TeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_TMC_Team");
    }
}
