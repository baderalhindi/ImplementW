using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// ADR-008's source mode per project and per field (TASK-052). A field without a row is MANUAL. INTEGRATED is a one-way door:
/// "once a field is integrated, manual substitution during a source outage is not permitted", and switching the field back to
/// MANUAL or HYBRID would be exactly that substitution.
/// </summary>
internal sealed class FinancialSourceModeService(
    IFinancialKpiRepository repository, IProjectFactsReader projects, FinancialKpiAccess access, IAuditTrail audit, TimeProvider timeProvider) : IFinancialSourceModeService
{
    public async Task<FinancialSourceModePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not { } project
            || !await access.CanAsync(callerId, PermissionCatalogue.FinancialView, project, cancellationToken).ConfigureAwait(false))
        {
            return new FinancialSourceModePage([], page.Page, page.PageSize, 0);
        }

        IReadOnlyList<FinancialSourceMode> configured = await repository.ListSourceModesAsync(projectId, cancellationToken).ConfigureAwait(false);
        List<FinancialSourceModeDetail> all = [.. Enum.GetValues<FinancialField>().Select(field =>
            configured.SingleOrDefault(m => m.FieldCode == field) is { } mode
                ? FinancialKpiMapping.ToDetail(mode)
                : new FinancialSourceModeDetail(null, projectId, field, SourceMode.Manual, null))];
        return new FinancialSourceModePage([.. all.Skip(page.Skip).Take(page.PageSize)], page.Page, page.PageSize, all.Count);
    }

    public async Task<AdministrationResult<Versioned<FinancialSourceModeDetail>>> GetAsync(Guid callerId, Guid sourceModeId, CancellationToken cancellationToken)
    {
        FinancialSourceMode? mode = await repository.FindSourceModeAsync(sourceModeId, null, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = mode is null ? null : await projects.FindAsync(mode.ProjectId, cancellationToken).ConfigureAwait(false);
        return (project is null ? AdministrationError.NotFound
                : await access.CheckAsync(callerId, PermissionCatalogue.FinancialView, project, cancellationToken).ConfigureAwait(false)) is { } refused
            ? refused
            : Versioned(mode!);
    }

    public async Task<AdministrationResult<Versioned<FinancialSourceModeDetail>>> CreateAsync(Guid callerId, FinancialSourceModeDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ProjectFacts? project = await projects.FindAsync(draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if ((project is null ? AdministrationError.NotFound
                : await access.CheckAsync(callerId, PermissionCatalogue.FinancialSourceManage, project, cancellationToken).ConfigureAwait(false)) is { } refused)
        {
            return refused;
        }

        if (draft.FieldCode == FinancialField.OpenCommitment)
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.NotInUse, new FieldIssue("fieldCode", FieldIssue.NotAllowed));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        FinancialSourceMode mode = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project!.Id,
            FieldCode = draft.FieldCode,
            SourceMode = draft.SourceMode,
            ConfiguredAt = now,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(mode);
        audit.Stage(FinancialKpiAudit.SourceModeChanged(callerId, project, mode, null));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => Versioned(mode),
            FinancialKpiSaveOutcome.Duplicate => AdministrationError.Duplicate("fieldCode"),
            FinancialKpiSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<FinancialSourceModeDetail>>> UpdateAsync(
        Guid callerId, Guid sourceModeId, SourceMode sourceMode, uint expectedVersion, CancellationToken cancellationToken)
    {
        FinancialSourceMode? mode = await repository.FindSourceModeAsync(sourceModeId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = mode is null ? null : await projects.FindAsync(mode.ProjectId, cancellationToken).ConfigureAwait(false);
        if ((project is null ? AdministrationError.NotFound
                : await access.CheckAsync(callerId, PermissionCatalogue.FinancialSourceManage, project, cancellationToken).ConfigureAwait(false)) is { } refused)
        {
            return refused;
        }

        SourceMode before = mode!.SourceMode;
        if (before == SourceMode.Integrated && sourceMode != SourceMode.Integrated)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.SourceModeLocked);
        }

        if (before == sourceMode)
        {
            return Versioned(mode);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        mode.SourceMode = sourceMode;
        mode.ConfiguredAt = now;
        mode.UpdatedAt = now;
        mode.UpdatedBy = callerId;
        audit.Stage(FinancialKpiAudit.SourceModeChanged(callerId, project!, mode, before));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved
            ? Versioned(mode)
            : AdministrationError.PreconditionFailed;
    }

    /// <summary>Null when a manual figure may be entered for the field; otherwise the refusal (ADR-008: no manual substitution).</summary>
    internal static async Task<AdministrationError?> ManualEntryRefusedAsync(
        IFinancialKpiRepository repository, Guid projectId, IEnumerable<(FinancialField Field, string Property)> fields, CancellationToken cancellationToken)
    {
        IReadOnlyList<FinancialSourceMode> modes = await repository.ListSourceModesAsync(projectId, cancellationToken).ConfigureAwait(false);
        FieldIssue[] integrated = [.. fields
            .Where(f => modes.Any(m => m.FieldCode == f.Field && m.SourceMode == SourceMode.Integrated))
            .Select(f => new FieldIssue(f.Property, FieldIssue.NotAllowed))];
        return integrated.Length > 0 ? AdministrationError.Rule(FinancialKpiErrorCodes.FieldIntegrated, integrated) : null;
    }

    private Versioned<FinancialSourceModeDetail> Versioned(FinancialSourceMode mode) => new(FinancialKpiMapping.ToDetail(mode), repository.RowVersionOf(mode));
}
