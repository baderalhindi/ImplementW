using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// SCR-025 Project Register: the projects the caller may see, most recently changed first (indexing-strategy I-01 to
/// I-05). <see cref="Q"/> matches the title or the Formal Project ID.
/// </summary>
public sealed record ProjectQuery(IReadOnlyCollection<ProjectLifecycleState> Statuses, Guid? DepartmentId, Guid? ExternalEntityId, string? Q, PageRequest Page);
