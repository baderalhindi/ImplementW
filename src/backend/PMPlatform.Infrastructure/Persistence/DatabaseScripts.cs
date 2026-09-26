using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// The <c>seed</c> and <c>validate-data-integrity</c> commands of the release image (TASK-027). Each runs one SQL file
/// from <c>db/seed</c>, embedded in this assembly, so the script an environment runs is exactly the release's. Like
/// <see cref="DatabaseMigration"/>, they run inside the environment as executions of its migration job, because the
/// database has no public IP (<c>.github/scripts/migrate-environment.sh</c>).
/// </summary>
public static partial class DatabaseScripts
{
    public const string SeedCommand = "seed";
    public const string ValidateDataIntegrityCommand = "validate-data-integrity";

    private static readonly Dictionary<string, string> ScriptByCommand = new(StringComparer.Ordinal)
    {
        [SeedCommand] = "seed-master-data.sql",
        [ValidateDataIntegrityCommand] = "validate-data-integrity.sql",
    };

    public static bool IsCommand(string command) => ScriptByCommand.ContainsKey(command);

    /// <summary>The text of <paramref name="fileName"/> in <c>db/seed</c>, as embedded in this assembly.</summary>
    public static string Read(string fileName)
    {
        using Stream stream = typeof(DatabaseScripts).Assembly.GetManifestResourceStream($"PMPlatform.Seed.{fileName}")
            ?? throw new InvalidOperationException($"db/seed/{fileName} is not embedded in {typeof(DatabaseScripts).Assembly.GetName().Name}.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Runs the command's script in one transaction and logs the server's notices. A failure rolls the transaction
    /// back and propagates, so the process exits non-zero: a seed that fails leaves nothing half-written, and a
    /// violation found by the validator (SQLSTATE 23000) fails the deployment step.
    /// </summary>
    public static async Task RunScriptCommandAsync(this IServiceProvider services, string command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!ScriptByCommand.TryGetValue(command, out string? fileName))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "Not a database script command.");
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseScripts));

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        connection.Notice += (_, e) => LogNotice(logger, e.Notice.MessageText);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        LogRunning(logger, command, fileName);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using NpgsqlCommand script = new(Read(fileName), connection, (NpgsqlTransaction)transaction);
            await script.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception)
        {
            LogFailed(logger, command, exception.SqlState, exception.MessageText);
            throw;
        }

        LogCompleted(logger, command);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Command}: running db/seed/{FileName}.")]
    private static partial void LogRunning(ILogger logger, string command, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    private static partial void LogNotice(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Command}: completed.")]
    private static partial void LogCompleted(ILogger logger, string command);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Command} failed with SQLSTATE {SqlState}: {Message}")]
    private static partial void LogFailed(ILogger logger, string command, string sqlState, string message);
}
