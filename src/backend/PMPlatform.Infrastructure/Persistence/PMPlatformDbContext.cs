using Microsoft.EntityFrameworkCore;
using PMPlatform.Domain.Common;

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
    }
}
