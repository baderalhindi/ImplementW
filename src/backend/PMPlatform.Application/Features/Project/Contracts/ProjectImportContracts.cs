using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>What a name in an import workbook is of, so it is matched against the right list (R3-4).</summary>
public enum ProjectImportDimension
{
    Entity = 1,
    Department = 2,
    Sector = 3,
    Portfolio = 4,
    Initiative = 5,
    Region = 6,
    City = 7,
    Priority = 8,
    Governance = 9,
    Status = 10,
    ProjectManager = 11,
    FollowUpOwner = 12,
}

/// <summary>An error blocks its row; a warning is allowed but flagged; info is something the platform settled itself.</summary>
public enum ProjectImportIssueLevel
{
    Error = 1,
    Warning = 2,
    Info = 3,
}

/// <summary>What an approved batch does with a row: a project that matches nothing is created, one that matches is updated.</summary>
public enum ProjectImportRowAction
{
    Create = 1,
    Update = 2,
    Skip = 3,
}

/// <summary>A name the importer maps to a platform value: an id, or a lifecycle state for <see cref="ProjectImportDimension.Status"/>.</summary>
public sealed record ProjectImportMapping(ProjectImportDimension Dimension, string SourceText, string TargetValue);

/// <summary>The importer's decisions: name mappings, and the rows, by legacy reference, to skip.</summary>
public sealed record ProjectImportPlan(IReadOnlyList<ProjectImportMapping> Mappings, IReadOnlyList<string> SkippedRows)
{
    public static ProjectImportPlan Empty { get; } = new([], []);
}

public sealed record ProjectImportIssue(string Column, string Code, ProjectImportIssueLevel Level);

/// <summary>A field an update changes, as text: the value before and after.</summary>
public sealed record ProjectImportChange(string Field, string? Before, string? After);

public sealed record ProjectImportRowPreview(
    int Row,
    string? LegacyReference,
    string? Title,
    ProjectImportRowAction Action,
    Guid? ProjectId,
    IReadOnlyList<ProjectImportIssue> Issues,
    IReadOnlyList<ProjectImportChange> Changes);

/// <summary>A name that matched nothing, listed once with the rows that carry it, and what it may be mapped to.</summary>
public sealed record ProjectImportUnmatchedName(ProjectImportDimension Dimension, string SourceText, int RowCount, IReadOnlyList<ProjectImportOption> Options);

/// <summary>A platform value a name may be mapped to.</summary>
public sealed record ProjectImportOption(string Value, BilingualLabel Label);

/// <summary>
/// What a workbook would do (R3-4): every row's action, issues and changes, the names to map, and the counts. <see cref="Submittable"/> is
/// true when no row has an error and no required name is unmatched (an unmatched follow-up owner is a warning).
/// </summary>
public sealed record ProjectImportPreview(
    string FileSha256,
    int RowCount,
    int CreateCount,
    int UpdateCount,
    int SkipCount,
    int ErrorCount,
    int WarningCount,
    bool Submittable,
    IReadOnlyList<ProjectImportIssue> FileIssues,
    IReadOnlyList<ProjectImportUnmatchedName> Unmatched,
    IReadOnlyList<ProjectImportRowPreview> Rows);

public sealed record ProjectImportBatchDetail(
    Guid Id,
    int BatchNo,
    ProjectImportBatchStatus Status,
    string FileName,
    string FileSha256,
    NarrativeText? SubmissionNote,
    int RowCount,
    int CreateCount,
    int UpdateCount,
    int SkipCount,
    int WarningCount,
    Guid SubmittedBy,
    DateTimeOffset SubmittedAt,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAt,
    NarrativeText? ReturnReason,
    Guid? RolledBackByUserId,
    DateTimeOffset? RolledBackAt,
    bool MayDecide,
    bool MayRollBack);

public sealed record ProjectImportBatchPage(IReadOnlyList<ProjectImportBatchDetail> Items, int Page, int PageSize, int TotalCount);

public sealed record ProjectImportBatchQuery(ProjectImportBatchStatus? Status, int Page, int PageSize);

/// <summary>The workbook a batch was submitted from, as uploaded.</summary>
public sealed record ProjectImportFile(string FileName, byte[] Content);

/// <summary>A workbook as uploaded.</summary>
public sealed record ProjectImportUpload(string FileName, byte[] Content);

/// <summary>
/// What a validation request answers (review §4, step 3): a workbook of up to 200 rows is validated at once, <see cref="Preview"/>; a larger one
/// is queued for the background, <see cref="Queued"/>, and its importer notified when it is done.
/// </summary>
public sealed record ProjectImportValidationOutcome(ProjectImportPreview? Preview, ProjectImportValidationDetail? Queued);

/// <summary>A background validation: its state, and once COMPLETED, the preview; a FAILED one names why.</summary>
public sealed record ProjectImportValidationDetail(
    Guid Id,
    ProjectImportValidationStatus Status,
    string FileName,
    int RowCount,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    ProjectImportPreview? Preview);

/// <summary>An importer's request to add a value the lists lack, and how it was resolved.</summary>
public sealed record ProjectImportValueRequestDetail(
    Guid Id,
    ProjectImportDimension Dimension,
    string SourceText,
    ProjectImportValueRequestStatus Status,
    Guid RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? ResolvedByUserId,
    DateTimeOffset? ResolvedAt,
    NarrativeText? ResolutionNote);

public sealed record ProjectImportValueRequestPage(IReadOnlyList<ProjectImportValueRequestDetail> Items, int Page, int PageSize, int TotalCount);
