using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// The project a run is scoped to, named for an approver's inbox row (stage QA D1-01): its title as entered (ADR-012) and its Formal Project
/// ID once it has one.
/// </summary>
public sealed record ApprovalProjectName(Guid Id, string? FormalProjectId, NarrativeText Title);
