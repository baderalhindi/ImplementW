using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Authentication;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>Registers the directory, single sign-on and session-token adapters (TASK-028).</summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DirectoryOptions>().Bind(configuration.GetSection(DirectoryOptions.Section));
        services.AddOptions<SingleSignOnOptions>().Bind(configuration.GetSection(SingleSignOnOptions.Section));
        services.AddOptions<SessionTokenOptions>().Bind(configuration.GetSection(SessionTokenOptions.Section));

        services.AddHttpClient(OpenIdConnectProvider.HttpClientName, (provider, client) =>
            client.Timeout = provider.GetRequiredService<IOptions<SingleSignOnOptions>>().Value.Timeout);

        services.AddSingleton<SsoTransactionProtector>();
        services.AddSingleton<IDirectoryService, LdapDirectoryService>();
        services.AddSingleton<ISingleSignOnProvider, OpenIdConnectProvider>();
        services.AddSingleton<ISessionTokenService, JwtSessionTokenService>();

        return services;
    }
}
