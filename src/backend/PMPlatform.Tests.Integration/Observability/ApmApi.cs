using Microsoft.AspNetCore.Mvc.Testing;
using PMPlatform.Api.Observability;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Observability;

/// <summary>The API under test with APM on, sending to <see cref="ApmIngestStub"/>, and the routes the suite calls.</summary>
internal static class ApmApi
{
    public const string EnvironmentTag = "test";
    public const string ClientErrorReports = "/api/v1/client-error-reports";
    public const string DiagnosticExceptions = ApmRegistration.DiagnosticExceptionPath;

    /// <summary>
    /// <c>APM_DSN</c> is read before the host is built, to decide whether the SDK is registered at all, so it is a host setting here:
    /// configuration a test factory adds later is not visible that early.
    /// </summary>
    public static WebApplicationFactory<Program> CreateApi(this IdentityTestHost host, ApmIngestStub stub, params (string Key, string Value)[] settings)
    {
        Dictionary<string, string?> all = new()
        {
            [ApmOptions.EnvironmentTagVariable] = EnvironmentTag,
            [$"{ApmOptions.Section}:DiagnosticExceptionEndpoint"] = "true",
        };
        foreach ((string key, string value) in settings)
        {
            all[key] = value;
        }

        return host.CreateApi(all).WithWebHostBuilder(builder => builder.UseSetting(ApmOptions.DsnVariable, stub.Dsn));
    }
}
