using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.DataImport;

/// <summary>TASK-102: every register a workbook can load has its template and its import, each protected: DATA_IMPORT for a platform register, the create permission for a project's.</summary>
[Collection(IdentitySuite.Name)]
public sealed class ImportEndpointTests(IdentityTestHost host)
{
    [Fact]
    public void EveryRegisterHasATemplateAndAnImportBothProtectedByItsOwnPermission()
    {
        Dictionary<string, string?> imports = host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            // The project import batches of the 2 Nov 2026 review (R3-4) are not a migration register: PROJECT_IMPORT protects them.
            .Where(e => e.RoutePattern.RawText?.StartsWith("api/v1/project-import-batches", StringComparison.Ordinal) != true)
            .Where(e => e.RoutePattern.RawText?.EndsWith("/import-template/content", StringComparison.Ordinal) == true
                        || e.RoutePattern.RawText?.EndsWith("/imports", StringComparison.Ordinal) == true)
            .ToDictionary(e => e.RoutePattern.RawText!, EndpointAuthorization.DeclarationOf);
        using IServiceScope scope = host.Api.Services.CreateScope();
        int registers = scope.ServiceProvider.GetServices<IImportRegister>().Count();

        Assert.Equal(2 * registers, imports.Count);
        using IServiceScope permissions = host.Api.Services.CreateScope();
        string[] allowed = [.. permissions.ServiceProvider.GetServices<IImportRegister>().Select(r => $"permission:{r.Permission}").Distinct()];
        Assert.All(imports, e => Assert.Contains(e.Value, allowed));
        Assert.Equal(
            ["permission:CONCERN_RAISE", "permission:DATA_IMPORT", "permission:KPI_RECORD", "permission:RISK_MANAGE", "permission:SCHEDULE_EDIT", "permission:TASK_MANAGE"],
            allowed.Order(StringComparer.Ordinal));
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
