using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>Registers the directory, single sign-on and session-token adapters (TASK-028) and the MFA provider and policy (TASK-029).</summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DirectoryOptions>().Bind(configuration.GetSection(DirectoryOptions.Section));
        services.AddOptions<SingleSignOnOptions>().Bind(configuration.GetSection(SingleSignOnOptions.Section));
        services.AddOptions<SessionTokenOptions>().Bind(configuration.GetSection(SessionTokenOptions.Section));
        services.AddOptions<MultiFactorProviderOptions>().Bind(configuration.GetSection(MultiFactorProviderOptions.Section));

        // CTL-07's minimum scope is not configurable away: an environment whose policy does not require MFA for R01
        // does not start.
        services.AddOptions<MultiFactorPolicy>()
            .Bind(configuration.GetSection(MultiFactorPolicy.Section))
            .Validate(
                policy => policy.RequiredRoles.Contains(MultiFactorPolicy.MinimumRequiredRole, StringComparer.Ordinal),
                $"{MultiFactorPolicy.Section}:RequiredRoles must include {MultiFactorPolicy.MinimumRequiredRole} (CTL-07).")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<MultiFactorPolicy>>().Value);

        services.AddHttpClient(OpenIdConnectProvider.HttpClientName, (provider, client) =>
            client.Timeout = provider.GetRequiredService<IOptions<SingleSignOnOptions>>().Value.Timeout);
        services.AddHttpClient(HttpMultiFactorProvider.HttpClientName, (provider, client) =>
            client.Timeout = provider.GetRequiredService<IOptions<MultiFactorProviderOptions>>().Value.Timeout);

        services.AddSingleton<SsoTransactionProtector>();
        services.AddSingleton<IDirectoryService, LdapDirectoryService>();
        services.AddSingleton<ISingleSignOnProvider, OpenIdConnectProvider>();
        services.AddSingleton<ISessionTokenService, JwtSessionTokenService>();
        services.AddSingleton<IMultiFactorProvider, HttpMultiFactorProvider>();

        return services;
    }
}
