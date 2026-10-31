using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialReceiptConfiguration : IEntityTypeConfiguration<MaterialReceipt>
{
    public void Configure(EntityTypeBuilder<MaterialReceipt> entity)
    {
        entity.ToTable("MaterialReceipt", tb => tb.HasComment("Phiếu nhập NVL (mới / bổ sung)"));

        entity.HasIndex(e => new { e.OrderId, e.Status }, "IX_MR_Order");

        entity.HasIndex(e => e.ReceiptCode, "UQ_MaterialReceipt_Code").IsUnique();

        entity.Property(e => e.ConfirmedAt).HasPrecision(0);
        entity.Property(e => e.Note).HasMaxLength(1000);
        entity.Property(e => e.ReceiptCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.ReturnReason).HasMaxLength(500);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.Source)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasComputedColumnSql("(CONVERT([varchar](10),case when [PurchaseOrderId] IS NULL then 'CUSTOMER' else 'SUPPLIER' end))", true);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("PENDING");
        entity.Property(e => e.SubmittedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");

        entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.MaterialReceipts)
            .HasForeignKey(d => d.ConfirmedBy)
            .HasConstraintName("FK_MR_ConfirmedBy");

        entity.HasOne(d => d.Order).WithMany(p => p.MaterialReceipts)
            .HasForeignKey(d => d.OrderId)
            .HasConstraintName("FK_MR_Order");

        entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.MaterialReceipts)
            .HasForeignKey(d => d.PurchaseOrderId)
            .HasConstraintName("FK_MR_PO");

        entity.HasOne(d => d.ShortageRequest).WithMany(p => p.MaterialReceipts)
            .HasForeignKey(d => d.ShortageRequestId)
            .HasConstraintName("FK_MR_ShortageRequest");
    }
}
