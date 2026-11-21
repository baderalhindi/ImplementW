using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class UserPasskeyConfiguration : IEntityTypeConfiguration<UserPasskey>
{
    public void Configure(EntityTypeBuilder<UserPasskey> builder)
    {
        builder.ToTable("user_passkey", "identity_access");
        builder.Property(e => e.CredentialId).HasMaxLength(1023);
        builder.HasIndex(e => e.CredentialId).IsUnique();
        builder.Property(e => e.PublicKey).HasMaxLength(2048);
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.HasIndex(e => e.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
