using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialReturnLineConfiguration : IEntityTypeConfiguration<MaterialReturnLine>
{
    public void Configure(EntityTypeBuilder<MaterialReturnLine> entity)
    {
        entity.ToTable("MaterialReturnLine", tb =>
            {
                tb.HasComment("Dòng phiếu trả: NVL / phế phẩm hoặc BTP cả bộ của dòng đơn");
                tb.HasTrigger("trg_MaterialReturnLine_Check");
            });

        entity.HasIndex(e => new { e.MaterialReturnId, e.MaterialId }, "UQ_MRetL_Material")
            .IsUnique()
            .HasFilter("([MaterialId] IS NOT NULL)");
        entity.HasIndex(e => new { e.MaterialReturnId, e.OrderLineId }, "UQ_MRetL_OrderLine")
            .IsUnique()
            .HasFilter("([OrderLineId] IS NOT NULL)");

        entity.Property(e => e.ConfirmedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.DiscrepancyReason).HasMaxLength(TextLimits.Reason);
        entity.Property(e => e.ReturnedQty).HasColumnType("decimal(18, 3)");

        entity.HasOne(d => d.MaterialIssueLine).WithMany(p => p.MaterialReturnLines)
            .HasForeignKey(d => d.MaterialIssueLineId)
            .HasConstraintName("FK_MRetL_IssueLine");

        entity.HasOne(d => d.Material).WithMany()
            .HasForeignKey(d => d.MaterialId)
            .HasConstraintName("FK_MRetL_Material");

        entity.HasOne(d => d.OrderLine).WithMany()
            .HasForeignKey(d => d.OrderLineId)
            .HasConstraintName("FK_MRetL_OrderLine");

        entity.HasOne(d => d.MaterialReturn).WithMany(p => p.MaterialReturnLines)
            .HasForeignKey(d => d.MaterialReturnId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MRetL_Return");
    }
}
