using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application;

/// <summary>Registers application services. Each module adds its own handlers here as it is built.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // IdentityAccess (TASK-028): sign-in and ADM-041. The ports they use are implemented in Infrastructure.
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IIdentityIntegrationService, IdentityIntegrationService>();

        return services;
    }
}
