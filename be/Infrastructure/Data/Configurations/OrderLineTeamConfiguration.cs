using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OrderLineTeamConfiguration : IEntityTypeConfiguration<OrderLineTeam>
{
    public void Configure(EntityTypeBuilder<OrderLineTeam> entity)
    {
        entity.ToTable("OrderLineTeam", tb => tb.HasComment("Chia dòng đơn (mã hàng × màu × size) cho tổ may"));

        entity.HasIndex(e => new { e.OrderLineId, e.SewingTeamId }, "UQ_OrderLineTeam").IsUnique();
        entity.HasIndex(e => new { e.SewingTeamId, e.OrderLineId }, "IX_OLT_Team");

        entity.Property(e => e.AssignedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");

        // Đơn còn NEW được bỏ dòng; bỏ dòng thì việc chia của dòng đó cũng hết tác dụng.
        entity.HasOne(d => d.OrderLine).WithMany()
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_OLT_OrderLine");

        entity.HasOne(d => d.SewingTeam).WithMany()
            .HasForeignKey(d => d.SewingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OLT_Team");
    }
}
