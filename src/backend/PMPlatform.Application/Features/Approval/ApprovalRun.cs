using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval;

/// <summary>A run with its tasks in stage order.</summary>
public sealed record ApprovalRun(ApprovalInstance Instance, IReadOnlyList<ApprovalTask> Tasks);
