using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval;

/// <summary>Starts approval runs for source modules (ADR-003 §8.2 edges 20–27) and answers which runs a subject has.</summary>
internal sealed class ApprovalRequestService(
    IApprovalRepository repository,
    IConfigurationResolver resolver,
    ApprovalPolicy policy,
    IAuthorizationEngine engine,
    IEnumerable<IApprovalOutcomeHandler> outcomeHandlers,
    IApprovalRunReader runReader,
    IAuditTrail audit,
    TimeProvider timeProvider) : IApprovalRequests
{
    public async Task<AdministrationResult<ApprovalInstanceDetail>> StartAsync(ApprovalStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        ApprovalSubject subject = start.Subject;

        // Checked here rather than at dispatch: a run whose outcome no module can apply must not start.
        if (!outcomeHandlers.Any(h => string.Equals(h.SubjectModule, subject.Module, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"No approval outcome handler is registered for module {subject.Module}.");
        }

        if (await engine.GetPrincipalAsync(start.RequestedByUserId, cancellationToken).ConfigureAwait(false) is not { IsActive: true })
        {
            return AdministrationError.Rule(ApprovalErrorCodes.RequesterInvalid, new FieldIssue("requestedByUserId", FieldIssue.Inactive));
        }

        IReadOnlyList<ApprovalRun> runs = await repository.FindBySubjectAsync(subject.Module, subject.Type, subject.Id, cancellationToken).ConfigureAwait(false);
        if (runs.SingleOrDefault(r => r.Instance.SubjectRevisionNo == subject.RevisionNo) is { Instance.Status: ApprovalInstanceStatus.Pending } retried
            && string.Equals(retried.Instance.RoutingKey, start.RoutingKey, StringComparison.Ordinal))
        {
            return ApprovalMapping.ToDetail(retried.Instance, retried.Tasks);
        }

        if (runs.Any(r => r.Instance.Status == ApprovalInstanceStatus.Pending))
        {
            return AdministrationError.Conflict(ApprovalErrorCodes.AlreadyPending, new FieldIssue("subject", FieldIssue.Duplicate));
        }

        ApprovalInstance? previous = runs.Count == 0 ? null : runs[^1].Instance;
        if (previous is not null && subject.RevisionNo <= previous.SubjectRevisionNo)
        {
            return AdministrationError.Rule(ApprovalErrorCodes.RevisionStale, new FieldIssue("subject.revisionNo", FieldIssue.NotAllowed));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ResolvedConfiguration authority = await resolver.ResolveAsync(ConfigurationFamilyCodes.ApprovalAuthority, now, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RoutedSeat> seats = ApprovalRouting.Route(authority, start);
        DateTimeOffset dueAt = await policy.DueAtAsync(now, cancellationToken).ConfigureAwait(false);

        Guid instanceId = Guid.CreateVersion7(now);
        ApprovalInstance instance = new()
        {
            Id = instanceId,
            SubjectModule = subject.Module,
            SubjectType = subject.Type,
            SubjectId = subject.Id,
            SubjectRevisionNo = subject.RevisionNo,
            RoutingKey = start.RoutingKey,
            AuthorityConfigurationVersionId = authority.VersionId,
            ScopeProjectId = start.ScopeProjectId,
            ScopeDepartmentId = start.ScopeDepartmentId,
            RequestedByUserId = start.RequestedByUserId,
            RequestedAt = now,
            Status = ApprovalInstanceStatus.Pending,
            OutcomeIdempotencyKey = ApprovalOutcomes.OutcomeKeyOf(instanceId),
            PreviousInstanceId = previous?.Id,
            CreatedAt = now,
            CreatedBy = start.RequestedByUserId,
            UpdatedAt = now,
            UpdatedBy = start.RequestedByUserId,
        };

        short firstStage = seats[0].SequenceNo;
        List<ApprovalTask> tasks = [.. seats.Select(seat => ApprovalRows.NewTask(
            instanceId, seat.SequenceNo, seat.ApproverRoleId, seat.SequenceNo == firstStage ? dueAt : null, start.RequestedByUserId, now))];

        repository.Add(instance);
        tasks.ForEach(repository.Add);
        audit.Stage(ApprovalAudit.Requested(instance));
        return ApprovalMapping.ToDetail(instance, tasks);
    }

    public Task<IReadOnlyList<ApprovalInstanceDetail>> FindBySubjectAsync(string subjectModule, string subjectType, Guid subjectId, CancellationToken cancellationToken) =>
        runReader.FindBySubjectAsync(subjectModule, subjectType, subjectId, cancellationToken);
}
