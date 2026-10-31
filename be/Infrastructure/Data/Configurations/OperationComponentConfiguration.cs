using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OperationComponentConfiguration : IEntityTypeConfiguration<OperationComponent>
{
    public void Configure(EntityTypeBuilder<OperationComponent> entity)
    {
        entity.HasKey(e => new { e.OperationId, e.ProductComponentId });

        entity.ToTable("OperationComponent", tb => tb.HasComment("Công đoạn làm trên chi tiết nào"));

        entity.HasIndex(e => e.ProductComponentId, "IX_OC_Component");

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
    }
}
