using System.IO.Compression;
using Microsoft.Extensions.Options;
using PMPlatform.Api.Authorization;
using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace PMPlatform.Api.Observability;

/// <summary>
/// APM and error tracking (TASK-090, CTL-30): the Sentry SDK captures every unhandled exception, every logged error (a worker
/// pass, a queue, a failed integration call) and a trace per request for latency, throughput and error rate, tagged with the
/// correlation id and redacted on the way out. The SDK speaks the Sentry protocol, which a self-hosted Sentry or GlitchTip in
/// the named region ingests (CTL-28); the endpoint is <c>APM_DSN</c>.
/// </summary>
internal static class ApmRegistration
{
    public const string DiagnosticExceptionPath = "/api/v1/apm-diagnostic-exceptions";

    /// <summary>
    /// Registers the SDK when <c>APM_DSN</c> is set. Without it nothing is registered at all, so the SDK's process-wide hub is never
    /// initialised — not even disabled — and no event can be captured or sent.
    /// </summary>
    public static void AddApm(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<ApmOptions>()
            .Configure<IConfiguration>((apm, configuration) =>
            {
                configuration.GetSection(ApmOptions.Section).Bind(apm);
                apm.Dsn = configuration[ApmOptions.DsnVariable];
                apm.EnvironmentTag = configuration[ApmOptions.EnvironmentTagVariable];
            })
            .Validate(apm => apm.IsValid(),
                $"{ApmOptions.EnvironmentTagVariable} is required with {ApmOptions.DsnVariable}, {ApmOptions.Section}:TracesSampleRate is 0 to 1, and " +
                $"{ApmOptions.Section}:DiagnosticExceptionEndpoint needs an environment tag other than '{ApmOptions.ProductionTag}'.")
            .ValidateOnStart();

        if (string.IsNullOrWhiteSpace(builder.Configuration[ApmOptions.DsnVariable]))
        {
            return;
        }

        builder.WebHost.UseSentry((ISentryBuilder _) => { });
        builder.Services.AddOptions<SentryAspNetCoreOptions>()
            .PostConfigure<IOptions<ApmOptions>, IHttpContextAccessor>((sentry, apm, httpContextAccessor) =>
                Configure(sentry, apm.Value, new ApmEventEnrichment(httpContextAccessor)));
    }

    /// <summary>The deliberate unhandled exception of the validation check, where an environment enables it (<see cref="ApmOptions.DiagnosticExceptionEndpoint"/>).</summary>
    public static void MapApmDiagnostics(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IOptions<ApmOptions>>().Value.DiagnosticExceptionEndpoint)
        {
            return;
        }

        app.MapPost(DiagnosticExceptionPath, ThrowDiagnosticException)
            .WithMetadata(new AllowAnyAuthenticatedUserAttribute())
            .ExcludeFromDescription();
    }

    private static IResult ThrowDiagnosticException(ApmDiagnosticExceptionRequest? request) => throw new ApmDiagnosticException(request?.Note);

    private static void Configure(SentryAspNetCoreOptions sentry, ApmOptions apm, ApmEventEnrichment enrichment)
    {
        sentry.Dsn = apm.Dsn;
        sentry.Environment = apm.EnvironmentTag;
        sentry.TracesSampleRate = apm.TracesSampleRate;

        // Nothing personal is attached by the SDK: no user, client address, cookies or request body.
        sentry.SendDefaultPii = false;
        sentry.MaxRequestBodySize = RequestSize.None;

        // Logged errors become events; warnings ride along as breadcrumbs. Information lines (EF's SQL among them) stay in the logs.
        sentry.MinimumEventLevel = LogLevel.Error;
        sentry.MinimumBreadcrumbLevel = LogLevel.Warning;

        // An integration answering 5xx is an event (Blueprint Section 22: integration failures). No trace header is added to calls
        // to Nafath, the SMS gateway, the SIEM or any other third party.
        sentry.CaptureFailedRequests = true;
        sentry.TracePropagationTargets = [];

        // The SDK keeps nothing on disk: no offline cache, no installation id file (a container's file system is not kept anyway).
        sentry.DisableFileWrite = true;
        sentry.AutoSessionTracking = false;
        sentry.AddEventProcessor(enrichment);
        sentry.AddTransactionProcessor(enrichment);

        // Redaction reads the envelope as JSON, so it is sent uncompressed.
        sentry.RequestBodyCompressionLevel = CompressionLevel.NoCompression;
        sentry.CreateHttpMessageHandler = () => new ApmPayloadRedaction(new SocketsHttpHandler());
    }
}

/// <summary>The body of the diagnostic exception request: a note that is put in the exception's message, so a sensitive value can be planted.</summary>
internal sealed record ApmDiagnosticExceptionRequest(string? Note);

/// <summary>Thrown on purpose by <see cref="ApmRegistration.DiagnosticExceptionPath"/> (TASK-090 validation check).</summary>
internal sealed class ApmDiagnosticException(string? note)
    : Exception($"Deliberate unhandled exception for the APM validation check (TASK-090). Note: {note}");
