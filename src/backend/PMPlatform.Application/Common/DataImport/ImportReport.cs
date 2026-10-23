namespace PMPlatform.Application.Common.DataImport;

/// <summary>
/// What a validation or an import found. Nothing is committed unless every row passed (all or nothing), so <see cref="Committed"/> is
/// true only with no <see cref="Issues"/>.
/// </summary>
public sealed record ImportReport(
    string RegisterCode,
    bool Committed,
    Guid? BatchId,
    int RowCount,
    int CreatedCount,
    int SkippedCount,
    IReadOnlyList<ImportIssue> Issues,
    IReadOnlyList<ImportRowOutcome> Rows);

/// <summary>One reason the workbook cannot be loaded: its worksheet row (1 for the header), its column, and the code.</summary>
public sealed record ImportIssue(int Row, string Column, string Code);

/// <summary>Where one row ended up. <see cref="EntityId"/> is the record it became, once committed.</summary>
public sealed record ImportRowOutcome(int Row, string LegacyId, ImportRowStatus Status, Guid? EntityId);

public enum ImportRowStatus
{
    /// <summary>Validated: it would be created.</summary>
    Ready = 1,

    Created = 2,

    /// <summary>Its legacy ID was imported by an earlier batch; nothing is written.</summary>
    Skipped = 3,

    Rejected = 4,
}

/// <summary>The import's own issue codes; a cell's shape uses the validation codes, and a register its module's.</summary>
public static class ImportIssueCodes
{
    /// <summary>A header the register requires is not in the first row.</summary>
    public const string ColumnMissing = "COLUMN_MISSING";

    /// <summary>A header the register does not have: a typo would otherwise drop a column's values silently.</summary>
    public const string ColumnUnknown = "COLUMN_UNKNOWN";

    public const string ColumnDuplicate = "COLUMN_DUPLICATE";

    /// <summary>The workbook has a header and no row.</summary>
    public const string NoRows = "NO_ROWS";

    /// <summary>A second row of the workbook has the same legacy ID.</summary>
    public const string DuplicateInFile = "DUPLICATE_IN_FILE";

    /// <summary>A code, username or legacy ID that names nothing the reference needs.</summary>
    public const string ReferenceNotFound = "REFERENCE_NOT_FOUND";

    /// <summary>The record already exists on the platform, created other than by this import.</summary>
    public const string AlreadyExists = "ALREADY_EXISTS";
}
