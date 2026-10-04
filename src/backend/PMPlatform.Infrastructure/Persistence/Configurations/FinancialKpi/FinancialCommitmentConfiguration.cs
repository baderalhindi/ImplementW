using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class FinancialCommitmentConfiguration : IEntityTypeConfiguration<FinancialCommitment>
{
    /// <summary>The ERD's invariant: one ACTIVE version per project and type — the budget of record — for any writer.</summary>
    public const string SingleActiveKey = "ix_financial_commitment_active_project_id_commitment_type";

    /// <summary>At most one version per project and type on its way, DRAFT, SUBMITTED, UNDER_REVIEW or RETURNED (financial-kpi.md D-5).</summary>
    public const string SingleOpenKey = "ix_financial_commitment_open_project_id_commitment_type";

    public void Configure(EntityTypeBuilder<FinancialCommitment> builder)
    {
        builder.ToTable("financial_commitment", "financial_kpi");
        builder.HasRowVersion();
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.SourceReference).HasMaxLength(200);
        builder.HasIndex(e => new { e.ProjectId, e.CommitmentType, e.VersionNo }).IsUnique();
        builder.HasIndex(e => new { e.ProjectId, e.CommitmentType }, SingleActiveKey).IsUnique().HasFilter("status = 'ACTIVE'").HasDatabaseName(SingleActiveKey);
        builder.HasIndex(e => new { e.ProjectId, e.CommitmentType }, SingleOpenKey).IsUnique()
            .HasFilter("status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED')").HasDatabaseName(SingleOpenKey);

        builder.HasCheck("numbers", "version_no >= 1 AND revision_no >= 1");
        builder.HasCheck("amount", "amount_sar >= 0");

        // ADR-008 provenance: a figure from a source system names its reference there.
        builder.HasCheck("source_reference", "source_type = 'MANUAL' OR source_reference IS NOT NULL");

        // The workflow's facts: activated exactly once it has been ACTIVE; superseded by another version exactly while SUPERSEDED;
        // a DECLARED_BUDGET is the intake's, and only it.
        builder.HasCheck("activated", "(status IN ('ACTIVE', 'SUPERSEDED')) = (activated_at IS NOT NULL)");
        builder.HasCheck(
            "superseded",
            "(status = 'SUPERSEDED') = (superseded_by_commitment_id IS NOT NULL) AND (superseded_by_commitment_id IS NULL OR superseded_by_commitment_id <> id)");
        builder.HasCheck("intake", "(commitment_type = 'DECLARED_BUDGET') = (project_intake_id IS NOT NULL)");

        builder.HasOne<FinancialCommitment>().WithMany().HasForeignKey(e => e.SupersededByCommitmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectIntake>().WithMany().HasForeignKey(e => e.ProjectIntakeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EnteredByUserId).OnDelete(DeleteBehavior.Restrict);

        // change_authorization_id references change_request.change_authorization, which TASK-060 creates; TASK-060 adds the key
        // (financial-kpi.md F-3).
    }
}
