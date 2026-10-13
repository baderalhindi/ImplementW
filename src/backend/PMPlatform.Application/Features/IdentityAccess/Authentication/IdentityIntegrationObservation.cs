using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// Wraps the identity provider adapters Infrastructure registered so each call is published as FG-05 telemetry (TASK-075). Called once
/// the adapters are registered; the adapters themselves know nothing of monitoring.
/// </summary>
public static class IdentityIntegrationObservation
{
    public static IServiceCollection ObserveIdentityIntegrations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IdentityIntegrationTelemetry>();
        Decorate<IDirectoryService>(services, (inner, provider) => new ObservedDirectoryService(inner, Telemetry(provider)));
        Decorate<ISingleSignOnProvider>(services, (inner, provider) => new ObservedSingleSignOnProvider(inner, Telemetry(provider)));
        Decorate<IMultiFactorProvider>(services, (inner, provider) => new ObservedMultiFactorProvider(inner, Telemetry(provider)));
        Decorate<IIdentityVerificationProvider>(services, (inner, provider) => new ObservedIdentityVerificationProvider(inner, Telemetry(provider)));
        return services;
    }

    private static IdentityIntegrationTelemetry Telemetry(IServiceProvider provider) => provider.GetRequiredService<IdentityIntegrationTelemetry>();

    /// <summary>Replaces the last registration of <typeparamref name="TService"/> with <paramref name="decorate"/> of it, at the same lifetime.</summary>
    private static void Decorate<TService>(IServiceCollection services, Func<TService, IServiceProvider, TService> decorate)
        where TService : class
    {
        ServiceDescriptor registered = services.LastOrDefault(d => d.ServiceType == typeof(TService))
                                       ?? throw new InvalidOperationException($"{typeof(TService).Name} is not registered.");
        services.Remove(registered);
        services.Add(ServiceDescriptor.Describe(typeof(TService), provider => decorate((TService)Create(provider, registered), provider), registered.Lifetime));
    }

    private static object Create(IServiceProvider provider, ServiceDescriptor registered) =>
        registered.ImplementationInstance
        ?? registered.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, registered.ImplementationType!);
}
