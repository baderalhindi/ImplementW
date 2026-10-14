using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Common.RateLimiting;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Middleware;

/// <summary>
/// Per-client rate limiting (TASK-078, CTL-12). An endpoint marked <see cref="RateLimitedAttribute"/>, or a sensitive write
/// (<see cref="SensitiveWriteAttribute"/>), counts each request against its policy's limit for that client: the signed-in user,
/// or for an anonymous caller the client address (behind the load balancer, the forwarded one). The request past the limit is
/// answered 429 <c>RATE_LIMITED</c> with <c>Retry-After</c> until the window closes, and never reaches the action (R-48).
/// It runs after authentication, so it knows the user, and before authorization, so refused requests count too.
/// </summary>
internal sealed partial class RateLimiting(
    RequestDelegate next, IRateLimitStore store, IOptionsMonitor<RateLimitingOptions> options, ILogger<RateLimiting> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (PolicyOf(context.GetEndpoint()) is not { } policy)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        RateLimitRule rule = options.CurrentValue.Policies[policy];
        string client = ClientOf(context);
        RateLimitCount? count = await store.IncrementAsync($"{policy}:{client}", rule.Window, context.RequestAborted).ConfigureAwait(false);
        if (count is null || count.Count <= rule.PermitLimit)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Once per window and client, so a burst does not flood the log with the same line.
        if (count.Count == rule.PermitLimit + 1)
        {
            LogThrottled(logger, policy, client, rule.PermitLimit, rule.Window);
        }

        context.Response.Headers.RetryAfter = RetryAfterSeconds(count.ResetAfter).ToString(CultureInfo.InvariantCulture);
        await ApiProblem.WriteAsync(context, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited, "Too many requests.").ConfigureAwait(false);
    }

    public static RateLimitPolicy? PolicyOf(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<RateLimitedAttribute>()?.Policy
        ?? (endpoint?.Metadata.GetMetadata<SensitiveWriteAttribute>() is not null ? RateLimitPolicy.SensitiveWrite : null);

    /// <summary>Whole seconds until the window closes, at least one: <c>Retry-After: 0</c> would invite an immediate retry that is refused again.</summary>
    public static long RetryAfterSeconds(TimeSpan resetAfter) => Math.Max(1, (long)Math.Ceiling(resetAfter.TotalSeconds));

    private static string ClientOf(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true && context.User.FindFirstValue(SessionTokenClaims.Subject) is { } subject
            ? $"user:{subject}"
            : $"address:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit {Policy} reached by {Client}: more than {PermitLimit} requests in {Window}; further requests answer 429.")]
    private static partial void LogThrottled(ILogger logger, RateLimitPolicy policy, string client, int permitLimit, TimeSpan window);
}
