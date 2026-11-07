using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using PMPlatform.Domain.Common;

using ProjectEntity = PMPlatform.Domain.Project.Project;

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

    private const string AuditSchema = "audit_activity";
    private const string ProjectAnchor = "ProjectId";
    private const string ProjectScope = "ScopeProjectId";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // ERD D-5: SAR only; one numeric(18,2) column, no currency column.
        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>().HavePrecision(18, 2);

        // ERD D-7: the entry language as 'ar' or 'en'.
        configurationBuilder.Properties<Language>().HaveConversion<LanguageCodeConverter>().HaveColumnType("char(2)");

        // Every other domain enum is a state or type column holding the upper-snake-case name (ERD §6).
        foreach (Type enumType in typeof(AuditedEntity).Assembly.GetTypes().Where(t => t.IsEnum && t != typeof(Language)))
        {
            configurationBuilder.Properties(enumType)
                .HaveConversion(typeof(EnumTextConverter<>).MakeGenericType(enumType))
                .HaveMaxLength(50);
        }

        configurationBuilder.Conventions.Add(_ => new ValueSetCheckConstraintConvention());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PMPlatformDbContext).Assembly);
        HideDeletedProjects(modelBuilder);
    }

    /// <summary>
    /// Client review, item 4: a deleted project and every record anchored on it leave all reads, in one place, so no register, dashboard,
    /// report, search or background job can miss it. The audit schema stays whole: it is the record of what happened, deletion included.
    /// </summary>
    private void HideDeletedProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>().HasQueryFilter(p => p.DeletedAt == null);

        foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes()
                     .Where(e => e.BaseType is null && !e.IsOwned() && e.GetSchema() != AuditSchema).ToList())
        {
            string? filter = entity.FindProperty(ProjectAnchor) is { } anchor
                ? anchor.IsNullable ? nameof(HideOptionallyAnchored) : nameof(HideAnchored)
                : entity.FindProperty(ProjectScope) is not null ? nameof(HideScoped) : null;
            if (filter is not null)
            {
                typeof(PMPlatformDbContext).GetMethod(filter, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entity.ClrType)
                    .Invoke(this, [modelBuilder]);
            }
        }
    }

    private void HideAnchored<T>(ModelBuilder modelBuilder)
        where T : class =>
        modelBuilder.Entity<T>().HasQueryFilter(e => Set<ProjectEntity>().Any(p => p.Id == EF.Property<Guid>(e, ProjectAnchor)));

    private void HideOptionallyAnchored<T>(ModelBuilder modelBuilder)
        where T : class =>
        modelBuilder.Entity<T>().HasQueryFilter(e => EF.Property<Guid?>(e, ProjectAnchor) == null
                                                     || Set<ProjectEntity>().Any(p => p.Id == EF.Property<Guid?>(e, ProjectAnchor)));

    private void HideScoped<T>(ModelBuilder modelBuilder)
        where T : class =>
        modelBuilder.Entity<T>().HasQueryFilter(e => EF.Property<Guid?>(e, ProjectScope) == null
                                                     || Set<ProjectEntity>().Any(p => p.Id == EF.Property<Guid?>(e, ProjectScope)));
}
