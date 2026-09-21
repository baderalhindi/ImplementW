namespace PMPlatform.Domain.Common;

/// <summary>
/// The four provenance fields every financial record carries (ADR-008 extension, TASK-108; ERD D-10):
/// <c>source_type</c>, <c>source_reference</c>, <c>as_of_date</c>, <c>entered_by_user_id</c>.
/// <see cref="EnteredByUserId"/> is the person who entered the figure and is distinct from the audit column
/// <c>created_by</c>, which may be a service principal (ERD §7).
/// </summary>
public sealed record FinancialProvenance
{
    public FinancialSourceType SourceType { get; }

    public string? SourceReference { get; }

    public DateOnly AsOfDate { get; }

    public Guid EnteredByUserId { get; }

    public FinancialProvenance(FinancialSourceType sourceType, string? sourceReference, DateOnly asOfDate, Guid enteredByUserId)
    {
        if (sourceType != FinancialSourceType.Manual && string.IsNullOrWhiteSpace(sourceReference))
        {
            throw new ArgumentException("A non-manual source must carry a source reference.", nameof(sourceReference));
        }

        if (enteredByUserId == Guid.Empty)
        {
            throw new ArgumentException("The entering user is required.", nameof(enteredByUserId));
        }

        SourceType = sourceType;
        SourceReference = sourceReference;
        AsOfDate = asOfDate;
        EnteredByUserId = enteredByUserId;
    }
}
