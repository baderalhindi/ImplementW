using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The representations of WF-13's rows, and the external least-disclosure projection (WF-13 §8.2, EXT-CC-05). An external caller's view is
/// built from the internal one by withholding every field AHDA keeps to itself — the internal-only lists below — so a field added to a
/// representation is visible externally only once it is classified (the unit test pins both lists against the records' properties).
/// </summary>
internal sealed class ExternalParticipationViews(IExternalParticipationRepository repository, IProjectFactsReader projects, ContributionTargets targets, TimeProvider timeProvider)
{
    /// <summary>A request's fields only AHDA sees: who reviews it, who issued it, who wrote the row, and the configuration version that enabled it.</summary>
    public static IReadOnlyList<string> RequestInternalOnlyFields { get; } =
        ["reviewerUserId", "participationConfigurationVersionId", "issuedByUserId", "createdBy", "updatedBy"];

    /// <summary>A revision's fields only AHDA sees: the source version and state it was checked against, its reviewer, the internal note.</summary>
    public static IReadOnlyList<string> ContributionInternalOnlyFields { get; } =
        ["targetVersion", "targetState", "reviewedByUserId", "reviewStartedAt", "reviewInternalNote", "createdBy", "updatedBy"];

    public async Task<ExternalUpdateRequestDetail> RequestAsync(ExternalUpdateRequest request, ParticipationAudience audience, CancellationToken cancellationToken) =>
        (await RequestsAsync([request], audience, cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<ExternalUpdateRequestDetail>> RequestsAsync(
        IReadOnlyList<ExternalUpdateRequest> requests, ParticipationAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        Dictionary<Guid, string?> formalIds = [];
        foreach (Guid projectId in requests.Select(r => r.ProjectId).Distinct())
        {
            formalIds[projectId] = (await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false))?.FormalProjectId;
        }

        List<ExternalUpdateRequestDetail> details = [];
        foreach (ExternalUpdateRequest r in requests)
        {
            ContributionSchema schema = ContributionSchemas.Of(r.ContributionSchemaCode);
            NarrativeText? label = r.TargetId is { } targetId && targets.Of(schema) is { } target
                ? (await target.FindAsync(targetId, cancellationToken).ConfigureAwait(false))?.Label
                : null;
            ExternalUpdateRequestDetail detail = new(
                r.Id, r.ProjectId, formalIds[r.ProjectId], r.ExternalEntityId, r.Origin, r.ContributionTypeItemId, r.ContributionSchemaCode, schema.Mode,
                r.TargetModule, r.TargetType, r.TargetId, label, schema.Fields, r.Instructions, r.ResponsibleUserId, r.ReviewerUserId, r.DueDate,
                DueConditionOf(r.Status, r.DueDate, today), r.Status, r.ParticipationConfigurationVersionId, r.IssuedByUserId, r.IssuedAt, r.CancelledAt,
                r.CancellationReason, r.ClosedAt, r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy, ParticipationAudience.Internal, []);
            details.Add(audience == ParticipationAudience.Internal ? detail : Project(detail));
        }

        return details;
    }

    public async Task<ExternalContributionDetail> ContributionAsync(ExternalContribution contribution, ParticipationAudience audience, CancellationToken cancellationToken) =>
        (await ContributionsAsync([contribution], audience, cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<ExternalContributionDetail>> ContributionsAsync(
        IReadOnlyList<ExternalContribution> contributions, ParticipationAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        ILookup<Guid, ExternalContributionField> fields =
            (await repository.ListFieldsAsync([.. contributions.Select(c => c.Id)], cancellationToken).ConfigureAwait(false)).ToLookup(f => f.ExternalContributionId);
        return
        [
            .. contributions.Select(c =>
            {
                ExternalContributionDetail detail = new(
                    c.Id, c.ExternalUpdateRequestId, c.ProjectId, c.ExternalEntityId, c.RevisionNo, c.PreviousRevisionId, c.Status, c.ContributorUserId,
                    [.. fields[c.Id].OrderBy(f => f.FieldCode, StringComparer.Ordinal).Select(f => new ContributionFieldValue(f.FieldCode, f.ProposedValue, f.ProposedValueLanguage))],
                    c.SubmittedAt, c.TargetVersion, c.TargetState, c.ReviewedByUserId, c.ReviewStartedAt, c.ReviewedAt, c.ReviewReason, c.ReviewInternalNote,
                    c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, ParticipationAudience.Internal, []);
                return audience == ParticipationAudience.Internal ? detail : Project(detail);
            }),
        ];
    }

    public static SourceApplicationDetail ApplicationOf(SourceApplication a, ExternalUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(request);
        return new SourceApplicationDetail(
            a.Id, a.ExternalContributionId, request.Id, a.AttemptNo, a.Status, ContributionSchemas.Of(request.ContributionSchemaCode).Mode, request.TargetModule,
            request.TargetType, request.TargetId, a.ExpectedTargetRevisionNo, a.ActualTargetRevisionNo, a.FailureCode, a.AttemptedByUserId, a.AttemptedAt,
            a.CompletedAt, a.RevalidatedAt, a.RevalidatedByUserId, a.RevalidatedTargetRevisionNo, a.CorrelationId);
    }

    /// <summary>EXT-F-020: due on its date, overdue after it, while the request waits on the entity; not applicable otherwise.</summary>
    public static ResponseDueCondition DueConditionOf(ExternalUpdateRequestStatus status, DateOnly? dueDate, DateOnly today) =>
        dueDate is not { } due || !ExternalParticipationWorkflow.AwaitsEntity(status) ? ResponseDueCondition.NotApplicable
        : today > due ? ResponseDueCondition.Overdue
        : today == due ? ResponseDueCondition.Due
        : ResponseDueCondition.NotDue;

    /// <summary>The external projection of a request: every internal-only field withheld and named (R-20).</summary>
    public static ExternalUpdateRequestDetail Project(ExternalUpdateRequestDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return detail with
        {
            ReviewerUserId = null,
            ParticipationConfigurationVersionId = null,
            IssuedByUserId = null,
            CreatedBy = null,
            UpdatedBy = null,
            Projection = ParticipationAudience.External,
            MaskedFields = RequestInternalOnlyFields,
        };
    }

    /// <summary>The external projection of a revision: every internal-only field withheld and named (R-20).</summary>
    public static ExternalContributionDetail Project(ExternalContributionDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return detail with
        {
            TargetVersion = null,
            TargetState = null,
            ReviewedByUserId = null,
            ReviewStartedAt = null,
            ReviewInternalNote = null,
            CreatedBy = null,
            UpdatedBy = null,
            Projection = ParticipationAudience.External,
            MaskedFields = ContributionInternalOnlyFields,
        };
    }
}
