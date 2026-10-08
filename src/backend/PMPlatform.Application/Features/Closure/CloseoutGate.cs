using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// The way into a project's closeout records every WF-10 operation shares: the project's facts (Project's query contract), the
/// authorization check, and the save that commits. A record the caller may not see is one that does not exist (R-47).
/// </summary>
internal sealed class CloseoutGate(ICloseoutRepository repository, IProjectFactsReader projects, CloseoutAccess access)
{
    /// <summary>The project, if the caller holds <paramref name="permissionCode"/> on it.</summary>
    public async Task<(ProjectFacts? Project, AdministrationError? Error)> ReachProjectAsync(
        Guid callerId, string permissionCode, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? (null, AdministrationError.NotFound)
            : (project, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The project, if the caller may see its closeout records; for a collection, so nothing is recorded (R-3).</summary>
    public async Task<ProjectFacts?> ViewableProjectAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false)
            ? project
            : null;

    /// <summary>
    /// The case, tracked, and its project, if the caller holds <paramref name="permissionCode"/> on it; with <paramref name="internalOnly"/>,
    /// AHDA's side, so an external user is refused whatever they hold and the refusal is audited (ADR-013).
    /// </summary>
    public async Task<Loaded<TCase>> LoadCaseAsync<TCase>(
        Guid callerId, string permissionCode, Guid caseId, uint? expectedVersion, bool internalOnly, CancellationToken cancellationToken)
        where TCase : CloseoutCase
    {
        TCase? @case = await repository.FindCaseAsync<TCase>(caseId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = @case is null ? null : await projects.FindAsync(@case.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded<TCase>(null, null, AdministrationError.NotFound)
            : new Loaded<TCase>(project, @case, await CheckAsync(callerId, permissionCode, project, CloseoutAudit.SubjectOf(@case!), internalOnly, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The obligation, tracked, and its project, as <see cref="LoadCaseAsync{TCase}"/>.</summary>
    public async Task<Loaded<PostProjectObligation>> LoadObligationAsync(
        Guid callerId, string permissionCode, Guid obligationId, uint? expectedVersion, bool internalOnly, CancellationToken cancellationToken)
    {
        PostProjectObligation? obligation = await repository.FindObligationAsync(obligationId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = obligation is null ? null : await projects.FindAsync(obligation.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded<PostProjectObligation>(null, null, AdministrationError.NotFound)
            : new Loaded<PostProjectObligation>(
                project, obligation, await CheckAsync(callerId, permissionCode, project, CloseoutAudit.SubjectOf(obligation!), internalOnly, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Saves and, when saved, commits; otherwise nothing was written.</summary>
    public async Task<CloseoutSaveOutcome> SaveAsync(ICloseoutWork work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        CloseoutSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved == CloseoutSaveOutcome.Saved)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    /// <summary>The refusal a save that did not save stands for: a stale version (412), the open case another took first, or a referenced draft (409).</summary>
    public static AdministrationError RefusalOf(CloseoutSaveOutcome outcome) => outcome switch
    {
        CloseoutSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
        CloseoutSaveOutcome.Duplicate => AdministrationError.Conflict(ClosureErrorCodes.CaseAlreadyOpen),
        CloseoutSaveOutcome.InUse => AdministrationError.Conflict(ClosureErrorCodes.CaseInUse),
        CloseoutSaveOutcome.Saved or _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a refusal."),
    };

    /// <summary>Stamps a changed row with who changed it and when.</summary>
    public static void Touch(AuditedEntity row, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.UpdatedAt = now;
        row.UpdatedBy = actorId;
    }

    private Task<AdministrationError?> CheckAsync(
        Guid callerId, string permissionCode, ProjectFacts project, AuditSubject subject, bool internalOnly, CancellationToken cancellationToken) =>
        internalOnly
            ? access.CheckInternalAsync(callerId, permissionCode, project, reason => CloseoutAudit.AuthorityRefused(callerId, project, subject, permissionCode, reason), cancellationToken)
            : access.CheckAsync(callerId, permissionCode, project, cancellationToken);
}

internal sealed record Loaded<T>(ProjectFacts? Project, T? Record, AdministrationError? Error)
    where T : class;
