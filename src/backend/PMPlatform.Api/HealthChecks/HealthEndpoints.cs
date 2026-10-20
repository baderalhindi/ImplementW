using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PMPlatform.Application.Common.Observability;

namespace PMPlatform.Api.HealthChecks;

/// <summary>
/// The health endpoints (TASK-091, CTL-31). Each answers 200 <c>Healthy</c> or 503 <c>Unhealthy</c> in plain text and nothing more: they
/// are anonymous, so which check failed and why goes to the log, not to the caller. Never cached.
/// </summary>
internal static class HealthEndpoints
{
    /// <summary>Every check. The path TASK-014 introduced, which the compose stack, the smoke tests and the drills read.</summary>
    public const string Health = "/health";

    /// <summary>Restart the instance when this fails: the process answers and no background worker is stalled. Cloud Run's liveness probe.</summary>
    public const string Live = "/health/live";

    /// <summary>The instance can serve: the database answers. Cloud Run's start-up probe and each environment's uptime check.</summary>
    public const string Ready = "/health/ready";

    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(Health).AllowAnonymous();
        app.MapHealthChecks(Live, Tagged(HealthCheckTags.Liveness)).AllowAnonymous();
        app.MapHealthChecks(Ready, Tagged(HealthCheckTags.Readiness)).AllowAnonymous();
    }

    private static HealthCheckOptions Tagged(string tag) => new() { Predicate = registration => registration.Tags.Contains(tag) };
}
