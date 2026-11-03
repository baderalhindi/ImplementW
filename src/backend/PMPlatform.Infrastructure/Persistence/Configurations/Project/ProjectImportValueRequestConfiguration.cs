using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectImportValueRequestConfiguration : IEntityTypeConfiguration<ProjectImportValueRequest>
{
    public void Configure(EntityTypeBuilder<ProjectImportValueRequest> builder)
    {
        builder.ToTable("project_import_value_request", "project");
        builder.HasRowVersion();
        builder.Property(e => e.Dimension).HasMaxLength(50);
        builder.Property(e => e.SourceText).HasMaxLength(200);
        builder.HasNarrative(e => e.ResolutionNote, "resolution_note");
        builder.HasCheck("resolution", "(status = 'OPEN') = (resolved_at IS NULL) AND (status = 'OPEN') = (resolved_by_user_id IS NULL)");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);

        // The administrators' queue of OPEN requests, oldest first (indexing-strategy I-65).
        builder.HasIndex(e => new { e.Status, e.CreatedAt, e.Id });
    }
}
