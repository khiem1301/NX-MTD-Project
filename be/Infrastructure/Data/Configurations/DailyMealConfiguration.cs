using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class DailyMealConfiguration : IEntityTypeConfiguration<DailyMeal>
{
    public void Configure(EntityTypeBuilder<DailyMeal> entity)
    {
        entity.ToTable("DailyMeal", tb =>
            {
                tb.HasComment("Suất ăn ngày (gồm suất khách)");
                tb.HasTrigger("trg_DailyMeal_GuestCutoff");
            });

        entity.HasIndex(e => e.MealDate, "UQ_DailyMeal_Date").IsUnique();

        entity.Property(e => e.AutoClosedAt).HasPrecision(0);
        entity.Property(e => e.ClosedAt).HasPrecision(0);
        entity.Property(e => e.GuestNote).HasMaxLength(300);
        entity.Property(e => e.KitchenNotifiedAt).HasPrecision(0);
        entity.Property(e => e.MealPrice).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("OPEN");
    }
}
