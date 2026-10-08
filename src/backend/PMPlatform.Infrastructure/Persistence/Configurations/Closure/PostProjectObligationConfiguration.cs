using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Closure;

internal sealed class PostProjectObligationConfiguration : IEntityTypeConfiguration<PostProjectObligation>
{
    public void Configure(EntityTypeBuilder<PostProjectObligation> builder)
    {
        builder.ToTable("post_project_obligation", "closure");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Description, "description");

        // A project's obligations, most recently changed first; WF-10's readiness reads them all.
        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });

        // Recorded against exactly one case: the completion case, or a terminal closure case.
        builder.HasCheck("case", "(completion_case_id IS NULL) <> (closure_case_id IS NULL)");

        // Satisfied exactly when it was settled as SATISFIED.
        builder.HasCheck("satisfied", "(status = 'SATISFIED') = (satisfied_at IS NOT NULL)");

        builder.HasOne<CompletionCase>().WithMany().HasForeignKey(e => e.CompletionCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClosureCase>().WithMany().HasForeignKey(e => e.ClosureCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
