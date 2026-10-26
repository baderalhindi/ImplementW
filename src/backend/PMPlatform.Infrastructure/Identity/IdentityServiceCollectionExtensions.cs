using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Infrastructure.Hosting;
using PMPlatform.Infrastructure.Identity.Nafath;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Registers the directory, single sign-on and session-token adapters (TASK-028), the MFA provider and policy (TASK-029),
/// and the Nafath identity verification adapter and policy (TASK-068), each observed as FG-05 telemetry (TASK-075).
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DirectoryOptions>().Bind(configuration.GetSection(DirectoryOptions.Section));
        services.AddOptions<SingleSignOnOptions>().Bind(configuration.GetSection(SingleSignOnOptions.Section));
        services.AddOptions<SessionTokenOptions>().Bind(configuration.GetSection(SessionTokenOptions.Section));
        services.AddOptions<MultiFactorProviderOptions>().Bind(configuration.GetSection(MultiFactorProviderOptions.Section));
        services.AddOptions<NafathOptions>().Bind(configuration.GetSection(NafathOptions.Section));
        services.AddOptions<IdentityVerificationPolicy>().Bind(configuration.GetSection(IdentityVerificationPolicy.Section));
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<IdentityVerificationPolicy>>().Value);

        // CTL-07's minimum scope is not configurable away: an environment whose policy does not require MFA for R01
        // does not start.
        services.AddOptions<MultiFactorPolicy>()
            .Bind(configuration.GetSection(MultiFactorPolicy.Section))
            .Validate(
                policy => policy.RequiredRoles.Contains(MultiFactorPolicy.MinimumRequiredRole, StringComparer.Ordinal),
                $"{MultiFactorPolicy.Section}:RequiredRoles must include {MultiFactorPolicy.MinimumRequiredRole} (CTL-07).")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<MultiFactorPolicy>>().Value);

        // R-42: each provider call carries the correlation id of the work it is part of (TASK-075).
        services.TryAddTransient<CorrelationPropagationHandler>();
        services.AddHttpClient(OpenIdConnectProvider.HttpClientName, (provider, client) =>
                client.Timeout = provider.GetRequiredService<IOptions<SingleSignOnOptions>>().Value.Timeout)
            .AddHttpMessageHandler<CorrelationPropagationHandler>();
        services.AddHttpClient(HttpMultiFactorProvider.HttpClientName, (provider, client) =>
                client.Timeout = provider.GetRequiredService<IOptions<MultiFactorProviderOptions>>().Value.Timeout)
            .AddHttpMessageHandler<CorrelationPropagationHandler>();
        services.AddHttpClient(NafathIdentityVerifier.HttpClientName, (provider, client) =>
                client.Timeout = provider.GetRequiredService<IOptions<NafathOptions>>().Value.Timeout)
            .AddHttpMessageHandler<CorrelationPropagationHandler>();

        services.AddSingleton<IDirectoryService, LdapDirectoryService>();
        services.AddSingleton<ISingleSignOnProvider, OpenIdConnectProvider>();
        services.AddSingleton<ISessionTokenService, JwtSessionTokenService>();
        services.AddSingleton<IMultiFactorProvider, HttpMultiFactorProvider>();
        services.AddSingleton<IIdentityVerificationProvider, NafathIdentityVerifier>();

        // TASK-075: every call to these providers is FG-05 telemetry.
        services.ObserveIdentityIntegrations();

        return services;
    }
}
