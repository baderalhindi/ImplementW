namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// ERD <c>financial_progress_update.status</c>: a period's actuals and forecast are reviewed and published by AHDA. RETURNED and
/// PUBLISHED are final; a returned revision is followed by revision + 1.
/// </summary>
public enum FinancialUpdateStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Published = 5,
}
