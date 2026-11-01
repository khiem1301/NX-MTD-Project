using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class GradeLevelCodeConfiguration : IEntityTypeConfiguration<GradeLevelCode>
{
    public void Configure(EntityTypeBuilder<GradeLevelCode> entity)
    {
        entity.HasKey(e => e.GradeCode);

        entity.ToTable("GradeLevelCode");

        entity.Property(e => e.GradeCode)
            .HasMaxLength(5)
            .IsUnicode(false);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.GradeName).HasMaxLength(50);
        entity.Property(e => e.Status)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
    }
}
