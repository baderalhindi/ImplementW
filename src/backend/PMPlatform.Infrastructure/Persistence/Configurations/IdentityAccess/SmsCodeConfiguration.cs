using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class SmsCodeConfiguration : IEntityTypeConfiguration<SmsCode>
{
    public void Configure(EntityTypeBuilder<SmsCode> builder)
    {
        builder.ToTable("sms_code", "identity_access");
        builder.Property(e => e.MobileNumber).HasMaxLength(20);
        builder.Property(e => e.CodeHash).HasMaxLength(64);
        builder.Property(e => e.FailedAttempts).HasDatabaseDefault(0);

        // The resend cooldown reads a person's latest code for a purpose.
        builder.HasIndex(e => new { e.UserId, e.Purpose, e.CreatedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
