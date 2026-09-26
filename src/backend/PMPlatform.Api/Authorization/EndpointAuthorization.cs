using Microsoft.AspNetCore.Authorization;
using PMPlatform.Application.Common.Authorization;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// TASK-030's acceptance criterion as a start-up check: every endpoint either is anonymous, acts only on the caller's own
/// session, or names the catalogue permission the engine checks. An endpoint that declares none, or names a permission
/// outside the catalogue, stops the API from starting.
/// </summary>
internal static class EndpointAuthorization
{
    public static void RequireEndpointAuthorization(this WebApplication app)
    {
        PermissionCatalogue catalogue = app.Services.GetRequiredService<PermissionCatalogue>();
        List<Endpoint> endpoints = [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)];

        List<string> undeclared = [.. endpoints.Where(e => DeclarationOf(e) is null).Select(e => e.DisplayName ?? e.ToString()!)];
        if (undeclared.Count > 0)
        {
            throw new InvalidOperationException(
                $"Endpoints without [AllowAnonymous], [AllowAnyAuthenticatedUser] or [RequirePermission]: {string.Join(", ", undeclared)}.");
        }

        List<string> unknown = [.. endpoints.SelectMany(PermissionsOf).Where(code => !catalogue.Contains(code)).Distinct(StringComparer.Ordinal)];
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException($"[RequirePermission] names a permission outside the catalogue: {string.Join(", ", unknown)}.");
        }
    }

    /// <summary>How the endpoint is protected: "anonymous", "session", "permission:&lt;codes&gt;", or null if it does not say.</summary>
    public static string? DeclarationOf(Endpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        List<string> permissions = [.. PermissionsOf(endpoint)];
        return endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ? "anonymous"
            : permissions.Count > 0 ? $"permission:{string.Join(",", permissions)}"
            : endpoint.Metadata.GetMetadata<AllowAnyAuthenticatedUserAttribute>() is not null ? "session"
            : null;
    }

    private static IEnumerable<string> PermissionsOf(Endpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizationRequirementData>()
            .SelectMany(data => data.GetRequirements())
            .OfType<PermissionRequirement>()
            .Select(requirement => requirement.PermissionCode);
}
