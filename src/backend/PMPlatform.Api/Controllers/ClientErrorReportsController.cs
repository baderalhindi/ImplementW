using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Middleware;
using PMPlatform.Api.Models.Observability;
using PMPlatform.Api.Observability;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// The SPA's unhandled errors (TASK-090). The browser sends them here, never to the APM: <c>APM_DSN</c> is a secret, the SPA's CSP
/// allows only its own origin, and every report is redacted on its way out like any server event. Anonymous, because the sign-in
/// screen can fail too, and therefore rate-limited per client address. Nothing is stored; with APM off the report is discarded.
/// Documented under FG-05, the platform's monitoring module (R-51), though FG-05 itself plays no part.
/// </summary>
[ApiController]
[Route("api/v1/client-error-reports")]
[Tags("IntegrationMonitoring")]
public sealed class ClientErrorReportsController : ControllerBase
{
    /// <summary>Reports an error the SPA could not handle.</summary>
    [HttpPost]
    [AllowAnonymous]
    [RateLimited(RateLimitPolicy.ClientErrorReport)]
    [NonSensitiveWrite("R-35: a diagnostic report; stores nothing and changes no state")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("IntegrationMonitoring_CreateClientErrorReport")]
    public IActionResult Create(ClientErrorReportCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ApiProblem.Result(HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed.", errors);
        }

        SentryEvent report = new()
        {
            Message = new SentryMessage { Formatted = $"{request.ErrorType}: {request.Message}" },
            Level = SentryLevel.Error,
            Logger = "spa",
            TransactionName = request.Path,
        };
        report.SetTag("origin", "spa");
        report.SetTag("source", JsonNamingPolicy.SnakeCaseUpper.ConvertName(request.Source!.Value.ToString()));
        if (request.CorrelationId is { } correlationId)
        {
            report.SetTag(ApmEventEnrichment.CorrelationTag, correlationId.ToString());
        }

        if (request.Stack is not null)
        {
            report.SetExtra("stack", request.Stack);
        }

        SentrySdk.CaptureEvent(report);
        return NoContent();
    }
}
