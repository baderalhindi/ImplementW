using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportJobConfiguration : IEntityTypeConfiguration<ReportJob>
{
    public void Configure(EntityTypeBuilder<ReportJob> builder)
    {
        builder.ToTable("report_job", "reports");
        builder.Property(e => e.RequestSnapshot).HasColumnType("jsonb");
        builder.Property(e => e.FailureCode).HasMaxLength(100);

        // US-RPT-SYS-038: one job per requester and key, for any writer.
        builder.HasIndex(e => new { e.RequestedByUserId, e.IdempotencyKey }).IsUnique();

        // I-52, SCR-140 Export History: a person's jobs, the newest first; and the worker's queues, the oldest first.
        builder.HasIndex(e => new { e.RequestedByUserId, e.RequestedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.RequestedAt });

        // A report job names its report version; an explorer composition carries its fields in its request.
        builder.HasCheck("kind", "(kind = 'REPORT') = (report_definition_id IS NOT NULL)");

        // A safe code from the catalogue, and only on a FAILED job (FG-02 REP-011).
        builder.HasCheck("failure_code", "(status = 'FAILED') = (failure_code IS NOT NULL) AND (failure_code IS NULL OR failure_code ~ '^[A-Z][A-Z0-9_]*$')");

        builder.HasOne<ReportDefinition>().WithMany().HasForeignKey(e => e.ReportDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasRowVersion();
    }
}
