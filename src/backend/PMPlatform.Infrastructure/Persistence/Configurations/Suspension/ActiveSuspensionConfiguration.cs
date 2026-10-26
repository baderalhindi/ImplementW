using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Suspension;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Suspension;

internal sealed class ActiveSuspensionConfiguration : IEntityTypeConfiguration<ActiveSuspension>
{
    /// <summary>The ERD's invariant (TASK-062, WF-09 BR-SUS-003): at most one open suspension per project, for any writer.</summary>
    public const string SingleOpenKey = "ix_active_suspension_open_project_id";

    public void Configure(EntityTypeBuilder<ActiveSuspension> builder)
    {
        builder.ToTable("active_suspension", "suspension");

        // Its row version serialises its end: two resumptions of one suspension cannot both commit.
        builder.HasRowVersion();
        builder.HasIndex(e => e.SuspensionRequestId).IsUnique();
        builder.HasIndex(e => e.ResumptionRequestId).IsUnique();
        builder.HasIndex(e => e.ProjectId, SingleOpenKey).IsUnique().HasFilter("ended_at IS NULL").HasDatabaseName(SingleOpenKey);

        // A project's suspension history, most recently started first (BR-SUS-039).
        builder.HasIndex(e => new { e.ProjectId, e.StartedAt, e.Id });

        // Ended never before it started, and exactly when a resumption or a closure (TASK-063) ended it: a resumption names its request, a
        // closure none. A period ended before TASK-063 has no reason recorded beside its resumption, so a missing reason is a resumption's.
        builder.HasCheck("ended", "(ended_at IS NULL OR ended_at >= started_at) AND (ended_at IS NULL) = (resumption_request_id IS NULL AND end_reason IS NULL)");
        builder.HasCheck("end_reason_request", "end_reason IS NULL OR (end_reason = 'RESUMED') = (resumption_request_id IS NOT NULL)");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SuspensionRequest>().WithMany().HasForeignKey(e => e.SuspensionRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SuspensionRequest>().WithMany().HasForeignKey(e => e.ResumptionRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
