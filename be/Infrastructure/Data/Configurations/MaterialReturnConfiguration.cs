using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialReturnConfiguration : IEntityTypeConfiguration<MaterialReturn>
{
    public void Configure(EntityTypeBuilder<MaterialReturn> entity)
    {
        entity.ToTable("MaterialReturn", tb => tb.HasComment("Phiếu trả vải thừa"));

        entity.HasIndex(e => e.ReturnCode, "UQ_MaterialReturn_Code").IsUnique();

        entity.Property(e => e.ConfirmedAt).HasPrecision(0);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Note).HasMaxLength(500);
        entity.Property(e => e.RejectReason).HasMaxLength(500);
        entity.Property(e => e.ReturnCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("PENDING");

        entity.HasOne(d => d.ConfirmedByNavigation).WithMany(p => p.MaterialReturns)
            .HasForeignKey(d => d.ConfirmedBy)
            .HasConstraintName("FK_MRet_ConfirmedBy");

        entity.HasOne(d => d.MaterialIssue).WithMany(p => p.MaterialReturns)
            .HasForeignKey(d => d.MaterialIssueId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MRet_Issue");
    }
}
