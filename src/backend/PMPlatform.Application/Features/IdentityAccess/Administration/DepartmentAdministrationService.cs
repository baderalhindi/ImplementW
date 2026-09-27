using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-011/012 (TASK-031). The tree has no cycle. Sign-in resolves an internal user's department by its directory
/// reference (ADR-007), so no two active departments share one.
/// </summary>
internal sealed partial class DepartmentAdministrationService(
    IDepartmentRepository departments,
    AdministrationAccess access,
    TimeProvider timeProvider,
    ILogger<DepartmentAdministrationService> logger) : IDepartmentAdministrationService
{
    public async Task<AdministrationResult<DepartmentPage>> ListAsync(Guid actorId, DepartmentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await access.CheckCollectionAsync(actorId, PermissionCatalogue.OrganizationView, cancellationToken).ConfigureAwait(false) is { } denied
            ? denied
            : await departments.ListAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DepartmentDetail>>> GetAsync(Guid actorId, Guid departmentId, CancellationToken cancellationToken)
    {
        Versioned<DepartmentDetail>? department = await departments.FindDetailAsync(departmentId, cancellationToken).ConfigureAwait(false);
        return department is null ? AdministrationError.NotFound
            : await access.CheckRecordAsync(actorId, PermissionCatalogue.OrganizationView, SubjectOf(departmentId), cancellationToken).ConfigureAwait(false) is { } denied ? denied
            : department;
    }

    public async Task<AdministrationResult<Versioned<DepartmentDetail>>> CreateAsync(Guid actorId, DepartmentDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (await access.CheckNewRecordAsync(actorId, PermissionCatalogue.OrganizationManage, new() { DepartmentId = draft.ParentDepartmentId }, cancellationToken)
                .ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        if (await ReferenceChecks.ActiveDepartmentAsync(departments, draft.ParentDepartmentId, "parentDepartmentId", cancellationToken).ConfigureAwait(false) is { } parentIssue)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, parentIssue);
        }

        if (await DirectoryReferenceInUseAsync(draft.DirectoryReference, exceptDepartmentId: null, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Duplicate("directoryReference");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Department department = new()
        {
            Id = Guid.CreateVersion7(now),
            Code = draft.Code,
            Name = draft.Name,
            ParentDepartmentId = draft.ParentDepartmentId,
            DirectoryReference = draft.DirectoryReference,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        departments.Add(department);
        return await SaveAsync(actorId, department, "created", cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DepartmentDetail>>> UpdateAsync(
        Guid actorId, Guid departmentId, DepartmentChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Department? department = await departments.FindForUpdateAsync(departmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, department, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (changes.ParentDepartmentId != department!.ParentDepartmentId)
        {
            if (await ReferenceChecks.ActiveDepartmentAsync(departments, changes.ParentDepartmentId, "parentDepartmentId", cancellationToken).ConfigureAwait(false) is { } parentIssue)
            {
                return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, parentIssue);
            }

            if (IsOwnAncestor(departmentId, changes.ParentDepartmentId, await departments.GetParentsAsync(cancellationToken).ConfigureAwait(false)))
            {
                return AdministrationError.Rule(IdentityAccessErrorCodes.DepartmentCycle, new FieldIssue("parentDepartmentId", FieldIssue.NotAllowed));
            }
        }

        if (department.IsActive && await DirectoryReferenceInUseAsync(changes.DirectoryReference, departmentId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Duplicate("directoryReference");
        }

        department.Name = changes.Name;
        department.ParentDepartmentId = changes.ParentDepartmentId;
        department.DirectoryReference = changes.DirectoryReference;
        return await SaveAsync(actorId, department, "updated", cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DepartmentDetail>>> ActivateAsync(Guid actorId, Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Department? department = await departments.FindForUpdateAsync(departmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, department, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (department!.IsActive)
        {
            return AdministrationError.InvalidTransition;
        }

        if (await DirectoryReferenceInUseAsync(department.DirectoryReference, departmentId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Duplicate("directoryReference");
        }

        department.IsActive = true;
        return await SaveAsync(actorId, department, "activated", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Users, projects and assignments that name the department keep naming it; sign-in no longer resolves to it.</summary>
    public async Task<AdministrationResult<Versioned<DepartmentDetail>>> DeactivateAsync(Guid actorId, Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Department? department = await departments.FindForUpdateAsync(departmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, department, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!department!.IsActive)
        {
            return AdministrationError.InvalidTransition;
        }

        department.IsActive = false;
        return await SaveAsync(actorId, department, "deactivated", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="parentId"/> is <paramref name="departmentId"/> or one of its descendants, or its ancestry already loops.</summary>
    internal static bool IsOwnAncestor(Guid departmentId, Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        HashSet<Guid> seen = [];
        for (Guid? current = parentId; current is { } id; current = parents.GetValueOrDefault(id))
        {
            if (id == departmentId || !seen.Add(id))
            {
                return true;
            }
        }

        return false;
    }

    private static AuthorizationSubject SubjectOf(Guid departmentId) => new() { DepartmentId = departmentId };

    private async Task<AdministrationError?> CheckWritableAsync(Guid actorId, Department? department, CancellationToken cancellationToken) =>
        department is null ? AdministrationError.NotFound
        : await access.CheckRecordAsync(actorId, PermissionCatalogue.OrganizationManage, SubjectOf(department.Id), cancellationToken).ConfigureAwait(false);

    private async Task<bool> DirectoryReferenceInUseAsync(string? directoryReference, Guid? exceptDepartmentId, CancellationToken cancellationToken) =>
        directoryReference is not null
        && await departments.IsDirectoryReferenceInUseAsync(directoryReference, exceptDepartmentId, cancellationToken).ConfigureAwait(false);

    private async Task<AdministrationResult<Versioned<DepartmentDetail>>> SaveAsync(Guid actorId, Department department, string change, CancellationToken cancellationToken)
    {
        department.UpdatedAt = timeProvider.GetUtcNow();
        department.UpdatedBy = actorId;
        if ((await departments.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogDepartmentChanged(logger, actorId, change, department.Id);
        return await departments.FindDetailAsync(department.Id, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Department {department.Id} was saved and cannot be read back.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization administration: {ActorId} {Change} department {DepartmentId}.")]
    private static partial void LogDepartmentChanged(ILogger logger, Guid actorId, string change, Guid departmentId);
}
