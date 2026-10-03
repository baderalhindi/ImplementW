using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ProjectTask;

internal sealed class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.ToTable("task_dependency", "project_task");
        builder.HasRowVersion();
        builder.HasIndex(e => new { e.PredecessorTaskId, e.SuccessorTaskId }).IsUnique();

        // Two distinct tasks. One project, live leaves and acyclicity are the guard trigger's (TASK-048_GuardProjectTask).
        builder.HasCheck("ends", "predecessor_task_id <> successor_task_id");

        builder.HasOne<ProjectTaskEntity>().WithMany().HasForeignKey(e => e.PredecessorTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectTaskEntity>().WithMany().HasForeignKey(e => e.SuccessorTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}
