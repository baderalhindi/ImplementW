using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// One throwaway database with every migration applied and <c>db/seed/seed-master-data.sql</c> loaded through the
/// release image's own <c>seed</c> command, as each environment loads it (TASK-027).
/// </summary>
public sealed class SeededDatabase : TestDatabase
{
    /// <summary>
    /// Every table's row count and the md5 of its rows, one line per table in every non-system schema: the same
    /// comparison as <c>docs/operations/database-fingerprint.sql</c>. Equal output means no row was added, removed or
    /// changed, <c>updated_at</c> included.
    /// </summary>
    public const string TableFingerprints = """
        SELECT n.nspname || '.' || c.relname || '|' ||
               (xpath('/row/f/text()', query_to_xml(format(
                   $q$SELECT count(*) || '|' || md5(coalesce(string_agg(t::text, E'\n' ORDER BY t::text COLLATE "C"), '')) AS f FROM %I.%I AS t$q$,
                   n.nspname, c.relname), false, true, '')))[1]::text
        FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind = 'r' AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
        ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"
        """;

    protected override Task PopulateAsync() => RunCommandAsync(DatabaseScripts.SeedCommand);

    /// <summary>Runs a release-image database command (<c>seed</c>, <c>validate-data-integrity</c>) against this database.</summary>
    public Task RunCommandAsync(string command) => RunCommandAsync(ConnectionString, command);

    public static async Task RunCommandAsync(string connectionString, string command)
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<PMPlatformDbContext>(options => options.UsePlatformDatabase(connectionString))
            .BuildServiceProvider();
        await services.RunScriptCommandAsync(command);
    }
}
