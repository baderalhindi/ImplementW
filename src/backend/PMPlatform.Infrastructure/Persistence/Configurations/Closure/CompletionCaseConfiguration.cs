using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Closure;

internal sealed class CompletionCaseConfiguration : IEntityTypeConfiguration<CompletionCase>
{
    /// <summary>WF-10 CLO-CC-05, for any writer: at most one completion case per project that is not yet final.</summary>
    public const string SingleOpenKey = "ix_completion_case_open_project_id";

    /// <summary>The ERD's partial unique index: one effective completion per project.</summary>
    public const string SingleEffectedKey = "ix_completion_case_effected_project_id";

    public void Configure(EntityTypeBuilder<CompletionCase> builder)
    {
        builder.ToTable("completion_case", "closure");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.CompletionNarrative, "completion_narrative");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);

        // SCR-111 in the project workspace and under ALL scope, and WF-10's activation pass.
        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.UpdatedAt, e.Id });
        builder.HasIndex(e => e.ProjectId, SingleOpenKey).IsUnique()
            .HasFilter(CloseoutCaseChecks.OpenFilter).HasDatabaseName(SingleOpenKey);
        builder.HasIndex(e => e.ProjectId, SingleEffectedKey).IsUnique()
            .HasFilter("status = 'EFFECTED'").HasDatabaseName(SingleEffectedKey);

        CloseoutCaseChecks.Apply(builder);

        // Past its requester a case has what review needs: the actual completion date and the Project Manager's narrative.
        builder.HasCheck("submission", "status IN ('DRAFT', 'RETURNED', 'WITHDRAWN') OR (actual_project_completion_date IS NOT NULL AND completion_narrative IS NOT NULL)");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
