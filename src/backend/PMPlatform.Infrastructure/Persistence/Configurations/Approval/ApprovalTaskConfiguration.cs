using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Approval;

internal sealed class ApprovalTaskConfiguration : IEntityTypeConfiguration<ApprovalTask>
{
    public void Configure(EntityTypeBuilder<ApprovalTask> builder)
    {
        builder.ToTable("approval_task", "approval");
        builder.HasNarrative(e => e.DecisionReason, "decision_reason");

        // Named: the generated name exceeds PostgreSQL's 63 characters. One task per role per stage, escalations included.
        builder.HasIndex(e => new { e.ApprovalInstanceId, e.SequenceNo, e.AssignedRoleId }).IsUnique().HasDatabaseName("ix_approval_task_instance_stage_role");

        builder.HasOne<ApprovalInstance>().WithMany().HasForeignKey(e => e.ApprovalInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.AssignedRoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AssignedUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActingUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalDelegation>().WithMany().HasForeignKey(e => e.ApprovalDelegationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalTask>().WithMany().HasForeignKey(e => e.EscalatedToTaskId).OnDelete(DeleteBehavior.Restrict);

        // indexing-strategy.md I-22 and I-23: SCR-100, a user's tasks and a role's queue, earliest due first.
        builder.HasIndex(e => new { e.AssignedUserId, e.Status, e.DueAt, e.Id });
        builder.HasIndex(e => new { e.AssignedRoleId, e.Status, e.DueAt, e.Id });
    }
}
