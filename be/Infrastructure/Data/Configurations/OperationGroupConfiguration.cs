using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OperationGroupConfiguration : IEntityTypeConfiguration<OperationGroup>
{
    public void Configure(EntityTypeBuilder<OperationGroup> entity)
    {
        entity.ToTable("OperationGroup", tb => tb.HasComment("Công đoạn to (nhóm công đoạn)"));

        entity.HasIndex(e => e.GroupName, "IX_OG_Name");

        entity.HasIndex(e => new { e.ProductId, e.GroupName }, "UQ_OG_Name").IsUnique();

        entity.HasIndex(e => new { e.OperationGroupId, e.ProductId }, "UQ_OG_Product").IsUnique();

        entity.Property(e => e.GroupName).HasMaxLength(200);

        entity.HasOne(d => d.Product).WithMany(p => p.OperationGroups)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OG_Product");
    }
}
