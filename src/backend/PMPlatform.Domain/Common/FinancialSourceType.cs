namespace PMPlatform.Domain.Common;

/// <summary>Where a financial figure came from (ADR-008 extension, TASK-108; ERD D-10 <c>source_type</c>).</summary>
public enum FinancialSourceType
{
    Manual = 1,
    Etimad = 2,
    Other = 3,
}
