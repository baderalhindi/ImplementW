using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;

namespace PMPlatform.Infrastructure.Audit;

/// <summary>
/// The FG-06 rebuild tooling as commands of the release image (TASK-073), run like <c>migrate</c> as executions of an environment's
/// job: <c>audit-integrity-verify</c> walks the hash chain, <c>activity-projection-check</c> compares the Activity projection with
/// what the store derives, and <c>activity-projection-rebuild</c> derives it again and repairs it. Each is audited, by the
/// audit-capture service principal, and logs its report. The exit code is 1 when the chain is broken or the projection differs from
/// the store, so a scheduled run fails visibly.
/// </summary>
public static partial class AuditCommands
{
    public const string VerifyIntegrityCommand = "audit-integrity-verify";
    public const string CheckProjectionCommand = "activity-projection-check";
    public const string RebuildProjectionCommand = "activity-projection-rebuild";

    private static readonly string[] Commands = [VerifyIntegrityCommand, CheckProjectionCommand, RebuildProjectionCommand];

    public static bool IsCommand(string command) => Commands.Contains(command, StringComparer.Ordinal);

    /// <summary>Runs the command and returns the process exit code.</summary>
    public static async Task<int> RunAuditCommandAsync(this IServiceProvider services, string command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IAuditOperationsService operations = scope.ServiceProvider.GetRequiredService<IAuditOperationsService>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuditCommands));
        Guid actor = AuditCapturePrincipal.Id;

        (object report, bool passed) = command switch
        {
            VerifyIntegrityCommand => await RunAsync(operations.VerifyIntegrityAsync(actor, cancellationToken), r => r.Intact).ConfigureAwait(false),
            CheckProjectionCommand => await RunAsync(operations.CheckProjectionAsync(actor, cancellationToken), r => r.Matches).ConfigureAwait(false),
            RebuildProjectionCommand => await RunAsync(operations.RebuildProjectionAsync(actor, cancellationToken), _ => true).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not an audit command."),
        };

        string json = JsonSerializer.Serialize(report, report.GetType(), EventSerialization.Options);
        LogReport(logger, command, passed ? "passed" : "FAILED", json);
        return passed ? 0 : 1;
    }

    private static async Task<(object Report, bool Passed)> RunAsync<TReport>(Task<TReport> run, Func<TReport, bool> passed)
        where TReport : class
    {
        TReport report = await run.ConfigureAwait(false);
        return (report, passed(report));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Command} {Outcome}: {Report}")]
    private static partial void LogReport(ILogger logger, string command, string outcome, string report);
}
