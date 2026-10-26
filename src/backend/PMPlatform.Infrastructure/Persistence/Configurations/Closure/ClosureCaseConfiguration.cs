using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Closure;

internal sealed class ClosureCaseConfiguration : IEntityTypeConfiguration<ClosureCase>
{
    /// <summary>WF-10 CLO-CC-06, for any writer: at most one closure case per project that is not yet final.</summary>
    public const string SingleOpenKey = "ix_closure_case_open_project_id";

    /// <summary>One effective closure per project: CLOSED is terminal.</summary>
    public const string SingleEffectedKey = "ix_closure_case_effected_project_id";

    public void Configure(EntityTypeBuilder<ClosureCase> builder)
    {
        builder.ToTable("closure_case", "closure");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.ClosureNarrative, "closure_narrative");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Ignore(e => e.Outcome);

        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.UpdatedAt, e.Id });
        builder.HasIndex(e => e.ProjectId, SingleOpenKey).IsUnique()
            .HasFilter(CloseoutCaseChecks.OpenFilter).HasDatabaseName(SingleOpenKey);
        builder.HasIndex(e => e.ProjectId, SingleEffectedKey).IsUnique()
            .HasFilter("status = 'EFFECTED'").HasDatabaseName(SingleEffectedKey);

        CloseoutCaseChecks.Apply(builder);

        // Past its requester a case has its final summary — on the terminal path, the justification for stopping (WF-10 §9).
        builder.HasCheck("submission", "status IN ('DRAFT', 'RETURNED', 'WITHDRAWN') OR closure_narrative IS NOT NULL");

        // The terminal path names no completion; the normal path names the completion it follows (BR-CLO-003, DCL-CLO-13).
        builder.HasOne<CompletionCase>().WithMany().HasForeignKey(e => e.CompletionCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
