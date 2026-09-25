using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Unit.Architecture;

namespace PMPlatform.Tests.Unit.Infrastructure.Persistence;

/// <summary>
/// TASK-024 conventions that hold without a database: every migration is named for the task that made it, carries
/// a rollback, and the model has no change that lacks a migration. The rollback itself is executed against
/// PostgreSQL by PMPlatform.Tests.Integration (MigrationRollbackTests).
/// </summary>
public sealed partial class MigrationConventionTests : IDisposable
{
    private const string CommonSchema = "common";

    private readonly PMPlatformDbContext _context = new PMPlatformDbContextFactory().CreateDbContext([]);

    public void Dispose() => _context.Dispose();

    [Fact]
    public void ThereIsAtLeastOneMigration() => Assert.NotEmpty(Migrations());

    /// <summary><c>&lt;timestamp&gt;_&lt;TaskID&gt;_&lt;Description&gt;</c>, e.g. <c>20260925151339_TASK-024_CreateModuleSchemas</c>.</summary>
    [Fact]
    public void EveryMigrationIdNamesItsTask()
    {
        IEnumerable<string> violations = Migrations()
            .Select(m => m.Id)
            .Where(id => MigrationId().Match(id) is not { Success: true } match
                || !DateTime.TryParseExact(match.Groups["timestamp"].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));

        Assert.Empty(violations);
    }

    /// <summary>A migration whose Down does nothing has no rollback path; one whose Up does nothing is not a change.</summary>
    [Fact]
    public void EveryMigrationHasAnUpAndADown()
    {
        IEnumerable<string> violations = Migrations()
            .Where(m => m.Migration.UpOperations.Count == 0 || m.Migration.DownOperations.Count == 0)
            .Select(m => $"{m.Id}: {m.Migration.UpOperations.Count} up, {m.Migration.DownOperations.Count} down");

        Assert.Empty(violations);
    }

    /// <summary>An entity or mapping change without its migration would reach no database.</summary>
    [Fact]
    public void TheModelHasNoChangeWithoutAMigration() => Assert.False(_context.Database.HasPendingModelChanges());

    /// <summary>M-13: one schema per module, named as the module in snake_case (ERD D-4), plus <c>common</c> (D-17).</summary>
    [Fact]
    public void MigrationsCreateExactlyTheModuleSchemas()
    {
        string[] created = [.. Migrations()
            .SelectMany(m => m.Migration.UpOperations.OfType<EnsureSchemaOperation>())
            .Select(o => o.Name)
            .Where(name => name != CommonSchema)
            .Order(StringComparer.Ordinal)];
        string[] modules = [.. ModuleRegistry.Modules.Select(SnakeCase).Order(StringComparer.Ordinal)];

        Assert.Equal(modules, created);
    }

    [Fact]
    public void TheHistoryTableIsInTheCommonSchema()
    {
        IHistoryRepository history = _context.GetService<IHistoryRepository>();

        Assert.Contains($"{CommonSchema}.\"{PMPlatformDbContext.MigrationsHistoryTable}\"", history.GetCreateIfNotExistsScript(), StringComparison.Ordinal);
    }

    private IEnumerable<(string Id, Migration Migration)> Migrations()
    {
        IMigrationsAssembly assembly = _context.GetService<IMigrationsAssembly>();
        string provider = _context.Database.ProviderName!;

        return assembly.Migrations.Select(m => (m.Key, assembly.CreateMigration(m.Value, provider)));
    }

    private static string SnakeCase(string pascalCase) =>
        PascalCaseBoundary().Replace(pascalCase, "_$1").ToLowerInvariant();

    [GeneratedRegex(@"^(?<timestamp>\d{14})_TASK-\d{3}_[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationId();

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex PascalCaseBoundary();
}
