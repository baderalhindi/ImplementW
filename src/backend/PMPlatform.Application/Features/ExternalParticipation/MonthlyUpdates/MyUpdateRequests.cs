using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;

/// <summary>
/// The window at sign-in and the pinned item in the bell (the 5 Nov 2026 review §2.1) read this: the monthly updates waiting on the signed-in person, whoever they are, soonest first. Only
/// requests naming them as responder, so any user may ask. A draft pre-fills itself; otherwise the form starts from last month's values on the project.
/// </summary>
internal sealed class MyUpdateRequests(
    IExternalParticipationRepository repository,
    IProjectMonthlyUpdates projects,
    TimeProvider timeProvider) : IMyUpdateRequests
{
    public async Task<MyUpdateRequestPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        IReadOnlyList<ExternalUpdateRequest> open = await repository.ListOpenMonthlyUpdatesAsync(callerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, ExternalContribution> latest = await repository.FindLatestContributionsAsync([.. open.Select(r => r.Id)], cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, IReadOnlyList<ExternalContributionField>> fields = (await repository.ListFieldsAsync([.. latest.Values.Select(c => c.Id)], cancellationToken).ConfigureAwait(false))
            .GroupBy(f => f.ExternalContributionId).ToDictionary(g => g.Key, g => (IReadOnlyList<ExternalContributionField>)[.. g]);

        List<MyUpdateRequest> items = [];
        foreach (ExternalUpdateRequest request in open.Skip(page.Skip).Take(page.PageSize))
        {
            ProjectMonthlySnapshot? project = await projects.ReadAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
            if (project is null)
            {
                continue;
            }

            ExternalContribution? contribution = latest.GetValueOrDefault(request.Id);
            (Dictionary<string, string> values, Dictionary<string, Language> languages) = contribution is not null && fields.TryGetValue(contribution.Id, out IReadOnlyList<ExternalContributionField>? saved)
                ? (saved.ToDictionary(f => f.FieldCode, f => f.ProposedValue, StringComparer.Ordinal),
                   saved.Where(f => f.ProposedValueLanguage is not null).ToDictionary(f => f.FieldCode, f => f.ProposedValueLanguage!.Value, StringComparer.Ordinal))
                : LastMonth(project);
            ExternalContribution? returned = contribution is { RevisionNo: > 1 } ? await repository.FindContributionByRevisionAsync(request.Id, contribution.RevisionNo - 1, cancellationToken).ConfigureAwait(false) : null;
            items.Add(new MyUpdateRequest(
                request.Id, repository.RowVersionOf(request), request.ProjectId, project.FormalProjectId, project.LegacyReference, project.Title, request.DueDate,
                request.DueDate < today, request.Status, request.Instructions, contribution?.Id, contribution is null ? null : repository.RowVersionOf(contribution), contribution?.Status,
                returned?.ReviewReason, returned is not null, project.DeclaredPlannedPercent, values, languages)
            {
                Stages = project.Stages,
            });
        }

        return new MyUpdateRequestPage(items, page.Page, page.PageSize, open.Count, open.Count(r => r.DueDate < today), open.Min(r => r.DueDate));
    }

    public async Task<AdministrationError?> MarkOpenedAsync(Guid callerId, Guid requestId, CancellationToken cancellationToken)
    {
        ExternalUpdateRequest? request = await repository.FindRequestAsync(requestId, null, cancellationToken).ConfigureAwait(false);
        if (request is null || request.ResponsibleUserId != callerId)
        {
            return AdministrationError.NotFound;
        }

        if (request.OpenedAt is null)
        {
            await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
            request.OpenedAt = timeProvider.GetUtcNow();
            await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static (Dictionary<string, string>, Dictionary<string, Language>) LastMonth(ProjectMonthlySnapshot project)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        Dictionary<string, Language> languages = new(StringComparer.Ordinal);
        void Number(string field, decimal? value)
        {
            if (value is { } number)
            {
                values[field] = number.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        void Narrative(string field, NarrativeText? value)
        {
            if (value is not null)
            {
                values[field] = value.Text;
                languages[field] = value.Language;
            }
        }

        if (project.Stages.Count > 0 && project.CurrentStageCode is { } stage && project.Stages.Any(s => s.StageCode == stage))
        {
            values[ContributionSchemas.CurrentStageCode] = stage;
            Number(ContributionSchemas.CurrentStagePercent, project.CurrentStagePercent);
        }
        else
        {
            Number(ContributionSchemas.ActualProgressPercent, project.DeclaredActualPercent);
        }

        Number(ContributionSchemas.ActualSpendPercent, project.DeclaredActualSpendPercent);
        Narrative(ContributionSchemas.CurrentObstacle, project.CurrentObstacle);
        Narrative(ContributionSchemas.NextAction, project.NextAction);
        if (project.NextActionDueDate is { } due)
        {
            values[ContributionSchemas.NextActionDueDate] = due.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return (values, languages);
    }
}
