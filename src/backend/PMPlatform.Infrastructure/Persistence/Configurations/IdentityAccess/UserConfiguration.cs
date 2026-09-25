using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user", "identity_access");
        builder.Property(e => e.DirectorySubjectId).HasMaxLength(200);
        builder.HasIndex(e => e.DirectorySubjectId).IsUnique();
        builder.Property(e => e.Username).HasMaxLength(100);
        builder.HasIndex(e => e.Username).IsUnique();
        builder.Property(e => e.DisplayName).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.MobileNumber).HasMaxLength(20);
        builder.Property(e => e.JobTitle).HasMaxLength(200);
        builder.Property(e => e.PreferredLanguage).HasDatabaseDefault(Language.Ar);
        builder.Property(e => e.NafathVerificationReference).HasMaxLength(200);

        // ADR-004: E.164, a plus sign and up to fifteen digits.
        builder.HasCheck("mobile_number", "mobile_number ~ '^\\+[1-9][0-9]{1,14}$'");
        builder.HasCheck("external_entity", "user_type <> 'EXTERNAL' OR external_entity_id IS NOT NULL");

        builder.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalEntity>().WithMany().HasForeignKey(e => e.ExternalEntityId).OnDelete(DeleteBehavior.Restrict);
    }
}
