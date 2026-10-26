using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A new profile on a canonical role; its first DRAFT copies <see cref="SourceVersionId"/>'s grants, or holds none.</summary>
public sealed record PermissionProfileCreateDraft(string Code, BilingualLabel Name, Guid BaseRoleId, Guid? SourceVersionId);

/// <summary>One cell of the grid: a catalogue permission at a data scope.</summary>
public sealed record PermissionGrantDraft(string PermissionCode, DataScope DataScope);

/// <summary>A DRAFT's whole grant set and its change summary, replacing what it held.</summary>
public sealed record PermissionGrantSetChange(IReadOnlyList<PermissionGrantDraft> Grants, NarrativeText? ChangeSummary);

/// <summary>How many active assignments moved to the version.</summary>
public sealed record PermissionProfileMigration(Guid PermissionProfileVersionId, int MigratedAssignments);

/// <summary>
/// ADM-008 permission profile authoring (TASK-110; the 24 Oct 2026 review, C4): AHDA composes a profile from the protected catalogue,
/// versions it and publishes it without a release. A version is edited only while DRAFT, by its author; a reviewer validates it and a
/// third person publishes it, neither holding the profile themselves; a PUBLISHED version never changes. Assignments stay on their
/// version until they are explicitly migrated. Every grant is held to the catalogue and to GrantPolicy at each step, and every change is
/// audited with its author, time and grant diff. R01–R08 and their shipped profiles are never deleted.
/// </summary>
public interface IPermissionProfileAuthoringService
{
    public Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> CreateProfileAsync(
        Guid actorId, PermissionProfileCreateDraft draft, CancellationToken cancellationToken);

    /// <summary>The next version of the profile, a DRAFT copying <paramref name="sourceVersionId"/>, or else its newest PUBLISHED version.</summary>
    public Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> CreateVersionAsync(
        Guid actorId, Guid profileId, Guid? sourceVersionId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> UpdateGrantsAsync(
        Guid actorId, Guid versionId, PermissionGrantSetChange change, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> ValidateAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> PublishAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> RetireAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Moves every active assignment of the profile's older versions onto this one, its newest PUBLISHED version.</summary>
    public Task<AdministrationResult<PermissionProfileMigration>> MigrateAssignmentsAsync(Guid actorId, Guid versionId, CancellationToken cancellationToken);
}
