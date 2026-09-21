using Microsoft.Extensions.DependencyInjection;

namespace PMPlatform.Application;

/// <summary>Registers application services. Each module adds its own handlers here as it is built.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
