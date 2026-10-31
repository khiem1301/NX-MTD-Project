using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class GradeEvaluationConfiguration : IEntityTypeConfiguration<GradeEvaluation>
{
    public void Configure(EntityTypeBuilder<GradeEvaluation> entity)
    {
        entity.ToTable("GradeEvaluation", tb => tb.HasComment("Đề xuất / đổi bậc thợ; dòng APPLIED là lịch sử bậc"));

        entity.HasIndex(e => new { e.WorkerId, e.EffectiveDate }, "IX_GradeEval_History")
            .IsDescending(false, true)
            .HasFilter("([Status]='APPLIED')");

        entity.HasIndex(e => new { e.WorkerId, e.ProposedBy, e.EvaluationDate }, "UQ_GradeEval").IsUnique();

        entity.Property(e => e.ConsideredAt).HasPrecision(0);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.CurrentGradeCode)
            .HasMaxLength(5)
            .IsUnicode(false);
        entity.Property(e => e.DecisionNote).HasMaxLength(500);
        entity.Property(e => e.ProposedGradeCode)
            .HasMaxLength(5)
            .IsUnicode(false);
        entity.Property(e => e.Reason).HasMaxLength(500);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("SUBMITTED");

        entity.HasOne(d => d.CurrentGradeCodeNavigation).WithMany(p => p.GradeEvaluationCurrentGradeCodeNavigations)
            .HasForeignKey(d => d.CurrentGradeCode)
            .HasConstraintName("FK_GradeEval_Current");

        entity.HasOne(d => d.ProposedByNavigation).WithMany(p => p.GradeEvaluationProposedByNavigations)
            .HasForeignKey(d => d.ProposedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_GradeEval_ProposedBy");

        entity.HasOne(d => d.ProposedGradeCodeNavigation).WithMany(p => p.GradeEvaluationProposedGradeCodeNavigations)
            .HasForeignKey(d => d.ProposedGradeCode)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_GradeEval_Proposed");

        entity.HasOne(d => d.Team).WithMany(p => p.GradeEvaluations)
            .HasForeignKey(d => d.TeamId)
            .HasConstraintName("FK_GradeEval_Team");

        entity.HasOne(d => d.Worker).WithMany(p => p.GradeEvaluationWorkers)
            .HasForeignKey(d => d.WorkerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_GradeEval_Worker");
    }
}
