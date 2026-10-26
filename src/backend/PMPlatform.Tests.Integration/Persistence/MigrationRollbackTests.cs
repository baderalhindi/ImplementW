using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-024 acceptance, executed against PostgreSQL: the migrations build the full schema from an empty database,
/// the history table records exactly what was applied, and every migration's Down restores the schema its Up
/// started from. Each test runs on its own throwaway database (<see cref="ThrowawayDatabase"/>).
/// </summary>
public sealed class MigrationRollbackTests
{
    /// <summary>The workbook's validation cell rolls back the last three migrations in one step.</summary>
    private const int WorkbookRollbackDepth = 3;

    [Fact]
    public async Task EveryMigrationRollsBackToTheSchemaBeforeIt()
    {
        await using ThrowawayDatabase database = await ThrowawayDatabase.CreateAsync();
        await using PMPlatformDbContext context = database.CreateContext();
        IMigrator migrator = context.GetService<IMigrator>();
        string[] migrations = [.. context.Database.GetMigrations()];

        // The history table exists before the first migration and survives the last Down, so it belongs in the
        // baseline snapshot: "empty" means no migration applied, not no history table.
        await context.GetService<IHistoryRepository>().CreateIfNotExistsAsync();

        // Up one migration at a time from empty, recording the schema each one leaves: schemas[i] is the schema
        // after the first i migrations.
        List<IReadOnlyList<string>> schemas = [await SchemaSnapshot.TakeAsync(database.ConnectionString)];
        foreach (string migration in migrations)
        {
            await migrator.MigrateAsync(migration);
            schemas.Add(await SchemaSnapshot.TakeAsync(database.ConnectionString));
        }

        Assert.Equal(migrations, await context.Database.GetAppliedMigrationsAsync());

        // Down one migration at a time: each must return the schema, and the history, to what they were before it.
        for (int applied = migrations.Length - 1; applied >= 0; applied--)
        {
            await migrator.MigrateAsync(applied == 0 ? Migration.InitialDatabase : migrations[applied - 1]);

            Assert.Equal(schemas[applied], await SchemaSnapshot.TakeAsync(database.ConnectionString));
            Assert.Equal(migrations[..applied], await context.Database.GetAppliedMigrationsAsync());
        }

        // And up again to the full schema with no manual step in between.
        await migrator.MigrateAsync();

        Assert.Equal(schemas[^1], await SchemaSnapshot.TakeAsync(database.ConnectionString));
        Assert.Equal(migrations, await context.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>
    /// The workbook's validation cell as written: from empty, roll back the last three migrations in one command,
    /// re-apply them in one command, and compare the schema in both directions. With fewer than three migrations
    /// the rollback goes to the empty database.
    /// </summary>
    [Fact]
    public async Task TheLastThreeMigrationsRollBackAndReapplyInOneStepEach()
    {
        await using ThrowawayDatabase database = await ThrowawayDatabase.CreateAsync();
        await using PMPlatformDbContext context = database.CreateContext();
        IMigrator migrator = context.GetService<IMigrator>();
        string[] migrations = [.. context.Database.GetMigrations()];
        int kept = Math.Max(0, migrations.Length - WorkbookRollbackDepth);
        string rollbackTarget = kept == 0 ? Migration.InitialDatabase : migrations[kept - 1];

        await migrator.MigrateAsync(rollbackTarget);
        IReadOnlyList<string> before = await SchemaSnapshot.TakeAsync(database.ConnectionString);

        await context.Database.MigrateAsync();
        IReadOnlyList<string> full = await SchemaSnapshot.TakeAsync(database.ConnectionString);

        await migrator.MigrateAsync(rollbackTarget);
        Assert.Equal(before, await SchemaSnapshot.TakeAsync(database.ConnectionString));
        Assert.Equal(migrations[..kept], await context.Database.GetAppliedMigrationsAsync());

        await context.Database.MigrateAsync();
        Assert.Equal(full, await SchemaSnapshot.TakeAsync(database.ConnectionString));
        Assert.Equal(migrations, await context.Database.GetAppliedMigrationsAsync());
    }
}
