namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// The <c>errors[]</c> codes of a refused configuration content, beyond the shared ones of <c>FieldIssue</c>. Each names
/// the entry by its path, e.g. <c>riskMatrixCells[3].ratingCode</c>, and never echoes a value (R-25).
/// </summary>
public static class ContentIssueCodes
{
    /// <summary>The family does not carry this section.</summary>
    public const string SectionNotAllowed = "SECTION_NOT_ALLOWED";

    /// <summary>A master data item of another catalogue than the field requires.</summary>
    public const string WrongCatalogue = "WRONG_CATALOGUE";

    /// <summary>A referenced item or KPI definition that is not PUBLISHED.</summary>
    public const string NotPublished = "NOT_PUBLISHED";

    /// <summary>A number outside the range the field allows.</summary>
    public const string OutOfRange = "OUT_OF_RANGE";

    /// <summary>A value that cannot be read as its declared type.</summary>
    public const string Malformed = "MALFORMED";

    /// <summary>Something the family requires is absent.</summary>
    public const string Incomplete = "INCOMPLETE";
}
