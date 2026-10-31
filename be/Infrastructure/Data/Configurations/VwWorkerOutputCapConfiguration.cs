using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwWorkerOutputCapConfiguration : IEntityTypeConfiguration<VwWorkerOutputCap>
{
    public void Configure(EntityTypeBuilder<VwWorkerOutputCap> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_WorkerOutputCap");
    }
}
