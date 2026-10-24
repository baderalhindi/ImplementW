using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.DataImport;

/// <summary>TASK-102: every register a workbook can load has its template and its import, and both answer DATA_IMPORT alone.</summary>
[Collection(IdentitySuite.Name)]
public sealed class ImportEndpointTests(IdentityTestHost host)
{
    [Fact]
    public void EveryRegisterHasATemplateAndAnImportBothProtectedByDataImport()
    {
        Dictionary<string, string?> imports = host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.EndsWith("/import-template/content", StringComparison.Ordinal) == true
                        || e.RoutePattern.RawText?.EndsWith("/imports", StringComparison.Ordinal) == true)
            .ToDictionary(e => e.RoutePattern.RawText!, EndpointAuthorization.DeclarationOf);
        using IServiceScope scope = host.Api.Services.CreateScope();
        int registers = scope.ServiceProvider.GetServices<IImportRegister>().Count();

        Assert.Equal(2 * registers, imports.Count);
        Assert.All(imports, e => Assert.Equal("permission:DATA_IMPORT", e.Value));
        Assert.Equal(
            imports.Keys.Where(k => k.EndsWith("/imports", StringComparison.Ordinal)).Select(k => k[..^"/imports".Length]).Order(),
            imports.Keys.Where(k => k.EndsWith("/import-template/content", StringComparison.Ordinal)).Select(k => k[..^"/import-template/content".Length]).Order());
    }

    [Fact]
    public void RegisterCodesAreUnique()
    {
        using IServiceScope scope = host.Api.Services.CreateScope();
        string[] codes = [.. scope.ServiceProvider.GetServices<IImportRegister>().Select(r => r.Code)];

        Assert.Equal(codes.Distinct(StringComparer.Ordinal).Count(), codes.Length);
    }
}
