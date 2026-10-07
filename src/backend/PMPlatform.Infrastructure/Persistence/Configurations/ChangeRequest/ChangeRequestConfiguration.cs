using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ChangeRequest;

internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequestEntity>
{
    public void Configure(EntityTypeBuilder<ChangeRequestEntity> builder)
    {
        builder.ToTable("change_request", "change_request");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Justification, "justification");
        builder.HasNarrative(e => e.ScopeImpact, "scope_impact");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.IsContractualObligation).HasDatabaseDefault(false);

        // TASK-026 (indexing-strategy.md I-26 to I-28): SCR-105 in the project workspace and under ALL scope, and its status filter.
        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.UpdatedAt, e.Id });

        builder.HasCheck("revision_no", "revision_no >= 1");

        // A DRAFT is a request never submitted; IMPLEMENTED and CLOSED say when they happened, and only they.
        builder.HasCheck("submitted", "(status = 'DRAFT') = (submitted_at IS NULL)");
        builder.HasCheck("implemented", "(status IN ('IMPLEMENTED', 'CLOSED')) = (implemented_at IS NOT NULL)");
        builder.HasCheck("closed", "(status = 'CLOSED') = (closed_at IS NOT NULL)");

        // A contractual-obligation change is flagged as one, so it is band 3 (TASK-106).
        builder.HasCheck("contractual", "change_type <> 'CONTRACTUAL_OBLIGATION' OR is_contractual_obligation");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.RequestedGovernanceProfileItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
