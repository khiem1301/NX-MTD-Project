using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class StocktakeLineConfiguration : IEntityTypeConfiguration<StocktakeLine>
{
    public void Configure(EntityTypeBuilder<StocktakeLine> entity)
    {
        entity.ToTable("StocktakeLine", tb =>
            {
                tb.HasComment("Dòng kiểm kê");
                tb.HasTrigger("trg_StocktakeLine_Check");
            });

        entity.HasIndex(e => new { e.StocktakeId, e.StockBalanceId }, "UQ_STL").IsUnique();

        entity.Property(e => e.BookQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.CountedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.DifferenceQty)
            .HasComputedColumnSql("(isnull([CountedQty],[BookQty])-[BookQty])", false)
            .HasColumnType("decimal(19, 3)");
        entity.Property(e => e.Note).HasMaxLength(300);

        entity.HasOne(d => d.StockBalance).WithMany(p => p.StocktakeLines)
            .HasForeignKey(d => d.StockBalanceId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_STL_Balance");

        entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeLines)
            .HasForeignKey(d => d.StocktakeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_STL_Stocktake");
    }
}
