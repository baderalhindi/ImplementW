using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// SCR-025 Project Register: the projects the caller may see, most recently changed first (indexing-strategy I-01 to
/// I-05). <see cref="Q"/> matches the title or the Formal Project ID. <see cref="ProjectManagerUserId"/> serves SCR-026 My
/// Projects (I-04); like every filter it narrows the caller's scope and never widens it.
/// </summary>
public sealed record ProjectQuery(
    IReadOnlyCollection<ProjectLifecycleState> Statuses, Guid? DepartmentId, Guid? ExternalEntityId, Guid? ProjectManagerUserId, string? Q, PageRequest Page);
