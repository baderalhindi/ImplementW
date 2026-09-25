using Microsoft.EntityFrameworkCore;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// The platform's one EF Core context and the owner of its migration history (TASK-024). Each module maps its
/// tables into its own schema (M-13) through <see cref="IEntityTypeConfiguration{TEntity}"/> classes in this
/// assembly, which <see cref="OnModelCreating"/> picks up; the context itself names no table.
/// </summary>
public sealed class PMPlatformDbContext(DbContextOptions<PMPlatformDbContext> options) : DbContext(options)
{
    /// <summary>ERD D-17: the history table is infrastructure, so it lives in <c>common</c>, not in a module schema.</summary>
    public const string MigrationsHistorySchema = "common";

    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PMPlatformDbContext).Assembly);
    }
}
