using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwDailyMealTotalConfiguration : IEntityTypeConfiguration<VwDailyMealTotal>
{
    public void Configure(EntityTypeBuilder<VwDailyMealTotal> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_DailyMealTotal");

        entity.Property(e => e.MealPrice).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
    }
}
