using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// The way into a project's risks every WF-06 operation shares: the project's facts (edge 3), the authorization check, and the
/// save that commits and answers with the risk as saved. A risk the caller may not see is one that does not exist (R-47).
/// </summary>
internal sealed class RiskGate(IRiskRepository repository, IProjectFactsReader projects, RiskAccess access, RiskViews views)
{
    /// <summary>The project, if the caller may see its risks.</summary>
    public async Task<ProjectFacts?> ViewableProjectAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false)
            ? project
            : null;

    /// <summary>The project, if the caller holds <paramref name="permissionCode"/> on it.</summary>
    public async Task<ReachedProject> ReachProjectAsync(Guid callerId, string permissionCode, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectFacts? project = await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project is null ? new ReachedProject(null, AdministrationError.NotFound)
            : new ReachedProject(project, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The risk, tracked, and its project, if the caller holds <paramref name="permissionCode"/> on the project.</summary>
    public async Task<LoadedRisk> LoadRiskAsync(Guid callerId, string permissionCode, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        (RiskEntity? risk, ProjectFacts? project) = await FindAsync(riskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new LoadedRisk(null, null, AdministrationError.NotFound)
            : new LoadedRisk(project, risk, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// As <see cref="LoadRiskAsync"/>, for a rating or acceptance: AHDA's authority, so an external user is refused whatever they
    /// hold, and the refusal is audited (ADR-013).
    /// </summary>
    public async Task<LoadedRisk> LoadWithAuthorityAsync(Guid callerId, string permissionCode, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        (RiskEntity? risk, ProjectFacts? project) = await FindAsync(riskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new LoadedRisk(null, null, AdministrationError.NotFound)
            : new LoadedRisk(project, risk, await access.CheckAuthorityAsync(
                callerId, permissionCode, project, reason => RiskAudit.AuthorityRefused(callerId, project, riskId, permissionCode, reason), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The risk and its project, if the caller may see the project's risks; for a collection, so nothing is recorded (R-3).</summary>
    public async Task<(RiskEntity Risk, ProjectFacts Project)?> ViewableRiskAsync(Guid callerId, Guid riskId, CancellationToken cancellationToken)
    {
        (RiskEntity? risk, ProjectFacts? project) = await FindAsync(riskId, null, cancellationToken).ConfigureAwait(false);
        return project is not null && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false) ? (risk!, project) : null;
    }

    /// <summary>Saves, commits what was saved and answers with the risk as now saved. A row changed meanwhile is a stale version (412).</summary>
    public async Task<AdministrationResult<Versioned<RiskDetail>>> SaveRiskAsync(IRiskWork work, RiskEntity risk, CancellationToken cancellationToken) =>
        await SaveAsync(work, cancellationToken).ConfigureAwait(false)
            ? new Versioned<RiskDetail>(await views.DetailAsync(risk, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(risk))
            : AdministrationError.PreconditionFailed;

    /// <summary>Saves and commits; false when a row changed or a unique key was taken meanwhile, and nothing was written.</summary>
    public async Task<bool> SaveAsync(IRiskWork work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != RiskSaveOutcome.Saved)
        {
            return false;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Stamps a changed row with who changed it and when.</summary>
    public static void Touch(Domain.Common.AuditedEntity row, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.UpdatedAt = now;
        row.UpdatedBy = actorId;
    }

    private async Task<(RiskEntity? Risk, ProjectFacts? Project)> FindAsync(Guid riskId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        RiskEntity? risk = await repository.FindRiskAsync(riskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return (risk, risk is null ? null : await projects.FindAsync(risk.ProjectId, cancellationToken).ConfigureAwait(false));
    }
}

internal sealed record ReachedProject(ProjectFacts? Project, AdministrationError? Error);

internal sealed record LoadedRisk(ProjectFacts? Project, RiskEntity? Risk, AdministrationError? Error);
