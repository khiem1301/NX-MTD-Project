using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OperationComponentConfiguration : IEntityTypeConfiguration<OperationComponent>
{
    public void Configure(EntityTypeBuilder<OperationComponent> entity)
    {
        entity.HasKey(e => new { e.OperationId, e.ProductComponentId, e.MaterialId });

        entity.ToTable("OperationComponent", tb => tb.HasComment("Công đoạn làm trên chi tiết vải nào, vải nào"));

        entity.HasIndex(e => e.ProductComponentId, "IX_OC_Component");
        entity.HasIndex(e => e.MaterialId, "IX_OC_Material");

        entity.HasOne(d => d.Operation).WithMany(p => p.OperationComponents)
            .HasPrincipalKey(p => new { p.OperationId, p.ProductId })
            .HasForeignKey(d => new { d.OperationId, d.ProductId })
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OC_Operation");

        entity.HasOne(d => d.ProductComponent).WithMany(p => p.OperationComponents)
            .HasPrincipalKey(p => new { p.ProductComponentId, p.ProductId })
            .HasForeignKey(d => new { d.ProductComponentId, d.ProductId })
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OC_Component");

        entity.HasOne(d => d.Material).WithMany()
            .HasForeignKey(d => d.MaterialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OC_Material");
    }
}
