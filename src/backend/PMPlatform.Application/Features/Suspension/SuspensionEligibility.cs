using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// When a project admits a request, and what a request needs (WF-09 §5.2). A suspension is of an ACTIVE project, a resumption of a
/// SUSPENDED one (BR-SUS-001, BR-SUS-002). The project's state is read again by every command that moves a request, so a request raised
/// while the project was eligible is refused once it is not (ELG-SUS-004, ELG-RES-004). That a project has at most one open request of
/// each type (BR-SUS-004, BR-SUS-005) and at most one open suspension (BR-SUS-003) is held by two unique keys, whose violation the save
/// answers with <see cref="DuplicateOf"/>: one rule, one place, for a concurrent request as for a sequential one.
/// </summary>
internal static class SuspensionEligibility
{
    /// <summary>
    /// Null when the project's state admits a request of <paramref name="type"/>. Suspending a project that is suspended already is a
    /// conflict with the suspension in force, not a wrong state.
    /// </summary>
    public static AdministrationError? ProjectRefused(SuspensionRequestType type, ProjectFacts project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return (type, project.Status) switch
        {
            (SuspensionRequestType.Suspend, ProjectLifecycleState.Active) or (SuspensionRequestType.Resume, ProjectLifecycleState.Suspended) => null,
            (SuspensionRequestType.Suspend, ProjectLifecycleState.Suspended) => AdministrationError.Conflict(SuspensionErrorCodes.AlreadyExists),
            _ => AdministrationError.Rule(SuspensionErrorCodes.ProjectNotEligible),
        };
    }

    /// <summary>The conflict a single-instance key answers with, for a request of <paramref name="type"/>.</summary>
    public static AdministrationError DuplicateOf(SuspensionRequestType type) =>
        AdministrationError.Conflict(type == SuspensionRequestType.Suspend ? SuspensionErrorCodes.AlreadyExists : SuspensionErrorCodes.ResumptionAlreadyExists);

    /// <summary>A planned resumption date belongs to a suspension, and falls after its effective date.</summary>
    public static AdministrationError? FieldsRefused(SuspensionRequestType type, SuspensionRequestChanges fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return fields.PlannedResumptionDate is not { } planned ? null
            : type == SuspensionRequestType.Resume ? AdministrationError.Rule(SuspensionErrorCodes.DateInvalid, new FieldIssue("plannedResumptionDate", FieldIssue.NotAllowed))
            : fields.RequestedEffectiveDate is { } effective && planned <= effective
                ? AdministrationError.Rule(SuspensionErrorCodes.DateInvalid, new FieldIssue("plannedResumptionDate", FieldIssue.BeforeStart))
            : null;
    }

    /// <summary>
    /// What review needs: an effective date, not before <paramref name="today"/> — a request takes effect from now on, never backdated
    /// (TBC-SUS-008 leaves the lead time to AHDA) — and fields <see cref="FieldsRefused"/> accepts.
    /// </summary>
    public static AdministrationError? SubmissionRefused(SuspensionRequest request, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.RequestedEffectiveDate is not { } effective
            ? AdministrationError.Rule(SuspensionErrorCodes.Incomplete, new FieldIssue("requestedEffectiveDate", FieldIssue.Required))
            : effective < today ? AdministrationError.Rule(SuspensionErrorCodes.DateInvalid, new FieldIssue("requestedEffectiveDate", FieldIssue.NotAllowed))
            : FieldsRefused(request.RequestType, new SuspensionRequestChanges(request.Reason, request.RequestedEffectiveDate, request.PlannedResumptionDate));
    }
}
