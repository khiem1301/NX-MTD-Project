using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> entity)
    {
        entity.ToTable("Operation", tb => tb.HasComment("Công đoạn"));

        entity.HasIndex(e => e.OperationGroupId, "IX_Operation_Group").HasFilter("([OperationGroupId] IS NOT NULL)");

        entity.HasIndex(e => new { e.ProductId, e.OperationCode }, "UQ_Operation").IsUnique();

        entity.HasIndex(e => new { e.OperationId, e.ProductId }, "UQ_Operation_IdProduct").IsUnique();

        entity.HasIndex(e => e.ProductId, "UX_Operation_FinalStep")
            .IsUnique()
            .HasFilter("([IsFinalStep]=(1) AND [Status]='ACTIVE')");

        entity.Property(e => e.OperationCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.OperationName).HasMaxLength(200);
        entity.Property(e => e.StandardTimeSec).HasColumnType("decimal(9, 2)");
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.TeamType)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("SEWING");

        entity.HasOne(d => d.Product).WithOne(p => p.Operation)
            .HasForeignKey<Operation>(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Operation_Product");

        entity.HasOne(d => d.OperationGroup).WithMany(p => p.Operations)
            .HasPrincipalKey(p => new { p.OperationGroupId, p.ProductId })
            .HasForeignKey(d => new { d.OperationGroupId, d.ProductId })
            .HasConstraintName("FK_Operation_Group");
    }
}
