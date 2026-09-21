using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using PMPlatform.Application;
using PMPlatform.Infrastructure;

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

await app.RunAsync().ConfigureAwait(false);
