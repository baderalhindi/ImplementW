namespace PMPlatform.Domain.Project;

/// <summary>Whether the project is funded from the strategy budget (RPMO register column "هل التمويل من ميزانية الاستراتيجية").</summary>
public enum FundingSource
{
    StrategyBudgetFull = 1,
    StrategyBudgetPartial = 2,
    NotStrategyBudget = 3,
}
