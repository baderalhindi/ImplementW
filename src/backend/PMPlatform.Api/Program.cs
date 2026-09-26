using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Correlation;
using PMPlatform.Api.Errors;
using PMPlatform.Application;
using PMPlatform.Infrastructure;
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

builder.Services.AddControllers()
    // api-conventions R-19: enumerations are UPPER_SNAKE_CASE strings.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)))
    // R-23: a body that does not bind (malformed JSON, no body) is a 400 in the platform envelope, and says only where.
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        ApiProblem.Result(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed.",
            [new FieldError("body", FieldError.Malformed)]));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AuthorizationPolicies.SystemAdministrator, policy => policy.RequireRole(AuthorizationPolicies.SystemAdministratorRole));

// TASK-029: which operations need a fresh second factor, and how fresh (ADR-010; the list is configuration).
builder.Services.AddOptions<StepUpOptions>()
    .Bind(builder.Configuration.GetSection(StepUpOptions.Section))
    .Validate(options => options.MaxAge > TimeSpan.Zero, $"{StepUpOptions.Section}:MaxAge must be positive.")
    .ValidateOnStart();

WebApplication app = builder.Build();

// TASK-024: `dotnet PMPlatform.Api.dll migrate [<target migration>]` applies the release's migrations and exits
// without serving. Each environment runs it as its migration job before the service is deployed.
if (args is [DatabaseMigration.Command, ..])
{
    await app.Services.MigrateDatabaseAsync(args.ElementAtOrDefault(1)).ConfigureAwait(false);
    return;
}

// TASK-027: `seed` loads db/seed/seed-master-data.sql; `validate-data-integrity` runs db/seed/validate-data-integrity.sql
// and exits non-zero on any violation. Each environment runs both after `migrate`, as executions of the same job.
if (args is [string scriptCommand] && DatabaseScripts.IsCommand(scriptCommand))
{
    await app.Services.RunScriptCommandAsync(scriptCommand).ConfigureAwait(false);
    return;
}

app.UseMiddleware<CorrelationId>();

// R-26: an unhandled exception is a 500 carrying code, correlation id and timestamp, and nothing about the exception.
app.UseExceptionHandler(handler => handler.Run(context =>
    ApiProblem.WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "Internal error.")));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<StepUpAuthentication>();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.RequireKnownStepUpOperations();

await app.RunAsync().ConfigureAwait(false);

/// <summary>The API's entry point; public so the integration tests can host it (WebApplicationFactory).</summary>
public partial class Program;
