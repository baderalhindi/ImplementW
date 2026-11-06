using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ExternalParticipation;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;

/// <summary>
/// The monthly update's scheduled work. Reminders (the 5 Nov 2026 review §2.1): 3 days before the due date, on the day, then daily; each request one a day; the first day it is late, its
/// reviewer is told too. Repeating batches (§2.2): on their day, the next batch of a series goes out to the same projects, each to its entity contact as it stands then, due the same number
/// of days after sending; a project that can no longer be sent to is left out of that month's.
/// </summary>
internal sealed class MonthlyUpdateMaintenance(
    IExternalParticipationRepository repository,
    IProjectFactsReader projects,
    IProjectMonthlyUpdates monthly,
    UpdateRequestBatchService batches,
    IOutbox outbox,
    TimeProvider timeProvider) : IMonthlyUpdateMaintenance
{
    public const int DaysBeforeDue = 3;

    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        int done = 0;
        foreach (ExternalUpdateRequest request in await repository.ListRemindableMonthlyUpdatesAsync(today, today.AddDays(DaysBeforeDue), batchSize, cancellationToken).ConfigureAwait(false))
        {
            done += await RemindAsync(request, today, now, cancellationToken).ConfigureAwait(false);
        }

        foreach (UpdateRequestBatch batch in await repository.ListRepeatingBatchesDueAsync(today, batchSize, cancellationToken).ConfigureAwait(false))
        {
            done += await SendNextAsync(batch, today, now, cancellationToken).ConfigureAwait(false);
        }

        return done;
    }

    private async Task<int> RemindAsync(ExternalUpdateRequest request, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        DateOnly due = request.DueDate!.Value;
        if (today < due && due != today.AddDays(DaysBeforeDue))
        {
            return 0;
        }

        ProjectFacts? project = await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null || project.Status == ProjectLifecycleState.Closed)
        {
            return 0;
        }

        Guid actor = request.IssuedByUserId!.Value;
        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        string stage = today < due ? "DUE_SOON" : today == due ? "DUE_TODAY" : "OVERDUE";
        outbox.Stage(MonthlyUpdateReminded.Of(request, project.DepartmentId, request.ResponsibleUserId!.Value, stage, actor, now));
        if (due.AddDays(1) == today)
        {
            outbox.Stage(MonthlyUpdateReminded.Of(request, project.DepartmentId, request.ReviewerUserId!.Value, "LATE_TO_REVIEWER", actor, now));
        }

        request.LastRemindedOn = today;
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> SendNextAsync(UpdateRequestBatch previous, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExternalUpdateRequest> sent = await repository.ListRequestsOfBatchAsync(previous.Id, cancellationToken).ConfigureAwait(false);
        int dueIn = previous.DueDate.DayNumber - DateOnly.FromDateTime(previous.CreatedAt.UtcDateTime).DayNumber;
        List<UpdateRequestBatchItem> items = [];
        foreach (ExternalUpdateRequest request in sent.Where(r => r.Status != ExternalUpdateRequestStatus.Cancelled))
        {
            ProjectMonthlySnapshot? project = await monthly.ReadAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
            if (project is { Status: ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active or ProjectLifecycleState.Suspended })
            {
                items.Add(new UpdateRequestBatchItem(project.Id, project.EntityContactUserId ?? request.ResponsibleUserId));
            }
        }

        UpdateRequestBatch next = (await repository.FindBatchForUpdateAsync(previous.Id, cancellationToken).ConfigureAwait(false))!;
        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        next.NextRunOn = null;
        ExternalParticipationGate.Touch(next, next.CreatedBy, now);
        if (items.Count > 0)
        {
            AdministrationResult<BuiltBatch> built = await batches.BuildAsync(
                next.CreatedBy, new UpdateRequestBatchDraft(items, today.AddDays(dueIn), true, next.RepeatDay, next.Instructions, next.ReviewerUserId), next.SeriesId, now,
                enforceSendGrant: false, cancellationToken).ConfigureAwait(false);
            if (built.Succeeded)
            {
                repository.Add(built.Value.Batch);
                foreach (ExternalUpdateRequest request in built.Value.Requests)
                {
                    await batches.StageSendAsync(request, next.CreatedBy, now, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }
}
