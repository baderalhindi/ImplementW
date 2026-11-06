using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.GlobalSearch.Contracts;

/// <summary>The kinds of record the header search finds.</summary>
public enum GlobalSearchRecordType
{
    Project,
    ProjectTask,
    Risk,
    ManagementConcern,
    ChangeRequest,
    Document,
}

/// <summary>
/// One record the caller may open. <see cref="ProjectId"/> is its project (null for a library document), <see cref="ProjectTitle"/> says which
/// project it belongs to where the row can name it, and <see cref="Reference"/> is a project's formal id.
/// </summary>
public sealed record GlobalSearchHit(GlobalSearchRecordType Type, Guid Id, Guid? ProjectId, NarrativeText Title, NarrativeText? ProjectTitle, string? Reference);

/// <summary>The most recently changed matches of each kind, kind by kind.</summary>
public sealed record GlobalSearchResult(IReadOnlyList<GlobalSearchHit> Items);
