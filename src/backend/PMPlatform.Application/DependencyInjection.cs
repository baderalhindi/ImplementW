using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application;

/// <summary>Registers application services. Each module adds its own handlers here as it is built.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // TASK-033: the formal audit trail every module records into, and the SIEM forwarder. The store and the SIEM
        // client are Infrastructure's; the request context is the API's.
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<ISiemForwarder, SiemForwarder>();

        // IdentityAccess (TASK-028): sign-in and ADM-041. The ports they use are implemented in Infrastructure.
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IIdentityIntegrationService, IdentityIntegrationService>();
        services.AddScoped<IAccessAudit, AccessAudit>();

        // TASK-030: the authorization engine, scoped so it reads the caller's grants once per request.
        services.TryAddSingleton(PermissionCatalogue.Platform);
        services.AddScoped<IAuthorizationEngine, AuthorizationEngine>();

        // TASK-031: FG-03 administration, ADM-002–013. The repositories and the mobile verifier are Infrastructure's.
        services.AddScoped<AdministrationAccess>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IMobileNumberVerificationService, MobileNumberVerificationService>();
        services.AddScoped<IUserContactDirectory, UserContactDirectory>();
        services.AddScoped<AccessRelationshipService>();
        services.AddScoped<IAccessRelationshipService>(provider => provider.GetRequiredService<AccessRelationshipService>());
        services.AddScoped<IProjectAccessLifecycle>(provider => provider.GetRequiredService<AccessRelationshipService>());
        services.AddScoped<IRoleAdministrationService, RoleAdministrationService>();
        services.AddScoped<IDepartmentAdministrationService, DepartmentAdministrationService>();
        services.AddScoped<IExternalEntityAdministrationService, ExternalEntityAdministrationService>();

        return services;
    }
}
