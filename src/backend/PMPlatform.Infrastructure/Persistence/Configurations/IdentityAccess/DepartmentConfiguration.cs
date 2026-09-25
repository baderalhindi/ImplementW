using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("department", "identity_access");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.Property(e => e.DirectoryReference).HasMaxLength(200);
        builder.Property(e => e.IsActive).HasDatabaseDefault(true);

        builder.HasOne<Department>().WithMany().HasForeignKey(e => e.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
