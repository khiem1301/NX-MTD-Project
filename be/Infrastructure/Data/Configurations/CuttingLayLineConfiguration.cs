using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class CuttingLayLineConfiguration : IEntityTypeConfiguration<CuttingLayLine>
{
    public void Configure(EntityTypeBuilder<CuttingLayLine> entity)
    {
        entity.ToTable("CuttingLayLine", tb =>
            {
                tb.HasComment("Số cắt theo size của bàn cắt");
                tb.HasTrigger("trg_CuttingLayLine_Check");
            });

        entity.HasIndex(e => new { e.CuttingLayId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_CLL").IsUnique();

        entity.Property(e => e.AdjustReason).HasMaxLength(500);

        entity.HasOne(d => d.CuttingLay).WithMany(p => p.CuttingLayLines)
            .HasForeignKey(d => d.CuttingLayId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CLL_Lay");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.CuttingLayLines)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CLL_OrderLine");

        entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.CuttingLayLines)
            .HasForeignKey(d => d.ProductComponentMaterialId)
            .HasConstraintName("FK_CLL_PCM");
    }
}
