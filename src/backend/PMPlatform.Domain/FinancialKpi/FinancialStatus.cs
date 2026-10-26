namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// ERD <c>published_financial_snapshot.financial_status</c>. UNKNOWN is a value of its own: a position without the figures to
/// rate it is UNKNOWN, never coerced to a colour.
/// </summary>
public enum FinancialStatus
{
    Green = 1,
    Amber = 2,
    Red = 3,
    Unknown = 4,
}
