using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MealRegistrationConfiguration : IEntityTypeConfiguration<MealRegistration>
{
    public void Configure(EntityTypeBuilder<MealRegistration> entity)
    {
        entity.ToTable("MealRegistration", tb =>
            {
                tb.HasComment("Suất ăn theo nhân viên (tick từ chấm công)");
                tb.HasTrigger("trg_MealRegistration_Cutoff");
            });

        entity.HasIndex(e => new { e.DailyMealId, e.EmployeeId }, "UQ_MR").IsUnique();

        entity.HasIndex(e => new { e.DailyMealId, e.TeamId }, "IX_MR_DailyMeal_Team");

        entity.Property(e => e.UpdatedAt).HasPrecision(0);
        entity.Property(e => e.WillEat).HasDefaultValue(true);

        entity.HasOne(d => d.DailyMeal).WithMany(p => p.MealRegistrations)
            .HasForeignKey(d => d.DailyMealId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MR_DailyMeal");

        entity.HasOne(d => d.Employee).WithMany()
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MR_Employee");

        entity.HasOne(d => d.Team).WithMany()
            .HasForeignKey(d => d.TeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MR_Team");

        entity.HasOne(d => d.UpdatedByEmployee).WithMany()
            .HasForeignKey(d => d.UpdatedByEmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MR_UpdatedBy");
    }
}
