namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>Error codes of project import batches (R3-4), and the codes of the issues a row is given.</summary>
public static class ProjectImportErrorCodes
{
    /// <summary>422: the file is not an .xlsx workbook with RPMO's data sheet and its row of field keys.</summary>
    public const string NotWorkbook = "PROJECT_IMPORT_NOT_WORKBOOK";

    /// <summary>422: more rows than one batch takes (1,500), or a file over 10 MB.</summary>
    public const string TooLarge = "PROJECT_IMPORT_TOO_LARGE";

    /// <summary>422: a row has an error, or a name is unmatched: the batch cannot be submitted or applied.</summary>
    public const string NotSubmittable = "PROJECT_IMPORT_NOT_SUBMITTABLE";

    /// <summary>403: the importer cannot decide their own batch.</summary>
    public const string OwnBatch = "PROJECT_IMPORT_OWN_BATCH";

    /// <summary>409: only a SUBMITTED batch is decided, only an APPROVED one rolled back.</summary>
    public const string InvalidState = "PROJECT_IMPORT_BATCH_STATE";

    /// <summary>409: a project of the batch changed since it was applied, or the batch updated existing projects: it cannot be rolled back.</summary>
    public const string RollbackRefused = "PROJECT_IMPORT_ROLLBACK_REFUSED";

    /// <summary>422: a value request names nothing, a name too long, or a status (a status is mapped, never added).</summary>
    public const string ValueRequestInvalid = "PROJECT_IMPORT_VALUE_REQUEST_INVALID";
}

/// <summary>The codes a row's issues carry. A column's shape uses the platform's validation codes as well (REQUIRED, MALFORMED, …).</summary>
public static class ProjectImportIssueCodes
{
    public const string NameUnmatched = "NAME_UNMATCHED";

    /// <summary>A warning: the follow-up owner names no active internal user; the field is left empty unless the name is mapped.</summary>
    public const string OwnerUnmatched = "OWNER_UNMATCHED";
    public const string InitiativeOutsidePortfolio = "INITIATIVE_OUTSIDE_PORTFOLIO";
    public const string EndBeforeStart = "END_BEFORE_START";
    public const string NotPositive = "NOT_POSITIVE";
    public const string PercentOutOfRange = "PERCENT_OUT_OF_RANGE";
    public const string DuplicateInFile = "DUPLICATE_IN_FILE";
    public const string ManagerNotFound = "MANAGER_NOT_FOUND";
    public const string ManagerNotR04 = "MANAGER_NOT_R04";
    public const string OutsideScope = "OUTSIDE_SCOPE";
    public const string StatusToChoose = "STATUS_TO_CHOOSE";
    public const string ProjectClosed = "PROJECT_CLOSED";
    public const string SameNameAndEntity = "SAME_NAME_AND_ENTITY";
    public const string ActivePastEnd = "ACTIVE_PAST_END";
    public const string CompletedBelowFull = "COMPLETED_BELOW_100";
    public const string CompletedFutureEnd = "COMPLETED_FUTURE_END";
    public const string FutureWithProgress = "FUTURE_WITH_PROGRESS";
    public const string ExceptionalValue = "EXCEPTIONAL_VALUE";
    public const string GovernedFieldNotUpdated = "GOVERNED_FIELD_NOT_UPDATED";
    public const string KeptBlank = "KEPT_BLANK";
    public const string PercentScaled = "PERCENT_SCALED";
    public const string NoChange = "NO_CHANGE";
    public const string SkippedByImporter = "SKIPPED_BY_IMPORTER";
    public const string ColumnIgnored = "COLUMN_IGNORED";
    public const string ColumnMissing = "COLUMN_MISSING";
    public const string TemplateOutdated = "TEMPLATE_OUTDATED";
}
