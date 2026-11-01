using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class PieceRateConfiguration : IEntityTypeConfiguration<PieceRate>
{
    public void Configure(EntityTypeBuilder<PieceRate> entity)
    {
        entity.ToTable("PieceRate", tb =>
            {
                tb.HasComment("Đơn giá công đoạn");
                tb.HasTrigger("trg_PieceRate_Approved");
            });

        entity.HasIndex(e => new { e.OperationId, e.EffectiveDate }, "UQ_PieceRate").IsUnique();

        entity.Property(e => e.ApprovedAt).HasPrecision(0);
        entity.Property(e => e.DecisionNote).HasMaxLength(500);
        entity.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("DRAFT");
        entity.Property(e => e.SubmittedAt).HasPrecision(0);

        entity.HasOne(d => d.Operation).WithMany(p => p.PieceRates)
            .HasForeignKey(d => d.OperationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PieceRate_Operation");
    }
}
