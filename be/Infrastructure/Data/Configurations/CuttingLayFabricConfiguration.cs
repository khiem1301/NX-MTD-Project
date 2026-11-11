using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class CuttingLayFabricConfiguration : IEntityTypeConfiguration<CuttingLayFabric>
{
    public void Configure(EntityTypeBuilder<CuttingLayFabric> entity)
    {
        entity.ToTable("CuttingLayFabric", tb => tb.HasComment("Số mét từng vải của bàn cắt"));

        entity.HasIndex(e => new { e.CuttingLayId, e.MaterialId }, "UQ_CLF").IsUnique();

        entity.Property(e => e.UsedM).HasColumnType("decimal(9, 2)");

        entity.HasOne(d => d.CuttingLay).WithMany(p => p.CuttingLayFabrics)
            .HasForeignKey(d => d.CuttingLayId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CLF_Lay");

        entity.HasOne(d => d.Material).WithMany(p => p.CuttingLayFabrics)
            .HasForeignKey(d => d.MaterialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CLF_Material");
    }
}
