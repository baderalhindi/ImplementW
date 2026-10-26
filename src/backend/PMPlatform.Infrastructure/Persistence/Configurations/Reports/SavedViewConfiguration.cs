using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("saved_view", "reports");
        builder.HasNarrative(e => e.Name, "name");

        // I-53, SCR-139: a person's own views (at most a few each, sorted after the seek).
        builder.HasIndex(e => new { e.OwnerUserId, e.ViewType });

        // A report's parameter set names its report version; a composition names allowlist entries instead.
        builder.HasCheck("report", "(view_type = 'REPORT_PARAMETERS') = (report_definition_id IS NOT NULL)");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReportDefinition>().WithMany().HasForeignKey(e => e.ReportDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasRowVersion();
    }
}
