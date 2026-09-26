using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Infrastructure.Identity;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure;

/// <summary>
/// Registers the implementations of the Application and Domain interfaces (L-3). Persistence (TASK-024),
/// identity adapters (TASK-028) and notification channels (TASK-039) are added here as they are built.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHealthChecks().AddCheck<PostgreSqlHealthCheck>("postgresql");

        // Resolved when a context is created, not at start-up, so the API still starts and reports Unhealthy
        // without a database, as it did before TASK-024.
        services.AddDbContext<PMPlatformDbContext>(options => options.UsePlatformDatabase(RequiredConnectionString(configuration)));

        // TASK-028: the directory, SSO and session-token adapters, and the IdentityAccess repository sign-in reads.
        services.AddIdentityIntegration(configuration);
        services.AddScoped<IUserAccessRepository, UserAccessRepository>();

        return services;
    }

    private static string RequiredConnectionString(IConfiguration configuration)
    {
        string? connectionString = configuration[ApplicationSecrets.DatabaseConnectionString];
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"{ApplicationSecrets.DatabaseConnectionString} is not configured.")
            : connectionString;
    }
}
