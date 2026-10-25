using System.Globalization;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-008 permission profile authoring (TASK-110; the 24 Oct 2026 review, C4). A profile version follows the governed lifecycle
/// (<see cref="GovernedLifecycle"/>): its author edits the grid while DRAFT, a second person validates it and a third publishes it. Neither
/// the reviewer nor the publisher may hold the profile — no one approves access they would receive themselves. The grid is checked
/// against the catalogue and <see cref="GrantPolicy"/> when edited, validated and published, so a profile can neither name a permission
/// outside the catalogue nor give R01 business data or R08 approval, reports, integrations or the SAR amounts. A PUBLISHED version never
/// changes; its assignments stay on it until migrated here. Every step is a PERMISSION_CHANGE audit event; a grid edit carries its diff,
/// one attribute per permission whose scope changed, from the old scope to the new.
/// </summary>
internal sealed partial class PermissionProfileAuthoringService(
    IRoleAdministrationRepository roles,
    PermissionCatalogue catalogue,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<PermissionProfileAuthoringService> logger) : IPermissionProfileAuthoringService
{
    private const string Module = "IdentityAccess";

    public async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> CreateProfileAsync(
        Guid actorId, PermissionProfileCreateDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (await roles.FindRoleAsync(draft.BaseRoleId, cancellationToken).ConfigureAwait(false) is not { } role)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue("baseRoleId", FieldIssue.NotFound));
        }

        if (await roles.IsProfileCodeInUseAsync(draft.Code, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Conflict(IdentityAccessErrorCodes.DuplicateKey, new FieldIssue("code", FieldIssue.Duplicate));
        }

        IReadOnlyList<PermissionProfileGrantDetail> source = [];
        if (draft.SourceVersionId is { } sourceVersionId)
        {
            if (await roles.FindVersionDetailAsync(sourceVersionId, cancellationToken).ConfigureAwait(false) is not { } sourceVersion)
            {
                return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue("sourceVersionId", FieldIssue.NotFound));
            }

            source = sourceVersion.Value.Grants;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        PermissionProfile profile = new()
        {
            Id = Guid.CreateVersion7(now),
            Code = draft.Code,
            Name = draft.Name,
            BaseRoleId = draft.BaseRoleId,
            IsShippedDefault = false,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        roles.Add(profile);
        audit.Stage(Entry(IdentityAccessAuditEvents.PermissionProfileCreated, actorId, nameof(PermissionProfile), profile.Id,
        [
            AuditAttribute.Of("profile_code", profile.Code),
            AuditAttribute.Of(IdentityAccessAuditAttributes.RoleCode, role.Value.Code),
        ]));
        PermissionProfileVersion version = AddDraft(actorId, profile.Id, 1, source, draft.SourceVersionId, now);
        return await SaveAsync(version.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> CreateVersionAsync(
        Guid actorId, Guid profileId, Guid? sourceVersionId, CancellationToken cancellationToken)
    {
        if (await roles.FindProfileForUpdateAsync(profileId, cancellationToken).ConfigureAwait(false) is null)
        {
            return AdministrationError.NotFound;
        }

        IReadOnlyList<PermissionProfileVersion> versions = await roles.ListVersionsAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (versions.Any(v => v.LifecycleState is GovernedLifecycleState.Draft or GovernedLifecycleState.Validated))
        {
            return AdministrationError.Conflict(IdentityAccessErrorCodes.ProfileDraftOpen);
        }

        Guid? sourceId = sourceVersionId ?? versions.FirstOrDefault(v => v.LifecycleState == GovernedLifecycleState.Published)?.Id;
        IReadOnlyList<PermissionProfileGrantDetail> source = [];
        if (sourceId is { } id)
        {
            if (await roles.FindVersionDetailAsync(id, cancellationToken).ConfigureAwait(false) is not { } sourceVersion)
            {
                return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue("sourceVersionId", FieldIssue.NotFound));
            }

            source = sourceVersion.Value.Grants;
        }

        PermissionProfileVersion version = AddDraft(actorId, profileId, versions.Count == 0 ? 1 : versions.Max(v => v.VersionNo) + 1, source, sourceId,
            timeProvider.GetUtcNow());
        return await SaveAsync(version.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        await roles.FindVersionDetailAsync(versionId, cancellationToken).ConfigureAwait(false) is { } version ? version : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> UpdateGrantsAsync(
        Guid actorId, Guid versionId, PermissionGrantSetChange change, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        PermissionProfileVersion? version = await roles.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        if (Refusal(GovernedLifecycle.CheckDraftEdit(version, actorId)) is { } refused)
        {
            return refused;
        }

        if (await GrantIssuesAsync(version.PermissionProfileId, change.Grants, cancellationToken).ConfigureAwait(false) is { Length: > 0 } issues)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ProfileGrantInvalid, issues);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Dictionary<Guid, PermissionProfileGrant> held = (await roles.ListGrantsForUpdateAsync(versionId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(g => g.PermissionId);
        IReadOnlyList<PermissionSummary> permissions = await roles.ListPermissionsAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, Guid> idOf = permissions.ToDictionary(p => p.Code, p => p.Id, StringComparer.Ordinal);
        Dictionary<Guid, string> codeOf = permissions.ToDictionary(p => p.Id, p => p.Code);
        Dictionary<Guid, PermissionGrantDraft> wanted = change.Grants.ToDictionary(g => idOf[g.PermissionCode]);
        List<AuditAttribute> diff = [];
        foreach ((Guid permissionId, PermissionProfileGrant grant) in held.Where(h => !wanted.ContainsKey(h.Key)))
        {
            roles.Remove(grant);
            diff.Add(GrantChange(codeOf[permissionId], grant.DataScope, null));
        }

        foreach ((Guid permissionId, PermissionGrantDraft draft) in wanted)
        {
            if (!held.TryGetValue(permissionId, out PermissionProfileGrant? grant))
            {
                roles.Add(NewGrant(actorId, versionId, permissionId, draft.DataScope, now));
                diff.Add(GrantChange(codeOf[permissionId], null, draft.DataScope));
            }
            else if (grant.DataScope != draft.DataScope)
            {
                diff.Add(GrantChange(codeOf[permissionId], grant.DataScope, draft.DataScope));
                grant.DataScope = draft.DataScope;
                Touch(grant, actorId, now);
            }
        }

        AuditAttribute? summary = AuditAttribute.Change("change_summary", version.ChangeSummary?.Text, change.ChangeSummary?.Text);
        version.ChangeSummary = change.ChangeSummary;
        Touch(version, actorId, now);
        audit.Stage(Entry(IdentityAccessAuditEvents.PermissionProfileGrantsChanged, actorId, nameof(PermissionProfileVersion), version.Id,
            [.. diff.OrderBy(a => a.Name, StringComparer.Ordinal), .. summary is null ? Array.Empty<AuditAttribute>() : [summary]]));
        return await SaveAsync(version.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> ValidateAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        PermissionProfileVersion? version = await roles.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        if (await ApprovalRefusalAsync(version, actorId, GovernedLifecycle.CheckValidate(version, actorId), cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        GovernedLifecycle.Validate(version, actorId, timeProvider.GetUtcNow());
        return await TransitionAsync(actorId, version, IdentityAccessAuditEvents.PermissionProfileVersionValidated, GovernedLifecycleState.Draft, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> PublishAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        PermissionProfileVersion? version = await roles.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        if (await ApprovalRefusalAsync(version, actorId, GovernedLifecycle.CheckPublish(version, actorId), cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        GovernedLifecycle.Publish(version, actorId, timeProvider.GetUtcNow());
        return await TransitionAsync(actorId, version, IdentityAccessAuditEvents.PermissionProfileVersionPublished, GovernedLifecycleState.Validated, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> RetireAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        PermissionProfileVersion? version = await roles.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        // A version people hold, or a shipped default's last published version, stays: R01–R08 are never left without a profile.
        if (version.LifecycleState == GovernedLifecycleState.Published
            && (await roles.CountActiveAssignmentsAsync(version.Id, cancellationToken).ConfigureAwait(false) > 0
                || await IsShippedDefaultsLastPublishedAsync(version, cancellationToken).ConfigureAwait(false)))
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ProfileVersionInUse);
        }

        GovernedLifecycleState from = version.LifecycleState;
        return Refusal(GovernedLifecycle.Retire(version, timeProvider.GetUtcNow())) is { } refused
            ? refused
            : await TransitionAsync(actorId, version, IdentityAccessAuditEvents.PermissionProfileVersionRetired, from, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<PermissionProfileMigration>> MigrateAssignmentsAsync(Guid actorId, Guid versionId, CancellationToken cancellationToken)
    {
        PermissionProfileVersion? version = await roles.FindVersionForUpdateAsync(versionId, null, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        IReadOnlyList<PermissionProfileVersion> versions = await roles.ListVersionsAsync(version.PermissionProfileId, cancellationToken).ConfigureAwait(false);
        if (version.LifecycleState != GovernedLifecycleState.Published
            || versions.FirstOrDefault(v => v.LifecycleState == GovernedLifecycleState.Published)?.Id != version.Id)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.MigrationTargetInvalid);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<AccessRelationship> assignments =
            await roles.ListActiveAssignmentsForUpdateAsync(version.PermissionProfileId, version.Id, cancellationToken).ConfigureAwait(false);
        foreach (AccessRelationship assignment in assignments)
        {
            audit.Stage(Entry(IdentityAccessAuditEvents.PermissionProfileAssignmentsMigrated, actorId, nameof(AccessRelationship), assignment.Id,
            [
                AuditAttribute.Of(IdentityAccessAuditAttributes.UserId, assignment.UserId),
                AuditAttribute.Change(IdentityAccessAuditAttributes.PermissionProfileVersionId, assignment.PermissionProfileVersionId, version.Id),
            ]));
            assignment.PermissionProfileVersionId = version.Id;
            Touch(assignment, actorId, now);
        }

        if ((await roles.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogMigrated(logger, actorId, version.Id, assignments.Count);
        return new PermissionProfileMigration(version.Id, assignments.Count);
    }

    /// <summary>The refusal of a validation or publication: the lifecycle's, then self-approval, then the grid's rules, which may have moved since it was edited.</summary>
    private async Task<AdministrationError?> ApprovalRefusalAsync(
        PermissionProfileVersion version, Guid actorId, GovernedTransitionOutcome check, CancellationToken cancellationToken)
    {
        if (Refusal(check) is { } refused)
        {
            return refused;
        }

        if (await roles.HoldsProfileAsync(actorId, version.PermissionProfileId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.SelfApproval);
        }

        IReadOnlyList<PermissionGrantDraft> grants = [.. (await roles.FindVersionDetailAsync(version.Id, cancellationToken).ConfigureAwait(false))!.Value.Grants
            .Select(g => new PermissionGrantDraft(g.PermissionCode, g.DataScope))];
        return await GrantIssuesAsync(version.PermissionProfileId, grants, cancellationToken).ConfigureAwait(false) is { Length: > 0 } issues
            ? AdministrationError.Rule(IdentityAccessErrorCodes.ProfileGrantInvalid, issues)
            : null;
    }

    /// <summary>Every rule the grid breaks: a permission outside the catalogue, a permission twice, one the base role may never hold (GrantPolicy).</summary>
    private async Task<FieldIssue[]> GrantIssuesAsync(Guid profileId, IReadOnlyList<PermissionGrantDraft> grants, CancellationToken cancellationToken)
    {
        string baseRoleCode = (await roles.FindProfileAsync(profileId, cancellationToken).ConfigureAwait(false))!.BaseRoleCode;
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<FieldIssue> issues = [];
        for (int i = 0; i < grants.Count; i++)
        {
            string field = $"grants[{i.ToString(CultureInfo.InvariantCulture)}].permissionCode";
            string code = grants[i].PermissionCode;
            if (!catalogue.Contains(code))
            {
                issues.Add(new FieldIssue(field, FieldIssue.NotFound));
            }
            else if (!seen.Add(code))
            {
                issues.Add(new FieldIssue(field, FieldIssue.Duplicate));
            }
            else if (!GrantPolicy.Permits(baseRoleCode, UserType.Internal, catalogue.Get(code)))
            {
                issues.Add(new FieldIssue(field, FieldIssue.NotAllowed));
            }
        }

        return [.. issues];
    }

    private async Task<bool> IsShippedDefaultsLastPublishedAsync(PermissionProfileVersion version, CancellationToken cancellationToken) =>
        (await roles.FindProfileAsync(version.PermissionProfileId, cancellationToken).ConfigureAwait(false))!.IsShippedDefault
        && (await roles.ListVersionsAsync(version.PermissionProfileId, cancellationToken).ConfigureAwait(false))
            .Count(v => v.LifecycleState == GovernedLifecycleState.Published) == 1;

    private PermissionProfileVersion AddDraft(
        Guid actorId, Guid profileId, int versionNo, IReadOnlyList<PermissionProfileGrantDetail> grants, Guid? sourceVersionId, DateTimeOffset now)
    {
        PermissionProfileVersion version = new()
        {
            Id = Guid.CreateVersion7(now),
            PermissionProfileId = profileId,
            VersionNo = versionNo,
            LifecycleState = GovernedLifecycleState.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        roles.Add(version);
        foreach (PermissionProfileGrantDetail grant in grants)
        {
            roles.Add(NewGrant(actorId, version.Id, grant.PermissionId, grant.DataScope, now));
        }

        audit.Stage(Entry(IdentityAccessAuditEvents.PermissionProfileVersionDrafted, actorId, nameof(PermissionProfileVersion), version.Id,
        [
            AuditAttribute.Of("version_no", versionNo),
            sourceVersionId is { } source ? AuditAttribute.Of("source_version_id", source) : null,
            AuditAttribute.Of("grant_count", grants.Count),
        ]));
        return version;
    }

    private static PermissionProfileGrant NewGrant(Guid actorId, Guid versionId, Guid permissionId, DataScope scope, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        PermissionProfileVersionId = versionId,
        PermissionId = permissionId,
        DataScope = scope,
        CreatedAt = now,
        CreatedBy = actorId,
        UpdatedAt = now,
        UpdatedBy = actorId,
    };

    private async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> TransitionAsync(
        Guid actorId, PermissionProfileVersion version, string eventType, GovernedLifecycleState from, CancellationToken cancellationToken)
    {
        Touch(version, actorId, timeProvider.GetUtcNow());
        audit.Stage(Entry(eventType, actorId, nameof(PermissionProfileVersion), version.Id,
            [AuditAttribute.Change("lifecycle_state", from, version.LifecycleState)]));
        return await SaveAsync(version.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdministrationResult<Versioned<PermissionProfileVersionDetail>>> SaveAsync(Guid versionId, CancellationToken cancellationToken)
    {
        return (await roles.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError
            ? saveError
            : await roles.FindVersionDetailAsync(versionId, cancellationToken).ConfigureAwait(false)
              ?? throw new InvalidOperationException($"Permission profile version {versionId} was saved and cannot be read back.");
    }

    private static AuditAttribute GrantChange(string permissionCode, DataScope? from, DataScope? to) =>
        AuditAttribute.Change($"grant_{permissionCode.ToLowerInvariant()}", from, to)
        ?? throw new InvalidOperationException("A grant change changes something.");

    private static AdministrationError? Refusal(GovernedTransitionOutcome outcome) => outcome switch
    {
        GovernedTransitionOutcome.Applied => null,
        GovernedTransitionOutcome.InvalidTransition => AdministrationError.InvalidTransition,
        GovernedTransitionOutcome.TerminalState => AdministrationError.TerminalState,
        GovernedTransitionOutcome.SeparationOfDuties => AdministrationError.Rule(IdentityAccessErrorCodes.SeparationOfDuties),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown governed transition outcome."),
    };

    private static void Touch(AuditedEntity row, Guid actorId, DateTimeOffset now)
    {
        row.UpdatedAt = now;
        row.UpdatedBy = actorId;
    }

    private static AuditEntry Entry(string eventType, Guid actorId, string subjectType, Guid subjectId, IEnumerable<AuditAttribute?> attributes) =>
        new(AuditEventClass.PermissionChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, subjectType, subjectId),
            Attributes = [.. attributes.OfType<AuditAttribute>()],
        };

    [LoggerMessage(Level = LogLevel.Information, Message = "Permission profiles: {ActorId} migrated {Count} assignments to version {VersionId}.")]
    private static partial void LogMigrated(ILogger logger, Guid actorId, Guid versionId, int count);
}
