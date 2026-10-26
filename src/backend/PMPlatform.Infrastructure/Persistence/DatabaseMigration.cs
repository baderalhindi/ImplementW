using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// The <c>migrate</c> command of the release image (TASK-024). Each environment's database has no public IP, so a
/// release's migrations run inside the environment as a Cloud Run job on the release image
/// (<c>.github/scripts/migrate-environment.sh</c>). The job runs <c>dotnet PMPlatform.Api.dll migrate</c>, which
/// reads <c>DB_CONNECTION_STRING</c> through the same configuration and secret store as the service.
/// </summary>
public static partial class DatabaseMigration
{
    public const string Command = "migrate";

    /// <summary>
    /// Applies every pending migration or, given <paramref name="targetMigration"/>, migrates up or down to it.
    /// A target of <c>0</c> reverts every migration.
    /// </summary>
    public static async Task MigrateDatabaseAsync(
        this IServiceProvider services,
        string? targetMigration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigration));

        string before = await CurrentMigrationAsync(context, cancellationToken).ConfigureAwait(false);
        LogMigrating(logger, before, targetMigration ?? "latest");

        // EF Core throws for a target that is not a migration of this release, before anything is applied.
        await context.GetService<IMigrator>().MigrateAsync(targetMigration, cancellationToken).ConfigureAwait(false);

        string after = await CurrentMigrationAsync(context, cancellationToken).ConfigureAwait(false);
        LogMigrated(logger, after);
    }

    private static async Task<string> CurrentMigrationAsync(PMPlatformDbContext context, CancellationToken cancellationToken) =>
        (await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).LastOrDefault() ?? "none";

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema at {Current}; migrating to {Target}.")]
    private static partial void LogMigrating(ILogger logger, string current, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema now at {Current}.")]
    private static partial void LogMigrated(ILogger logger, string current);
}
