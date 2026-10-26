using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// Newest request first. <see cref="RequestedByCaller"/> is SCR-101 My Requests; the subject filters are how a source
/// module's screen finds the runs of its record (M-4). Only runs the caller may see are listed. An empty
/// <see cref="Statuses"/> is no filter.
/// </summary>
public sealed record ApprovalInstanceQuery(
    bool RequestedByCaller,
    string? SubjectModule,
    string? SubjectType,
    Guid? SubjectId,
    IReadOnlyCollection<ApprovalInstanceStatus> Statuses,
    PageRequest Page);
