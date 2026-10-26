using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Closure;

internal sealed class ReadinessCheckConfiguration : IEntityTypeConfiguration<ReadinessCheck>
{
    public void Configure(EntityTypeBuilder<ReadinessCheck> builder)
    {
        builder.ToTable("readiness_check", "closure");
        builder.IsAppendOnly();
        builder.Property(e => e.CheckCode).HasMaxLength(100);
        builder.HasNarrative(e => e.Detail, "detail");

        // A case's readiness, newest evaluation first.
        builder.HasIndex(e => new { e.CompletionCaseId, e.EvaluatedAt });
        builder.HasIndex(e => new { e.ClosureCaseId, e.EvaluatedAt });

        // Exactly one case is named (the ERD's CHECK).
        builder.HasCheck("case", "(completion_case_id IS NULL) <> (closure_case_id IS NULL)");

        // A pass is blocked by nothing and a failure by something; a waiver says who accepted the exception, and why.
        builder.HasCheck("blocking_count", "blocking_count >= 0 AND (result <> 'PASS' OR blocking_count = 0) AND (result <> 'FAIL' OR blocking_count > 0)");
        builder.HasCheck("waiver", "(result = 'WAIVED') = (waived_by_user_id IS NOT NULL) AND (result = 'WAIVED') = (detail IS NOT NULL)");

        builder.HasOne<CompletionCase>().WithMany().HasForeignKey(e => e.CompletionCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClosureCase>().WithMany().HasForeignKey(e => e.ClosureCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.WaivedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
