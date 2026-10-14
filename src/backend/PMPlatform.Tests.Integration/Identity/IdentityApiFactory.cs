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
/// identity provider, with the clock and the log output under the test's control. <paramref name="services"/> adds what a
/// suite plays itself, such as a source module's approval outcome handler (TASK-035).
/// </summary>
public sealed class IdentityApiFactory(
    IReadOnlyDictionary<string, string?> settings, AdjustableTimeProvider clock, CapturedLogs logs, Action<IServiceCollection>? services = null)
    : WebApplicationFactory<Program>
{
    /// <summary>A test value, never a real key.</summary>
    public const string SigningKey = "identity-integration-test-signing-key-32-bytes-or-more";

    /// <summary>
    /// TASK-078: limits no suite reaches. Every test request comes from one address and a few users, so the shipped limits would
    /// throttle the suites themselves; the rate-limiting tests set their own in the factory's settings, which win.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string?> UnreachableRateLimits = new Dictionary<string, string?>
    {
        ["RateLimiting:Policies:SignIn:PermitLimit"] = "1000000",
        ["RateLimiting:Policies:SessionRefresh:PermitLimit"] = "1000000",
        ["RateLimiting:Policies:SensitiveWrite:PermitLimit"] = "1000000",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(UnreachableRateLimits).AddInMemoryCollection(settings));
        builder.ConfigureLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        builder.ConfigureTestServices(collection =>
        {
            collection.RemoveAll<TimeProvider>();
            collection.AddSingleton<TimeProvider>(clock);
            services?.Invoke(collection);
        });
    }
}
