using System.Globalization;
using Microsoft.Extensions.Options;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// The privileged-session re-authentication middleware (TASK-029, CTL-07, ADR-010). It runs after authentication and
/// authorization, so it sees only requests the caller is otherwise allowed to make; the token itself has already been
/// checked for MFA by <see cref="MultiFactorTokenValidation"/>. An operation on <see cref="StepUpOptions.Operations"/>
/// needs a session that passed a second factor no longer than <see cref="StepUpOptions.MaxAge"/> ago; otherwise it
/// answers 403 <c>STEP_UP_REQUIRED</c> and the client steps up with <c>POST /api/v1/sessions/current/step-up</c>.
/// </summary>
internal sealed class StepUpAuthentication(RequestDelegate next, IOptionsMonitor<StepUpOptions> options, TimeProvider timeProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        StepUpOptions stepUp = options.CurrentValue;
        if (context.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName is { } operation
            && stepUp.Operations.Contains(operation, StringComparer.Ordinal))
        {
            SessionAuthentication? authentication = context.User.Identity?.IsAuthenticated == true ? SessionPrincipal.Authentication(context.User) : null;

            // A step-up operation is never anonymous, even if its endpoint was marked so by mistake.
            if (authentication is null)
            {
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await ApiProblem.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.AuthenticationRequired, "Authentication required.")
                    .ConfigureAwait(false);
                return;
            }

            if (!authentication.MultiFactor || timeProvider.GetUtcNow() - authentication.AuthenticatedAt > stepUp.MaxAge)
            {
                // RFC 9470's challenge parameters, on the status the platform's error catalogue fixes (api-conventions §4.4).
                context.Response.Headers.WWWAuthenticate = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Bearer error=\"insufficient_user_authentication\", error_description=\"A fresh second factor is required\", max_age={(long)stepUp.MaxAge.TotalSeconds}");
                await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.StepUpRequired, "Step-up authentication required.")
                    .ConfigureAwait(false);
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}

/// <summary>Start-up check of <see cref="StepUpOptions.Operations"/> against the endpoints the API actually has.</summary>
internal static class StepUpOperations
{
    public static void RequireKnownStepUpOperations(this WebApplication app)
    {
        IReadOnlyList<string> configured = app.Services.GetRequiredService<IOptions<StepUpOptions>>().Value.Operations;
        HashSet<string> known = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .OfType<string>()];

        List<string> unknown = [.. configured.Where(operation => !known.Contains(operation))];
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"{StepUpOptions.Section}:Operations names no endpoint: {string.Join(", ", unknown)}. Step-up would not apply to it.");
        }
    }
}
