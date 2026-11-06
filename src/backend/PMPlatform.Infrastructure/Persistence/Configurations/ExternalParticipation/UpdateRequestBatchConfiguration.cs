using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ExternalParticipation;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ExternalParticipation;

internal sealed class UpdateRequestBatchConfiguration : IEntityTypeConfiguration<UpdateRequestBatch>
{
    public void Configure(EntityTypeBuilder<UpdateRequestBatch> builder)
    {
        builder.ToTable("update_request_batch", "external_participation");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Instructions, "instructions");
        builder.HasCheck("repeat_day", "repeat_day IS NULL OR repeat_day BETWEEN 1 AND 28");
        builder.HasCheck("repeat", "repeat_monthly = (repeat_day IS NOT NULL)");
        builder.HasIndex(e => new { e.UpdatedAt, e.Id });
        builder.HasIndex(e => e.NextRunOn).HasFilter("next_run_on IS NOT NULL");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
