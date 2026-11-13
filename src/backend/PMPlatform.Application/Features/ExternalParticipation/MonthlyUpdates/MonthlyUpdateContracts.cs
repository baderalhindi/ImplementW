using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;

/// <summary>One project of a batch. A responder given here, for a project with no entity contact, becomes its contact.</summary>
public sealed record UpdateRequestBatchItem(Guid ProjectId, Guid? ResponsibleUserId);

/// <summary>The "طلب تحديث" flow's third step (the 5 Nov 2026 review §2.2).</summary>
public sealed record UpdateRequestBatchDraft(
    IReadOnlyList<UpdateRequestBatchItem> Items, DateOnly DueDate, bool RepeatMonthly, int? RepeatDay, NarrativeText Instructions, Guid ReviewerUserId);

/// <summary>Where a project of a batch stands, as the batch page shows it.</summary>
public enum BatchRowState
{
    Sent = 1,
    Opened = 2,
    Draft = 3,
    Submitted = 4,
    Accepted = 5,
    Returned = 6,
    Rejected = 7,
    Withdrawn = 8,
}

public sealed record UpdateRequestBatchRow(
    Guid RequestId, Guid ProjectId, string? FormalProjectId, string? LegacyReference, NarrativeText ProjectTitle, Guid ExternalEntityId, Guid? ResponsibleUserId,
    BatchRowState State, bool Overdue, uint RequestVersion);

public sealed record UpdateRequestBatchSummary(
    Guid Id, Guid SeriesId, DateOnly DueDate, bool RepeatMonthly, int? RepeatDay, DateOnly? NextRunOn, DateTimeOffset? WithdrawnAt, Guid ReviewerUserId, Guid CreatedBy, DateTimeOffset CreatedAt,
    int ProjectCount, int EntityCount, int OpenCount);

public sealed record UpdateRequestBatchDetail(UpdateRequestBatchSummary Batch, NarrativeText Instructions, IReadOnlyList<UpdateRequestBatchRow> Rows);

public sealed record UpdateRequestBatchPage(IReadOnlyList<UpdateRequestBatchSummary> Items, int Page, int PageSize, int TotalCount);

/// <summary>A request waiting on its responder, with what the window pre-fills: the open draft's values, or last month's from the project.</summary>
public sealed record MyUpdateRequest(
    Guid RequestId, uint RequestVersion, Guid ProjectId, string? FormalProjectId, string? LegacyReference, NarrativeText ProjectTitle, DateOnly? DueDate, bool Overdue, ExternalUpdateRequestStatus Status,
    NarrativeText Instructions, Guid? ContributionId, uint? ContributionVersion, ExternalContributionStatus? ContributionStatus, NarrativeText? ReturnReason, bool PreviouslyReturned,
    decimal? PlannedProgressPercent, IReadOnlyDictionary<string, string> Prefill, IReadOnlyDictionary<string, Language> PrefillLanguages)
{
    /// <summary>
    /// Review round 7, item 1: the stages of the project's plan that apply, each with its planned % today. When there are any, the answer gives the current
    /// stage and its % complete instead of the overall figure.
    /// </summary>
    public IReadOnlyList<ProjectMonthlyStage> Stages { get; init; } = [];
}

/// <summary>What the bell and the window need: the due requests, soonest first, with how many are late.</summary>
public sealed record MyUpdateRequestPage(IReadOnlyList<MyUpdateRequest> Items, int Page, int PageSize, int TotalCount, int OverdueCount, DateOnly? EarliestDueDate);

public sealed record UpdateRequestResponder(Guid UserId, string DisplayName);

public sealed record UpdateRequestResponderPage(IReadOnlyList<UpdateRequestResponder> Items, int Page, int PageSize, int TotalCount);

public interface IUpdateRequestBatchService
{
    public Task<AdministrationResult<UpdateRequestBatchDetail>> SendAsync(Guid callerId, UpdateRequestBatchDraft draft, CancellationToken cancellationToken);

    public Task<UpdateRequestBatchPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<UpdateRequestBatchDetail>> GetAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken);

    /// <summary>"Remind the late ones": a reminder today to each responder whose request waits past its due date.</summary>
    public Task<AdministrationResult<UpdateRequestBatchDetail>> RemindLateAsync(Guid callerId, Guid batchId, CancellationToken cancellationToken);

    /// <summary>Cancels what is still open and stops the series. Answers already submitted stay with their reviewer.</summary>
    public Task<AdministrationResult<UpdateRequestBatchDetail>> WithdrawAsync(Guid callerId, Guid batchId, NarrativeText reason, CancellationToken cancellationToken);
}

public interface IMyUpdateRequests
{
    public Task<MyUpdateRequestPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Records that the responder has opened the request; the first time only.</summary>
    public Task<AdministrationError?> MarkOpenedAsync(Guid callerId, Guid requestId, CancellationToken cancellationToken);
}

/// <summary>One pass of the monthly update's scheduled work: the reminders due today and the repeating batches due out.</summary>
public interface IMonthlyUpdateMaintenance
{
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}
