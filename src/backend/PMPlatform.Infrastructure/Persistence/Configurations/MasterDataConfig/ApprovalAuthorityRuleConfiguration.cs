using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ApprovalAuthorityRuleConfiguration : IEntityTypeConfiguration<ApprovalAuthorityRule>
{
    public void Configure(EntityTypeBuilder<ApprovalAuthorityRule> builder)
    {
        builder.ToTable("approval_authority_rule", "master_data_config");
        builder.Property(e => e.SubjectTypeCode).HasMaxLength(100);
        builder.Property(e => e.IsMandatory).HasDatabaseDefault(true);

        // ADR-016: three bands.
        builder.HasCheck("band_no", "band_no BETWEEN 1 AND 3");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.GovernanceProfileItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.ApproverRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
