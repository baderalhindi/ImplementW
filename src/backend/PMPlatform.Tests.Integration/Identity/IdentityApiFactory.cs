using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// PMPlatform.Api hosted in the test process against the identity test database, the test directory and the test
/// identity provider, with the clock and the log output under the test's control.
/// </summary>
public sealed class IdentityApiFactory(IReadOnlyDictionary<string, string?> settings, AdjustableTimeProvider clock, CapturedLogs logs)
    : WebApplicationFactory<Program>
{
    /// <summary>A test value, never a real key.</summary>
    public const string SigningKey = "identity-integration-test-signing-key-32-bytes-or-more";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings));
        builder.ConfigureLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
    }
}
