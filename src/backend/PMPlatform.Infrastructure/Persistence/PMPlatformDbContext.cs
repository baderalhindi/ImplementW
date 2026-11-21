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
    private const string DeletedFilter = "Deleted";
    private const string DeletedProjectFilter = "DeletedProject";
    private const string DeletedParentFilter = "DeletedParent";

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
    /// Client review, items 4 and 8: a deleted record leaves every read, in one place, so no register, dashboard, report, search or
    /// background job can miss it. Three named filters, each switched off together by <c>IgnoreQueryFilters()</c>:
    /// <see cref="DeletedFilter"/> on every <see cref="IDeletable"/>; <see cref="DeletedProjectFilter"/> on everything anchored on a project;
    /// <see cref="DeletedParentFilter"/> on the records that belong to a deletable one. The audit schema stays whole: it is the record of what
    /// happened, deletion included.
    /// </summary>
    private void HideDeletedProjects(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes()
                     .Where(e => e.BaseType is null && !e.IsOwned() && e.GetSchema() != AuditSchema).ToList())
        {
            string? filter = entity.FindProperty(ProjectAnchor) is { } anchor
                ? anchor.IsNullable ? nameof(HideOptionallyAnchored) : nameof(HideAnchored)
                : entity.FindProperty(ProjectScope) is not null ? nameof(HideScoped) : null;
            foreach (string method in new[] { typeof(IDeletable).IsAssignableFrom(entity.ClrType) ? nameof(HideDeleted) : null, filter }.OfType<string>())
            {
                typeof(PMPlatformDbContext).GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entity.ClrType)
                    .Invoke(this, [modelBuilder]);
            }
        }

        HideChildrenOfDeleted(modelBuilder);
    }

    /// <summary>
    /// What belongs to a deletable record goes with it (client review, item 8): every entity with a foreign key to an <see cref="IDeletable"/>
    /// other than the project (anchored above), unless it is deletable itself and so lives by its own lifecycle, such as an issue raised from
    /// a risk. Read from the model, so a new child is covered without naming it here.
    /// </summary>
    private void HideChildrenOfDeleted(ModelBuilder modelBuilder)
    {
        foreach (IMutableForeignKey key in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).ToList())
        {
            Type child = key.DeclaringEntityType.ClrType;
            Type parent = key.PrincipalEntityType.ClrType;
            if (key.Properties.Count != 1 || parent == child || parent == typeof(ProjectEntity) || key.DeclaringEntityType.GetSchema() == AuditSchema
                || !typeof(IDeletable).IsAssignableFrom(parent) || typeof(IDeletable).IsAssignableFrom(child))
            {
                continue;
            }

            IMutableProperty column = key.Properties[0];
            typeof(PMPlatformDbContext).GetMethod(column.IsNullable ? nameof(HideOptionalChildOf) : nameof(HideChildOf), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(child, parent)
                .Invoke(this, [modelBuilder, column.Name]);
        }
    }

    private void HideChildOf<TChild, TParent>(ModelBuilder modelBuilder, string column)
        where TChild : class
        where TParent : AuditedEntity, IDeletable =>
        modelBuilder.Entity<TChild>().HasQueryFilter($"{DeletedParentFilter}:{column}", e => Set<TParent>().Any(p => p.Id == EF.Property<Guid>(e, column)));

    private void HideOptionalChildOf<TChild, TParent>(ModelBuilder modelBuilder, string column)
        where TChild : class
        where TParent : AuditedEntity, IDeletable =>
        modelBuilder.Entity<TChild>().HasQueryFilter($"{DeletedParentFilter}:{column}", e => EF.Property<Guid?>(e, column) == null
                                                                                          || Set<TParent>().Any(p => p.Id == EF.Property<Guid?>(e, column)));

    private static void HideDeleted<T>(ModelBuilder modelBuilder)
        where T : class, IDeletable =>
        modelBuilder.Entity<T>().HasQueryFilter(DeletedFilter, e => e.DeletedAt == null);

    private void HideAnchored<T>(ModelBuilder modelBuilder)
        where T : class =>
        modelBuilder.Entity<T>().HasQueryFilter(DeletedProjectFilter, e => Set<ProjectEntity>().Any(p => p.Id == EF.Property<Guid>(e, ProjectAnchor)));

    private void HideOptionallyAnchored<T>(ModelBuilder modelBuilder)
        where T : class =>
        modelBuilder.Entity<T>().HasQueryFilter(DeletedProjectFilter, e => EF.Property<Guid?>(e, ProjectAnchor) == null
                                                     || Set<ProjectEntity>().Any(p => p.Id == EF.Property<Guid?>(e, ProjectAnchor)));

    private void HideScoped<T>(ModelBuilder modelBuilder)
        where T : class =>
        modelBuilder.Entity<T>().HasQueryFilter(DeletedProjectFilter, e => EF.Property<Guid?>(e, ProjectScope) == null
                                                     || Set<ProjectEntity>().Any(p => p.Id == EF.Property<Guid?>(e, ProjectScope)));
}
