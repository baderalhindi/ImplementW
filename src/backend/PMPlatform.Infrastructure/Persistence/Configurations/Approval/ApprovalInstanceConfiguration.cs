using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Approval;

internal sealed class ApprovalInstanceConfiguration : IEntityTypeConfiguration<ApprovalInstance>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters. A start that loses the race to another violates it.</summary>
    public const string SubjectRevisionKey = "ix_approval_instance_subject_revision";

    public void Configure(EntityTypeBuilder<ApprovalInstance> builder)
    {
        builder.ToTable("approval_instance", "approval");
        builder.Property(e => e.SubjectModule).HasMaxLength(50);
        builder.Property(e => e.SubjectType).HasMaxLength(100);
        builder.Property(e => e.RoutingKey).HasMaxLength(100);
        builder.Property(e => e.OutcomeIdempotencyKey).HasMaxLength(200);

        // ERD D-15: one run per subject revision; a returned subject comes back as a new revision.
        builder.HasIndex(e => new { e.SubjectModule, e.SubjectType, e.SubjectId, e.SubjectRevisionNo }).IsUnique().HasDatabaseName(SubjectRevisionKey);
        builder.HasIndex(e => e.OutcomeIdempotencyKey).IsUnique();

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.AuthorityConfigurationVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_approval_instance_authority_configuration_version");
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ScopeProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(e => e.ScopeDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalInstance>().WithMany().HasForeignKey(e => e.PreviousInstanceId).OnDelete(DeleteBehavior.Restrict);

        // indexing-strategy.md I-24: SCR-101 My Requests.
        builder.HasIndex(e => new { e.RequestedByUserId, e.RequestedAt, e.Id });

        // Every decision changes the run's row, so two decisions on one run cannot both commit.
        builder.HasRowVersion();
    }
}
