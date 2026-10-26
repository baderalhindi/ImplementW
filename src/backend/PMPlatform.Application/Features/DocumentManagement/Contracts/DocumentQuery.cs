using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>
/// SCR-120 Library, SCR-121 Project Documents (<see cref="ProjectId"/>) and SCR-122 Recent Documents: the documents the
/// caller may read, most recently changed first. <see cref="Q"/> matches the title.
/// </summary>
public sealed record DocumentQuery(Guid? ProjectId, IReadOnlyCollection<DocumentStatus> Statuses, string? Q, PageRequest Page);
