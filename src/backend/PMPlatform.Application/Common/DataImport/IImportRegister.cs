using PMPlatform.Application.Common.Authorization;

namespace PMPlatform.Application.Common.DataImport;

/// <summary>
/// A register a migration workbook can load (TASK-102). Each lives in the module that owns the rows it writes and writes them through
/// that module's own rules (ADR-003 M-1), so an imported row meets every invariant a row entered on screen does. The engine owns the
/// workbook, the shape of each cell, the legacy-ID lineage and the one transaction; the register owns what a row means.
/// </summary>
public interface IImportRegister
{
    /// <summary>The register's code in the API path and the lineage, e.g. <c>DEPARTMENT</c>.</summary>
    public string Code { get; }

    /// <summary>The module that owns the rows, as audit names it (event-conventions EV-1).</summary>
    public string OwningModule { get; }

    /// <summary>
    /// What admits a person to the register: <c>DATA_IMPORT</c> for a platform register; a project register's own create permission, which
    /// the module's service then decides row by row on the row's project (data-import.md D-12).
    /// </summary>
    public string Permission => PermissionCatalogue.DataImport;

    /// <summary>The columns after <see cref="ImportColumn.LegacyId"/>, in template order.</summary>
    public IReadOnlyList<ImportColumn> Columns { get; }

    /// <summary>
    /// The values a reference column accepts today, by column key, for the template's Reference values sheet: the codes a user copies
    /// rather than guesses. Empty when no column refers to a list. <paramref name="actorId"/> is who asked: a project register lists only
    /// the projects they reach.
    /// </summary>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(Guid actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Where the row belongs, decided before anything is looked up under its legacy ID. A project register refuses a project the person does
    /// not reach here, so a row can neither learn of nor collide with another project's record.
    /// </summary>
    public Task<ImportRowScope> ScopeAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ImportRowScope.Platform);

    /// <summary>
    /// Writes one row and saves it, inside the import's transaction; the engine rolls the row back when it is rejected. A reason the row
    /// cannot be written is an issue, not an exception: an exception aborts the whole import.
    /// </summary>
    public Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken);
}

/// <summary>A value a reference column accepts: the code the cell holds and its name in both languages.</summary>
public sealed record ImportReferenceValue(string Code, string NameAr, string NameEn);

/// <summary>Where a row's legacy ID is unique: the whole platform, or one project, whose people number their records independently.</summary>
public sealed record ImportRowScope
{
    private ImportRowScope(Guid? projectId, IReadOnlyList<ImportCellIssue> issues)
    {
        ProjectId = projectId;
        Issues = issues;
    }

    public static ImportRowScope Platform { get; } = new(null, []);

    public Guid? ProjectId { get; }

    /// <summary>Why the row has no scope: the project it names is not one the person reaches. Nothing else is done for such a row.</summary>
    public IReadOnlyList<ImportCellIssue> Issues { get; }

    public static ImportRowScope OfProject(Guid projectId) => new(projectId, []);

    public static ImportRowScope Rejected(string column, string code) => new(null, [new ImportCellIssue(column, code)]);

    /// <summary>The key the lineage holds for <paramref name="legacyId"/> in this scope.</summary>
    public string LineageKey(string legacyId) => ImportLineage.Key(ProjectId, legacyId);
}

/// <summary>How the lineage keys a legacy ID (data-import.md D-13).</summary>
public static class ImportLineage
{
    /// <summary>A project's 32 hexadecimal digits and a slash before the legacy ID.</summary>
    public const int KeyLength = ImportColumn.LegacyIdLength + 33;

    public static string Key(Guid? projectId, string legacyId) => projectId is { } id ? $"{id:N}/{legacyId}" : legacyId;
}

/// <summary>What a register knows about the import it is writing a row for.</summary>
public sealed class ImportContext(Guid actorId, Func<string, string, CancellationToken, Task<Guid?>> findImported)
{
    /// <summary>The person importing: every row is created by them and audited as theirs.</summary>
    public Guid ActorId { get; } = actorId;

    /// <summary>
    /// The platform record a legacy ID of <paramref name="registerCode"/> became, in an earlier batch or an earlier row of this one;
    /// null when none has that ID.
    /// </summary>
    public Task<Guid?> FindImportedAsync(string registerCode, string legacyId, CancellationToken cancellationToken) =>
        findImported(registerCode, legacyId, cancellationToken);
}

/// <summary>The outcome of writing one row: the record it became, or why it was refused, by column.</summary>
public sealed record ImportRowResult
{
    private ImportRowResult(Guid? entityId, IReadOnlyList<ImportCellIssue> issues)
    {
        EntityId = entityId;
        Issues = issues;
    }

    public Guid? EntityId { get; }

    public IReadOnlyList<ImportCellIssue> Issues { get; }

    public static ImportRowResult Created(Guid entityId) => new(entityId, []);

    public static ImportRowResult Rejected(string column, string code) => new(null, [new ImportCellIssue(column, code)]);

    public static ImportRowResult Rejected(IReadOnlyList<ImportCellIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return issues.Count == 0 ? throw new ArgumentException("A rejected row names at least one issue.", nameof(issues)) : new(null, issues);
    }
}

/// <summary>A reason a row cannot be written: the column at fault (empty for the row as a whole) and the code.</summary>
public sealed record ImportCellIssue(string Column, string Code);
