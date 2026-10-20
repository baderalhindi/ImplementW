using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PMPlatform.Infrastructure.Hosting;

/// <summary>
/// Liveness of the background workers (TASK-091): unhealthy when a worker has been in one pass for longer than
/// <see cref="WorkerLivenessOptions.WorkerPassTimeout"/>, which only a pass that will never finish does. A failed pass is not a stall —
/// <see cref="PollingWorker"/> logs it and runs the next — so a database outage leaves the workers live.
/// </summary>
internal sealed class WorkerLivenessHealthCheck(WorkerActivity activity, IOptions<WorkerLivenessOptions> options, TimeProvider timeProvider)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        TimeSpan timeout = options.Value.WorkerPassTimeout;
        string[] stalled =
        [
            .. activity.PassesInProgress()
                .Where(pass => now - pass.Value > timeout)
                .Select(pass => pass.Key)
                .Order(StringComparer.Ordinal),
        ];

        return Task.FromResult(stalled.Length == 0
            ? HealthCheckResult.Healthy("No background worker is stalled.")
            : HealthCheckResult.Unhealthy($"In one pass for longer than {timeout}: {string.Join(", ", stalled)}."));
    }
}
