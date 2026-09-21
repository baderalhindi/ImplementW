using PMPlatform.Application;
using PMPlatform.Infrastructure;

// Composition root (L-4): the only place in PMPlatform.Api that references PMPlatform.Infrastructure.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
