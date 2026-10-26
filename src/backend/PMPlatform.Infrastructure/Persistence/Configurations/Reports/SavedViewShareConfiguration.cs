using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class SavedViewShareConfiguration : IEntityTypeConfiguration<SavedViewShare>
{
    public void Configure(EntityTypeBuilder<SavedViewShare> builder)
    {
        builder.ToTable("saved_view_share", "reports");

        // A view's shares, and (through the foreign key's index on shared_with_role_id) the views shared with a role: SCR-139 (I-53, P-1).
        builder.HasIndex(e => new { e.SavedViewId, e.SharedWithRoleId }).IsUnique();

        builder.HasOne<SavedView>().WithMany().HasForeignKey(e => e.SavedViewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.SharedWithRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
