using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class DispatchNoteLineConfiguration : IEntityTypeConfiguration<DispatchNoteLine>
{
    public void Configure(EntityTypeBuilder<DispatchNoteLine> entity)
    {
        entity.ToTable("DispatchNoteLine", tb =>
            {
                tb.HasComment("Dòng phiếu xuất kho");
                tb.HasTrigger("trg_DispatchNoteLine_Check");
            });

        entity.HasIndex(e => new { e.DispatchNoteId, e.OrderLineId }, "UQ_DNL").IsUnique();

        entity.HasOne(d => d.DispatchNote).WithMany(p => p.DispatchNoteLines)
            .HasForeignKey(d => d.DispatchNoteId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DNL_DN");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.DispatchNoteLines)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DNL_OrderLine");
    }
}
