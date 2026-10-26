using Microsoft.EntityFrameworkCore;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// The one place the EF Core options are set (TASK-024). The running API, <c>dotnet ef</c> and the migration tests
/// all call it, so all three build the same model and read and write the same history table.
/// </summary>
internal static class DatabaseOptions
{
    /// <remarks><paramref name="connectionString"/> is null only at design time, where adding or scripting a migration needs no database.</remarks>
    public static DbContextOptionsBuilder UsePlatformDatabase(this DbContextOptionsBuilder builder, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                PMPlatformDbContext.MigrationsHistoryTable,
                PMPlatformDbContext.MigrationsHistorySchema))
            // ERD D-4: tables and columns are snake_case; entity and property names stay PascalCase in C#.
            .UseSnakeCaseNamingConvention();
    }
}
