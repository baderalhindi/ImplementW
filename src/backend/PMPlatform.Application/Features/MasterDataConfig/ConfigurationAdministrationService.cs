using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// The FG-04 versioned configuration engine (TASK-034; Blueprint Section 12). A version's content is written only while
/// it is DRAFT and only by its author; a reviewer validates and a publisher publishes it, each checking the content
/// again. Publication sets the moment it takes effect, now or later, and after every version of the family published
/// before it, and touches no other version: history is never rewritten, it is superseded by date (ERD D-13).
/// </summary>
internal sealed partial class ConfigurationAdministrationService(
    IConfigurationRepository configuration,
    ConfigurationReferenceReader referenceReader,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<ConfigurationAdministrationService> logger) : IConfigurationAdministrationService
{
    public async Task<IReadOnlyList<ConfigurationFamilySummary>> ListFamiliesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ConfigurationFamily> families = await configuration.ListFamiliesAsync(cancellationToken).ConfigureAwait(false);
        ILookup<Guid, PublishedVersionWindow> windows = await WindowsAsync([.. families.Select(f => f.Id)], cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        return
        [
            .. families.Select(f => EffectiveVersionSelection.Select([.. windows[f.Id]], now).Window is { } active
                ? new ConfigurationFamilySummary(f.Id, f.Code, f.Name, active.VersionId, active.VersionNo)
                : new ConfigurationFamilySummary(f.Id, f.Code, f.Name, null, null)),
        ];
    }

    public async Task<AdministrationResult<ConfigurationVersionPage>> ListVersionsAsync(ConfigurationVersionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        (IReadOnlyList<ConfigurationVersionRow> rows, int totalCount) = await configuration.ListVersionsAsync(query, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, PublishedVersionWindow> windows = await WindowsAsync([.. rows.Select(r => r.Version.ConfigurationFamilyId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        return new ConfigurationVersionPage(
            [
                .. rows.Select(r => new ConfigurationVersionSummary(
                    r.Version.Id, r.Version.ConfigurationFamilyId, r.FamilyCode, r.Version.VersionNo, r.Version.LifecycleState,
                    r.Version.EffectiveFrom, r.Version.EffectiveTo, EffectivityOf(r.Version, windows, now))),
            ],
            query.Page.Page,
            query.Page.PageSize,
            totalCount);
    }

    public async Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        await DetailAsync(versionId, cancellationToken).ConfigureAwait(false) is { } version ? version : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> CreateVersionAsync(
        Guid actorId, ConfigurationVersionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (await configuration.FindFamilyAsync(draft.FamilyId, cancellationToken).ConfigureAwait(false) is not { } family)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, new FieldIssue("familyId", FieldIssue.NotFound));
        }

        ConfigurationContent? copied = null;
        if (draft.BasedOnVersionId is { } sourceId)
        {
            ConfigurationVersionRow? source = (await configuration.FindVersionAsync(sourceId, cancellationToken).ConfigureAwait(false))?.Value;
            if (source is null || source.Version.ConfigurationFamilyId != family.Id)
            {
                return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, new FieldIssue("basedOnVersionId", FieldIssue.NotFound));
            }

            copied = await configuration.ReadContentAsync(sourceId, cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ConfigurationVersion version = new()
        {
            Id = Guid.CreateVersion7(now),
            ConfigurationFamilyId = family.Id,
            VersionNo = await configuration.GetLatestVersionNoAsync(family.Id, cancellationToken).ConfigureAwait(false) + 1,
            ChangeSummary = draft.ChangeSummary,
            LifecycleState = GovernedLifecycleState.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
        };
        configuration.AddVersion(version);
        if (copied is not null)
        {
            await configuration.StageContentAsync(version.Id, copied, actorId, now, cancellationToken).ConfigureAwait(false);
        }

        return await SaveAsync(actorId, version, "created", MasterDataConfigAuditEvents.VersionCreated,
            [
                AuditAttribute.Change("configuration_family_id", null, version.ConfigurationFamilyId),
                AuditAttribute.Change("version_no", null, version.VersionNo),
                AuditAttribute.Change("based_on_version_id", null, draft.BasedOnVersionId),
                AuditAttribute.Change("content_sha256", null, copied is null ? null : ContentFingerprint.Of(copied)),
                AuditAttribute.Change("lifecycle_state", null, version.LifecycleState),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> UpdateVersionAsync(
        Guid actorId, Guid versionId, ConfigurationVersionChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        ConfigurationVersion? version = await configuration.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        if (GovernedRefusal.Of(GovernedLifecycle.CheckDraftEdit(version, actorId)) is { } refused)
        {
            return refused;
        }

        string familyCode = await FamilyCodeOfAsync(version, cancellationToken).ConfigureAwait(false);
        ConfigurationReferences references = await referenceReader.ReadAsync(changes.Content, cancellationToken).ConfigureAwait(false);
        if (ConfigurationContentRules.CheckEntries(familyCode, changes.Content, references) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ContentInvalid, InContent(issues));
        }

        string before = ContentFingerprint.Of(await configuration.ReadContentAsync(versionId, cancellationToken).ConfigureAwait(false));
        await configuration.StageContentAsync(versionId, changes.Content, actorId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        AuditAttribute?[] changed =
        [
            AuditAttribute.Change("change_summary", version.ChangeSummary?.Text, changes.ChangeSummary?.Text),
            AuditAttribute.Change("change_summary_lang", version.ChangeSummary?.Language, changes.ChangeSummary?.Language),
            AuditAttribute.Change("content_sha256", before, ContentFingerprint.Of(changes.Content)),
            .. ConfigurationSections.Counts(changes.Content).Where(c => c.Value > 0)
                .Select(c => AuditAttribute.Of($"{ConfigurationContentRules.PathOf(c.Key)}_count", c.Value)),
        ];
        version.ChangeSummary = changes.ChangeSummary;
        return await SaveAsync(actorId, version, "updated", MasterDataConfigAuditEvents.VersionUpdated, changed, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> ValidateVersionAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ConfigurationVersion? version = await configuration.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        if (GovernedRefusal.Of(GovernedLifecycle.CheckValidate(version, actorId)) is { } refused)
        {
            return refused;
        }

        if (await CheckContentAsync(version, cancellationToken).ConfigureAwait(false) is { } contentRefused)
        {
            return contentRefused;
        }

        GovernedLifecycle.Validate(version, actorId, timeProvider.GetUtcNow());
        return await SaveAsync(actorId, version, "validated", MasterDataConfigAuditEvents.VersionValidated,
            [AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Draft, version.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> PublishVersionAsync(
        Guid actorId, Guid versionId, DateTimeOffset? effectiveFrom, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ConfigurationVersion? version = await configuration.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        if (GovernedRefusal.Of(GovernedLifecycle.CheckPublish(version, actorId)) is { } refused)
        {
            return refused;
        }

        if (await CheckContentAsync(version, cancellationToken).ConfigureAwait(false) is { } contentRefused)
        {
            return contentRefused;
        }

        // A backdated version would change what a past transaction resolves to; one effective before an earlier
        // publication would reorder history. The database enforces the second too, under a lock on the family.
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset from = effectiveFrom ?? now;
        IReadOnlyList<PublishedVersionWindow> published = [.. (await WindowsAsync([version.ConfigurationFamilyId], cancellationToken).ConfigureAwait(false))[version.ConfigurationFamilyId]];
        if (from < now || published.Any(w => w.EffectiveFrom >= from))
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.EffectiveFromInvalid, new FieldIssue("effectiveFrom", FieldIssue.NotAllowed));
        }

        GovernedLifecycle.Publish(version, actorId, now);
        version.EffectiveFrom = from;
        return await SaveAsync(actorId, version, "published", MasterDataConfigAuditEvents.VersionPublished,
            [
                AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Validated, version.LifecycleState),
                AuditAttribute.Change("effective_from", null, version.EffectiveFrom),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> RetireVersionAsync(
        Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ConfigurationVersion? version = await configuration.FindVersionForUpdateAsync(versionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.NotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        GovernedLifecycleState before = version.LifecycleState;
        if (GovernedRefusal.Of(GovernedLifecycle.Retire(version, now)) is { } refused)
        {
            return refused;
        }

        if (version.EffectiveFrom is { } from)
        {
            version.EffectiveTo = from > now ? from : now;
        }

        return await SaveAsync(actorId, version, "retired", MasterDataConfigAuditEvents.VersionRetired,
            [
                AuditAttribute.Change("lifecycle_state", before, version.LifecycleState),
                AuditAttribute.Change("effective_to", null, version.EffectiveTo),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private static ConfigurationEffectivity? EffectivityOf(ConfigurationVersion version, ILookup<Guid, PublishedVersionWindow> windows, DateTimeOffset now)
    {
        List<PublishedVersionWindow> family = [.. windows[version.ConfigurationFamilyId]];
        return family.SingleOrDefault(w => w.VersionId == version.Id) is { } window
            ? EffectiveVersionSelection.EffectivityOf(window, family, now)
            : null;
    }

    private async Task<ILookup<Guid, PublishedVersionWindow>> WindowsAsync(IReadOnlyCollection<Guid> familyIds, CancellationToken cancellationToken) =>
        (await configuration.ListPublishedWindowsAsync(familyIds, cancellationToken).ConfigureAwait(false)).ToLookup(w => w.FamilyId, w => w.Window);

    private async Task<string> FamilyCodeOfAsync(ConfigurationVersion version, CancellationToken cancellationToken) =>
        (await configuration.FindFamilyAsync(version.ConfigurationFamilyId, cancellationToken).ConfigureAwait(false))?.Code
        ?? throw new InvalidOperationException($"Version {version.Id} names no family.");

    /// <summary>
    /// The content is checked again at each step: an item it references may have been retired since it was written. It is
    /// read in canonical order, so each issue's path names the entry a reader of the content sees at that index.
    /// </summary>
    private async Task<AdministrationError?> CheckContentAsync(ConfigurationVersion version, CancellationToken cancellationToken)
    {
        string familyCode = await FamilyCodeOfAsync(version, cancellationToken).ConfigureAwait(false);
        ConfigurationContent content = ContentOrder.Canonical(await configuration.ReadContentAsync(version.Id, cancellationToken).ConfigureAwait(false));
        ConfigurationReferences references = await referenceReader.ReadAsync(content, cancellationToken).ConfigureAwait(false);
        return ConfigurationContentRules.CheckEntries(familyCode, content, references) is { Count: > 0 } invalid
                ? AdministrationError.Rule(MasterDataConfigErrorCodes.ContentInvalid, InContent(invalid))
            : ConfigurationContentRules.CheckComplete(familyCode, content, references) is { Count: > 0 } incomplete
                ? AdministrationError.Rule(MasterDataConfigErrorCodes.ContentIncomplete, InContent(incomplete))
            : null;
    }

    /// <summary>The content is the <c>content</c> property of a version's representation, so each path is named from there (R-23).</summary>
    private static FieldIssue[] InContent(IEnumerable<FieldIssue> issues) =>
        [.. issues.Select(i => i with { Field = i.Field == "content" ? i.Field : $"content.{i.Field}" })];

    private async Task<Versioned<ConfigurationVersionDetail>?> DetailAsync(Guid versionId, CancellationToken cancellationToken)
    {
        if (await configuration.FindVersionAsync(versionId, cancellationToken).ConfigureAwait(false) is not { } row)
        {
            return null;
        }

        ConfigurationVersion version = row.Value.Version;
        ILookup<Guid, PublishedVersionWindow> windows = await WindowsAsync([version.ConfigurationFamilyId], cancellationToken).ConfigureAwait(false);
        ConfigurationContent content = ContentOrder.Canonical(await configuration.ReadContentAsync(versionId, cancellationToken).ConfigureAwait(false));
        return new Versioned<ConfigurationVersionDetail>(
            new ConfigurationVersionDetail(
                version.Id, version.ConfigurationFamilyId, row.Value.FamilyCode, version.VersionNo, version.EffectiveFrom, version.EffectiveTo,
                EffectivityOf(version, windows, timeProvider.GetUtcNow()), version.ChangeSummary, content, GovernedRecord.Of(version)),
            row.Version);
    }

    private async Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> SaveAsync(
        Guid actorId, ConfigurationVersion version, string change, string eventType, AuditAttribute?[] attributes, CancellationToken cancellationToken)
    {
        version.UpdatedAt = timeProvider.GetUtcNow();
        version.UpdatedBy = actorId;
        audit.Stage(ConfigurationAudit.Entry(eventType, actorId, nameof(ConfigurationVersion), version.Id, attributes));
        if ((await configuration.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogVersionChanged(logger, actorId, change, version.Id);
        return await DetailAsync(version.Id, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Configuration version {version.Id} was saved and cannot be read back.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuration administration: {ActorId} {Change} configuration version {VersionId}.")]
    private static partial void LogVersionChanged(ILogger logger, Guid actorId, string change, Guid versionId);
}
