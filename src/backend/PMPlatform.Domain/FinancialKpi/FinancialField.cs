namespace PMPlatform.Domain.FinancialKpi;

/// <summary>ERD <c>financial_source_mode.field_code</c>: the financial fields whose source is set per project (ADR-008).</summary>
public enum FinancialField
{
    ApprovedBudget = 1,
    ActualExpenditure = 2,
    ForecastAtCompletion = 3,

    /// <summary>Not used at launch (ADR-008 gate).</summary>
    OpenCommitment = 4,
}
