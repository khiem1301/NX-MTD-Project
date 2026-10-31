using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class RequestLineConfiguration : IEntityTypeConfiguration<RequestLine>
{
    public void Configure(EntityTypeBuilder<RequestLine> entity)
    {
        entity.ToTable("RequestLine", tb =>
            {
                tb.HasComment("Dòng chi tiết đơn từ");
                tb.HasTrigger("trg_RequestLine_Check");
            });

        entity.Property(e => e.ItemDescription).HasMaxLength(300);
        entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.ReplenishQty).HasColumnType("decimal(18, 3)");

        entity.HasOne(d => d.Material).WithMany(p => p.RequestLines)
            .HasForeignKey(d => d.MaterialId)
            .HasConstraintName("FK_RequestLine_Material");

        entity.HasOne(d => d.MaterialReceiptLine).WithMany(p => p.RequestLines)
            .HasForeignKey(d => d.MaterialReceiptLineId)
            .HasConstraintName("FK_RequestLine_ReceiptLine");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.RequestLines)
            .HasForeignKey(d => d.OrderLineId)
            .HasConstraintName("FK_RequestLine_OrderLine");

        entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.RequestLines)
            .HasForeignKey(d => d.ProductComponentMaterialId)
            .HasConstraintName("FK_RequestLine_PCM");

        entity.HasOne(d => d.Request).WithMany(p => p.RequestLines)
            .HasForeignKey(d => d.RequestId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_RequestLine_Request");
    }
}
