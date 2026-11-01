using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> entity)
    {
        entity.ToTable("StockTransaction", tb =>
            {
                tb.HasComment("Thẻ kho (ghi qua usp_Stock_Post)");
                tb.HasTrigger("trg_StockTransaction_AppendOnly");
                tb.HasTrigger("trg_StockTransaction_PostOnly");
            });

        entity.HasIndex(e => new { e.StockBalanceId, e.CreatedAt }, "IX_ST_Balance");

        entity.HasIndex(e => new { e.SourceDocType, e.SourceDocId, e.SourceLineId, e.StockBalanceId }, "UQ_ST_SourceLine").IsUnique();

        entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.QtyChange).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.SourceDocType)
            .HasMaxLength(30)
            .IsUnicode(false);

        entity.HasOne(d => d.StockBalance).WithMany(p => p.StockTransactions)
            .HasForeignKey(d => d.StockBalanceId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_ST_Balance");
    }
}
