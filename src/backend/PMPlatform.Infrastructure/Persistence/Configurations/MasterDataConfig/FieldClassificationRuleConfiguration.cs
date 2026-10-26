using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class FieldClassificationRuleConfiguration : IEntityTypeConfiguration<FieldClassificationRule>
{
    public void Configure(EntityTypeBuilder<FieldClassificationRule> builder)
    {
        builder.ToTable("field_classification_rule", "master_data_config");
        builder.Property(e => e.EntityCode).HasMaxLength(100);
        builder.Property(e => e.FieldCode).HasMaxLength(100);
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.EntityCode, e.FieldCode }).IsUnique();

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DataClassificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
