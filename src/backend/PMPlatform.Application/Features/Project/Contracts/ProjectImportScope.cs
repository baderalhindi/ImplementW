using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// What the registers inside a project share (data-import.md D-12): the column naming the project, the check that the person reaches it under
/// the register's permission, and the Formal Project IDs the template offers. A project out of reach reads as one that does not exist (R-47).
/// </summary>
public sealed class ProjectImportScope(IProjectFactsReader projects, IAuthorizationEngine engine, CatalogueCodeBook catalogues, IUserLookup users)
{
    public const string ProjectColumn = "project_formal_id";

    public const string LanguageColumn = "text_language";

    public static ImportColumn Column { get; } = new(
        ProjectColumn, ImportValueKind.Text, Required: true,
        "The Formal Project ID of a project you may add these records to.",
        "المعرّف الرسمي لمشروع يحق لك إضافة هذه السجلات إليه.")
    {
        MaxLength = 50,
    };

    /// <summary>The language a row's narrative cells are written in.</summary>
    public static ImportColumn Language { get; } = new(
        LanguageColumn, ImportValueKind.Choice, Required: true,
        "The language the row's text is written in: ar or en.", "لغة نص الصف: ar أو en.")
    {
        Choices = ["ar", "en"],
    };

    /// <summary>The cell as narrative text in the row's language; null when the cell is empty.</summary>
    public static NarrativeText? Narrative(ImportRecord record, string column)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Text(column) is { } text ? new NarrativeText(text, LanguageCode.Parse(record.Text(LanguageColumn)!)) : null;
    }

    /// <summary>The published catalogue item the cell names; an unknown code is an issue on its column. Null also when the cell is empty.</summary>
    public async Task<Guid?> ItemAsync(string catalogueCode, ImportRecord record, string column, List<ImportCellIssue> issues, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(issues);
        if (record.Text(column) is not { } code)
        {
            return null;
        }

        MasterDataItemReference? item = await catalogues.FindAsync(catalogueCode, code, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            issues.Add(new ImportCellIssue(column, ImportIssueCodes.ReferenceNotFound));
        }

        return item?.Id;
    }

    /// <summary>The person the cell names by username; an unknown one is an issue on its column. Null also when the cell is empty.</summary>
    public async Task<Guid?> UserAsync(ImportRecord record, string column, List<ImportCellIssue> issues, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(issues);
        if (record.Text(column) is not { } username)
        {
            return null;
        }

        Guid? id = await users.FindIdByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (id is null)
        {
            issues.Add(new ImportCellIssue(column, ImportIssueCodes.ReferenceNotFound));
        }

        return id;
    }

    /// <summary>The template's lists for a catalogue column, beside the projects.</summary>
    public Task<IReadOnlyList<ImportReferenceValue>> CatalogueValuesAsync(string catalogueCode, CancellationToken cancellationToken) =>
        catalogues.ReferenceValuesAsync(catalogueCode, cancellationToken);

    public async Task<ImportRowScope> PlaceAsync(ImportRecord record, Guid actorId, string permissionCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        return await projects.FindByFormalProjectIdAsync(record.Text(ProjectColumn)!, cancellationToken).ConfigureAwait(false) is { } project
               && (await engine.EvaluateAsync(actorId, new AuthorizationRequest(permissionCode, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).IsAllowed
            ? ImportRowScope.OfProject(project.Id)
            : ImportRowScope.Rejected(ProjectColumn, ImportIssueCodes.ReferenceNotFound);
    }

    /// <summary>The projects the person reaches under the permission and could still add records to, by Formal Project ID.</summary>
    public async Task<IReadOnlyList<ImportReferenceValue>> ReachableAsync(Guid actorId, string permissionCode, CancellationToken cancellationToken)
    {
        RecordScope scope = await engine.GetRecordScopeAsync(actorId, permissionCode, cancellationToken).ConfigureAwait(false);
        return
        [
            .. (await projects.ListReachedAsync(scope, cancellationToken).ConfigureAwait(false))
                .Where(p => p.FormalProjectId is not null && p.Status != ProjectLifecycleState.Closed)
                .OrderBy(p => p.FormalProjectId, StringComparer.Ordinal)
                .Select(p => new ImportReferenceValue(p.FormalProjectId!, p.Title.Text, p.Title.Text)),
        ];
    }

    private static AuthorizationSubject SubjectOf(ProjectFacts project) => new()
    {
        ProjectId = project.Id,
        DepartmentId = project.DepartmentId,
        ExternalEntityId = project.ExternalEntityId,
        OwnerUserId = project.ProjectManagerUserId,
    };
}
