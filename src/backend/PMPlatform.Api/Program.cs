using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using PMPlatform.Application;
using PMPlatform.Infrastructure;
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

// TASK-024: `dotnet PMPlatform.Api.dll migrate [<target migration>]` applies the release's migrations and exits
// without serving. Each environment runs it as its migration job before the service is deployed.
if (args is [DatabaseMigration.Command, ..])
{
    await app.Services.MigrateDatabaseAsync(args.ElementAtOrDefault(1)).ConfigureAwait(false);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

await app.RunAsync().ConfigureAwait(false);
