using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ExternalParticipation;

internal sealed class ExternalContributionFieldConfiguration : IEntityTypeConfiguration<ExternalContributionField>
{
    public void Configure(EntityTypeBuilder<ExternalContributionField> builder)
    {
        builder.ToTable("external_contribution_field", "external_participation");
        builder.Property(e => e.FieldCode).HasMaxLength(100);
        builder.Property(e => e.ProposedValueLanguage).HasColumnName("proposed_value_lang");

        // ERD: one value per field of a revision.
        builder.HasIndex(e => new { e.ExternalContributionId, e.FieldCode }).IsUnique();

        builder.HasOne<ExternalContribution>().WithMany().HasForeignKey(e => e.ExternalContributionId).OnDelete(DeleteBehavior.Cascade);
    }
}
