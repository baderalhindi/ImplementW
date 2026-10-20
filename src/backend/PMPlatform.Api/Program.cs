using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Options;
using PMPlatform.Api.Auditing;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Correlation;
using PMPlatform.Api.Errors;
using PMPlatform.Api.HealthChecks;
using PMPlatform.Api.Middleware;
using PMPlatform.Api.Models;
using PMPlatform.Api.Observability;
using PMPlatform.Api.Validation;
using PMPlatform.Application;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Infrastructure;
using PMPlatform.Infrastructure.Audit;
using PMPlatform.Infrastructure.Identity;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Infrastructure.Secrets;

// Composition root (L-4): the only place in PMPlatform.Api that references PMPlatform.Infrastructure.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// TASK-013: a developer's local-only values, copied from appsettings.Template.json. Git-ignored and never
// published. Inserted directly before the environment-variable provider so that, exactly as in a deployed
// environment, an environment variable overrides any file.
IList<IConfigurationSource> configurationSources = builder.Configuration.Sources;
int environmentVariablesIndex = configurationSources
    .TakeWhile(source => source is not EnvironmentVariablesConfigurationSource)
    .Count();
configurationSources.Insert(environmentVariablesIndex, new JsonConfigurationSource
{
    Path = $"appsettings.{builder.Environment.EnvironmentName}.Local.json",
    Optional = true,
    ReloadOnChange = true,
});

// TASK-019: the approved secret store, added last so a secret resolves to the store's value wherever else
// it is configured. Only local development may run without it, on the git-ignored Local.json above (CTL-18).
builder.Configuration.AddSecretStore(required: !builder.Environment.IsDevelopment());

// TASK-077: every string an action receives obeys the platform's text rules before the action runs (CTL-11).
builder.Services.AddControllers(options => options.Filters.Add<RequestTextValidation>())
    .AddJsonOptions(options => AddWireConverters(options.JsonSerializerOptions))
    // R-23: a body that does not bind (malformed JSON, no body) is a 400 in the platform envelope, and says only where.
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        ApiProblem.Result(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed.",
            [new FieldError("body", FieldError.Malformed)]));
// TASK-043: the document describes the wire. The generator reads these options, not MVC's, so it is given the same
// converters; Money's converter is opaque to it, so its schema is declared (R-16).
builder.Services.ConfigureHttpJsonOptions(options => AddWireConverters(options.SerializerOptions));
builder.Services.AddOpenApi(options => options.AddSchemaTransformer<MoneySarSchemaTransformer>());
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// TASK-033: audit events carry the request's correlation id, client address and caller.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditRequestContext, HttpAuditRequestContext>();

// TASK-028: every request is authenticated by the platform's own access token (R-46), validated against the current
// JWT_SIGNING_KEY. Authorization is deny-by-default: an endpoint without [AllowAnonymous] requires a valid token.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration, TimeProvider>((options, configuration, timeProvider) =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = SessionTokenValidation.AccessToken(configuration, timeProvider);
        options.Events = BearerChallenge.Events();
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// TASK-030: [RequirePermission] is decided by the authorization engine on grants read from the database, never by the
// token's role claims. Scoped, like the engine it asks.
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// TASK-029: which operations need a fresh second factor, and how fresh (ADR-010; the list is configuration).
builder.Services.AddOptions<StepUpOptions>()
    .Bind(builder.Configuration.GetSection(StepUpOptions.Section))
    .Validate(options => options.MaxAge > TimeSpan.Zero, $"{StepUpOptions.Section}:MaxAge must be positive.")
    .ValidateOnStart();

// TASK-078 (CTL-12): endpoint hardening. Kestrel does not name itself in a Server header.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

// The client address rate limiting and the audit trail (TASK-033 F-5) read. Behind AHDA's load balancer the TCP peer is
// Google's front end and the client is in X-Forwarded-For, which the regional load balancer extends with
// "<client>, <load balancer>". Cloud Run accepts traffic only from that load balancer (network-security.md §3.3), so the last
// ClientAddress:TrustedProxyHops entries are the platform's own and the one before them is the client; anything a client
// wrote further left is ignored. Zero (Development) keeps the TCP peer.
builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
{
    int trustedProxyHops = configuration.GetValue<int>("ClientAddress:TrustedProxyHops");
    options.ForwardedHeaders = trustedProxyHops > 0 ? ForwardedHeaders.XForwardedFor : ForwardedHeaders.None;
    options.ForwardLimit = trustedProxyHops;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.Section))
    .Validate(options => options.IsComplete(),
        $"{RateLimitingOptions.Section}:Policies needs a positive PermitLimit and Window for each of {string.Join(", ", Enum.GetNames<RateLimitPolicy>())}.")
    .ValidateOnStart();

builder.Services.AddOptions<AllowedOrigins>()
    .Configure<IConfiguration, IHostEnvironment>((allowed, configuration, environment) =>
        allowed.Origins = OriginAllowlist.Parse(configuration[OriginAllowlist.Variable], allowHttp: environment.IsDevelopment()))
    .ValidateOnStart();
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IOptions<AllowedOrigins>>((cors, allowed) =>
    cors.AddDefaultPolicy(OriginAllowlist.Policy(allowed.Value.Origins)));

// TASK-090 (CTL-30): APM and error tracking when APM_DSN is set — unhandled exceptions, logged errors, failed integration calls
// and a trace per request, each tagged with its correlation id and redacted against the registry before it leaves (CTL-27).
builder.AddApm();

WebApplication app = builder.Build();

// TASK-024: `dotnet PMPlatform.Api.dll migrate [<target migration>]` applies the release's migrations and exits
// without serving. Each environment runs it as its migration job before the service is deployed.
if (args is [DatabaseMigration.Command, ..])
{
    Environment.ExitCode = await RunToCompletionAsync(args[0], async () =>
    {
        await app.Services.MigrateDatabaseAsync(args.ElementAtOrDefault(1)).ConfigureAwait(false);
        return 0;
    }).ConfigureAwait(false);
    return;
}

// TASK-027: `seed` loads db/seed/seed-master-data.sql; `validate-data-integrity` runs db/seed/validate-data-integrity.sql
// and exits non-zero on any violation. Each environment runs both after `migrate`, as executions of the same job.
if (args is [string scriptCommand] && DatabaseScripts.IsCommand(scriptCommand))
{
    Environment.ExitCode = await RunToCompletionAsync(scriptCommand, async () =>
    {
        await app.Services.RunScriptCommandAsync(scriptCommand).ConfigureAwait(false);
        return 0;
    }).ConfigureAwait(false);
    return;
}

// TASK-073: `audit-integrity-verify`, `activity-projection-check` and `activity-projection-rebuild` — the FG-06 rebuild tooling — run
// once and exit, non-zero when the chain is broken or the Activity projection differs from the audit store.
if (args is [string auditCommand] && AuditCommands.IsCommand(auditCommand))
{
    Environment.ExitCode = await RunToCompletionAsync(auditCommand, () => app.Services.RunAuditCommandAsync(auditCommand)).ConfigureAwait(false);
    return;
}

app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeaders>();
app.UseMiddleware<CorrelationId>();

// R-26: an unhandled exception is a 500 carrying code, correlation id and timestamp, and nothing about the exception.
// TASK-034: configuration an operation requires and cannot resolve is 422 CONFIGURATION_MISSING — the operation stops
// explicitly instead of defaulting (Blueprint Section 12).
app.UseExceptionHandler(handler => handler.Run(context =>
    context.Features.Get<IExceptionHandlerFeature>()?.Error is ConfigurationMissingException
        ? ApiProblem.WriteAsync(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.ConfigurationMissing, "Configuration missing.")
        : ApiProblem.WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "Internal error.")));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseMiddleware<OriginAllowlist>();
app.UseCors();

// TASK-079 (CTL-13): a state-changing request another site could have made the browser send is refused before it is
// authenticated. After the origin allowlist, so a request from a foreign origin is still refused as ORIGIN_NOT_ALLOWED.
app.UseMiddleware<CsrfProtection>();
app.UseAuthentication();
app.UseMiddleware<RateLimiting>();
app.UseAuthorization();
app.UseMiddleware<StepUpAuthentication>();

// TASK-091 (CTL-31): /health, /health/live and /health/ready, anonymous.
app.MapHealthEndpoints();
app.MapControllers();
app.MapApmDiagnostics();
app.RequireKnownStepUpOperations();
app.RequireEndpointAuthorization();

await app.RunAsync().ConfigureAwait(false);

// TASK-089: a one-shot command fails by its exit status, never by an unhandled exception. An unhandled exception aborts the
// runtime, and the release image runs dotnet as PID 1 with no init: the kernel ignores the abort signal for PID 1, so the
// process spins at full CPU instead of exiting, and a failed `migrate` would hold its migration job until the job times out
// (db/migrations/rollback-validation.md F-1).
static async Task<int> RunToCompletionAsync(string command, Func<Task<int>> run)
{
#pragma warning disable CA1031 // Every failure of a one-shot command is reported the same way: on stderr, and as exit status 1.
    try
    {
        return await run().ConfigureAwait(false);
    }
    catch (Exception exception)
    {
        await Console.Error.WriteLineAsync($"{command} failed: {exception}").ConfigureAwait(false);
        return 1;
    }
#pragma warning restore CA1031
}

// api-conventions R-19: enumerations are UPPER_SNAKE_CASE strings.
// R-16: a SAR amount is a decimal string with two fraction digits.
static void AddWireConverters(JsonSerializerOptions options)
{
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    options.Converters.Add(new MoneySarJsonConverter());
}

/// <summary>The API's entry point; public so the integration tests can host it (WebApplicationFactory).</summary>
public partial class Program;
