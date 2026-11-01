using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class DispatchNoteConfiguration : IEntityTypeConfiguration<DispatchNote>
{
    public void Configure(EntityTypeBuilder<DispatchNote> entity)
    {
        entity.ToTable("DispatchNote", tb => tb.HasComment("Phiếu xuất kho thành phẩm (kiêm lệnh giao hàng)"));

        entity.HasIndex(e => new { e.OrderId, e.Status }, "IX_DN_Order");

        entity.HasIndex(e => e.DispatchCode, "UQ_DispatchNote_Code").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
        entity.Property(e => e.DispatchCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.DispatchedAt).HasPrecision(0);
        entity.Property(e => e.Note).HasMaxLength(1000);
        entity.Property(e => e.RejectReason).HasMaxLength(500);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.Status)
            .HasMaxLength(25)
            .IsUnicode(false)
            .HasDefaultValue("PENDING");

        entity.HasOne(d => d.DispatchedByNavigation).WithMany(p => p.DispatchNotes)
            .HasForeignKey(d => d.DispatchedBy)
            .HasConstraintName("FK_DN_DispatchedBy");

        entity.HasOne(d => d.Order).WithMany(p => p.DispatchNotes)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DN_Order");
    }
}
