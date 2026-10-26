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

    /// <summary>The columns after <see cref="ImportColumn.LegacyId"/>, in template order.</summary>
    public IReadOnlyList<ImportColumn> Columns { get; }

    /// <summary>
    /// The values a reference column accepts today, by column key, for the template's Reference values sheet: the codes a user copies
    /// rather than guesses. Empty when no column refers to a list.
    /// </summary>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes one row and saves it, inside the import's transaction; the engine rolls the row back when it is rejected. A reason the row
    /// cannot be written is an issue, not an exception: an exception aborts the whole import.
    /// </summary>
    public Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken);
}

/// <summary>A value a reference column accepts: the code the cell holds and its name in both languages.</summary>
public sealed record ImportReferenceValue(string Code, string NameAr, string NameEn);

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
