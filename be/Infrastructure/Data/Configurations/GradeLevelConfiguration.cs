using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class GradeLevelConfiguration : IEntityTypeConfiguration<GradeLevel>
{
    public void Configure(EntityTypeBuilder<GradeLevel> entity)
    {
        entity.ToTable("GradeLevel", tb => tb.HasComment("Bậc thợ A–D và tiền bậc theo ngày hiệu lực"));

        entity.HasIndex(e => new { e.GradeCode, e.EffectiveDate }, "UQ_GradeLevel").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.GradeCode)
            .HasMaxLength(5)
            .IsUnicode(false);
                    entity.Property(e => e.MonthlyAmount).HasColumnType("decimal(18, 2)");

        entity.HasOne(d => d.GradeCodeNavigation).WithMany(p => p.GradeLevels)
            .HasForeignKey(d => d.GradeCode)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_GradeLevel_Code");
    }
}
