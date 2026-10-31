using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialIssueLineConfiguration : IEntityTypeConfiguration<MaterialIssueLine>
{
    public void Configure(EntityTypeBuilder<MaterialIssueLine> entity)
    {
        entity.ToTable("MaterialIssueLine", tb =>
            {
                tb.HasComment("Dòng phiếu cấp kho");
                tb.HasTrigger("trg_MaterialIssueLine_Check");
            });

        entity.HasIndex(e => new { e.MaterialIssueId, e.MaterialId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_MIL").IsUnique();

        entity.Property(e => e.ConfirmedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);
        entity.Property(e => e.IssuedQty).HasColumnType("decimal(18, 3)");

        entity.HasOne(d => d.Material).WithMany(p => p.MaterialIssueLines)
            .HasForeignKey(d => d.MaterialId)
            .HasConstraintName("FK_MIL_Material");

        entity.HasOne(d => d.MaterialIssue).WithMany(p => p.MaterialIssueLines)
            .HasForeignKey(d => d.MaterialIssueId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MIL_Issue");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.MaterialIssueLines)
            .HasForeignKey(d => d.OrderLineId)
            .HasConstraintName("FK_MIL_OrderLine");

        entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.MaterialIssueLines)
            .HasForeignKey(d => d.ProductComponentMaterialId)
            .HasConstraintName("FK_MIL_PCM");
    }
}
