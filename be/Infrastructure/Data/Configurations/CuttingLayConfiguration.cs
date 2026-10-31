using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class CuttingLayConfiguration : IEntityTypeConfiguration<CuttingLay>
{
    public void Configure(EntityTypeBuilder<CuttingLay> entity)
    {
        entity.ToTable("CuttingLay", tb => tb.HasComment("Bàn cắt"));

        entity.HasIndex(e => new { e.CuttingTeamId, e.WorkDate, e.ShiftCode, e.LaySequence }, "UQ_CL").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Note).HasMaxLength(500);
        entity.Property(e => e.ShiftCode)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("RECORDED");

        entity.HasOne(d => d.CuttingTeam).WithMany(p => p.CuttingLays)
            .HasForeignKey(d => d.CuttingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CL_Team");

        entity.HasOne(d => d.FabricMaterial).WithMany(p => p.CuttingLays)
            .HasForeignKey(d => d.FabricMaterialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CL_Material");

        entity.HasOne(d => d.WorkOrder).WithMany(p => p.CuttingLays)
            .HasForeignKey(d => d.WorkOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CL_WO");
    }
}
