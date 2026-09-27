using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>The reference rules several services share. Each returns the issue with <c>field</c>, or null if the reference may be used.</summary>
internal static class ReferenceChecks
{
    /// <summary>A sponsor is a named AHDA person (ADR-013): an active internal user.</summary>
    public static async Task<FieldIssue?> ActiveInternalUserAsync(
        IUserAdministrationRepository users, Guid? userId, string field, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return null;
        }

        UserDetail? user = (await users.FindDetailAsync(id, cancellationToken).ConfigureAwait(false))?.Value;
        return user is null ? new FieldIssue(field, FieldIssue.NotFound)
            : user is not { UserType: UserType.Internal, Status: UserStatus.Active } ? new FieldIssue(field, FieldIssue.Inactive)
            : null;
    }

    public static async Task<FieldIssue?> ActiveDepartmentAsync(
        IDepartmentRepository departments, Guid? departmentId, string field, CancellationToken cancellationToken)
    {
        if (departmentId is not { } id)
        {
            return null;
        }

        DepartmentDetail? department = (await departments.FindDetailAsync(id, cancellationToken).ConfigureAwait(false))?.Value;
        return department is null ? new FieldIssue(field, FieldIssue.NotFound)
            : !department.IsActive ? new FieldIssue(field, FieldIssue.Inactive)
            : null;
    }

    /// <summary>An entity that is not RETIRED; a suspended one may still be referenced and is usable again once reactivated.</summary>
    public static async Task<FieldIssue?> UnretiredEntityAsync(
        IExternalEntityRepository entities, Guid? entityId, string field, CancellationToken cancellationToken)
    {
        if (entityId is not { } id)
        {
            return null;
        }

        ExternalEntityDetail? entity = (await entities.FindDetailAsync(id, cancellationToken).ConfigureAwait(false))?.Value;
        return entity is null ? new FieldIssue(field, FieldIssue.NotFound)
            : entity.Status == ExternalEntityStatus.Retired ? new FieldIssue(field, FieldIssue.Inactive)
            : null;
    }
}
