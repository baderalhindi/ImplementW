using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Suspension;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Suspension;

internal sealed class SuspensionRequestConfiguration : IEntityTypeConfiguration<SuspensionRequest>
{
    /// <summary>
    /// WF-09 BR-SUS-004 and BR-SUS-005, for any writer: at most one request of each type per project that is not yet final. With at most
    /// one open suspension per project, that is one open suspension request per project and one open resumption request per suspension.
    /// </summary>
    public const string SingleOpenKey = "ix_suspension_request_open_project_id_request_type";

    public void Configure(EntityTypeBuilder<SuspensionRequest> builder)
    {
        builder.ToTable("suspension_request", "suspension");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Reason, "reason");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);

        // TASK-026 (indexing-strategy.md I-29, I-30, I-54): SCR-108 in the project workspace and under ALL scope, and its status filter.
        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.ProjectId, e.RequestType }, SingleOpenKey).IsUnique()
            .HasFilter("status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED')").HasDatabaseName(SingleOpenKey);

        builder.HasCheck("revision_no", "revision_no >= 1");

        // A DRAFT is a request never submitted; an EFFECTED one says when it took effect, and only it.
        builder.HasCheck("submitted", "(status = 'DRAFT') = (submitted_at IS NULL)");
        builder.HasCheck("effected", "(status = 'EFFECTED') = (effected_at IS NOT NULL)");

        // Past its requester a request has its effective date; a planned resumption belongs to a suspension, after its effective date.
        builder.HasCheck("effective_date", "status IN ('DRAFT', 'RETURNED') OR requested_effective_date IS NOT NULL");
        builder.HasCheck(
            "planned_resumption_date",
            "planned_resumption_date IS NULL OR (request_type = 'SUSPEND' AND (requested_effective_date IS NULL OR planned_resumption_date > requested_effective_date))");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
