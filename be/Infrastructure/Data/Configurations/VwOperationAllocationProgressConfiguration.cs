using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwOperationAllocationProgressConfiguration : IEntityTypeConfiguration<VwOperationAllocationProgress>
{
    public void Configure(EntityTypeBuilder<VwOperationAllocationProgress> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_OperationAllocationProgress");
    }
}
