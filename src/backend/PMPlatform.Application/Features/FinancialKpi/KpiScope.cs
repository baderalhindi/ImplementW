using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// Where a target version or a measurement is authorized: on its assignment's project (M-7). An assignment the caller cannot see
/// is one that does not exist (R-47).
/// </summary>
internal sealed class KpiScope(IFinancialKpiRepository repository, IProjectFactsReader projects, FinancialKpiAccess access)
{
    public async Task<Scoped> LoadAsync(Guid callerId, string permissionCode, Guid assignmentId, CancellationToken cancellationToken)
    {
        KpiAssignment? assignment = await repository.FindAssignmentAsync(assignmentId, null, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = assignment is null ? null : await projects.FindAsync(assignment.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Scoped(null, null, AdministrationError.NotFound)
            : new Scoped(project, assignment, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Whether the caller may see the assignment's records, for a collection: nothing is recorded (R-3).</summary>
    public async Task<bool> CanAsync(Guid callerId, string permissionCode, Guid assignmentId, CancellationToken cancellationToken) =>
        await repository.FindAssignmentAsync(assignmentId, null, cancellationToken).ConfigureAwait(false) is { } assignment
        && await projects.FindAsync(assignment.ProjectId, cancellationToken).ConfigureAwait(false) is { } project
        && await access.CanAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false);

    internal sealed record Scoped(ProjectFacts? Project, KpiAssignment? Assignment, AdministrationError? Error);
}
