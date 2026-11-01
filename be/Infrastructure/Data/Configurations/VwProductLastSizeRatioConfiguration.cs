using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwProductLastSizeRatioConfiguration : IEntityTypeConfiguration<VwProductLastSizeRatio>
{
    public void Configure(EntityTypeBuilder<VwProductLastSizeRatio> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_ProductLastSizeRatio");

        entity.Property(e => e.SizeRatio)
            .HasMaxLength(8000)
            .IsUnicode(false);
    }
}
