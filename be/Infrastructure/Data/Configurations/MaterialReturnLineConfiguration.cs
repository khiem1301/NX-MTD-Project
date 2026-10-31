using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialReturnLineConfiguration : IEntityTypeConfiguration<MaterialReturnLine>
{
    public void Configure(EntityTypeBuilder<MaterialReturnLine> entity)
    {
        entity.ToTable("MaterialReturnLine", tb =>
            {
                tb.HasComment("Dòng phiếu trả vải thừa");
                tb.HasTrigger("trg_MaterialReturnLine_Check");
            });

        entity.HasIndex(e => new { e.MaterialReturnId, e.MaterialIssueLineId }, "UQ_MRetL").IsUnique();

        entity.Property(e => e.ConfirmedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);
        entity.Property(e => e.ReturnedQty).HasColumnType("decimal(18, 3)");

        entity.HasOne(d => d.MaterialIssueLine).WithMany(p => p.MaterialReturnLines)
            .HasForeignKey(d => d.MaterialIssueLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MRetL_IssueLine");

        entity.HasOne(d => d.MaterialReturn).WithMany(p => p.MaterialReturnLines)
            .HasForeignKey(d => d.MaterialReturnId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MRetL_Return");
    }
}
