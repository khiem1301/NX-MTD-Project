using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwWorkOrderLineProgressConfiguration : IEntityTypeConfiguration<VwWorkOrderLineProgress>
{
    public void Configure(EntityTypeBuilder<VwWorkOrderLineProgress> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_WorkOrderLineProgress");

        entity.Property(e => e.WorkOrderCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.WorkOrderStatus)
            .HasMaxLength(20)
            .IsUnicode(false);
    }
}
