using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectFollowUpNoteConfiguration : IEntityTypeConfiguration<ProjectFollowUpNote>
{
    public void Configure(EntityTypeBuilder<ProjectFollowUpNote> builder)
    {
        builder.ToTable("project_follow_up_note", "project");
        builder.HasNarrative(e => e.Note, "note");
        builder.IsAppendOnly();

        // A project's notes, newest first (indexing-strategy I-61).
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ProjectId, e.CreatedAt, e.Id });
    }
}
