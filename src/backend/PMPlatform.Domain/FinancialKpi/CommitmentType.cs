namespace PMPlatform.Domain.FinancialKpi;

/// <summary>ERD <c>financial_commitment.commitment_type</c>.</summary>
public enum CommitmentType
{
    /// <summary>The budget of record, approved through WF-11.</summary>
    ApprovedBudget = 1,

    /// <summary>ADR-014: the budget a legacy intake declared, written by TASK-104's consumer.</summary>
    DeclaredBudget = 2,

    /// <summary>Not used at launch (ADR-008 gate).</summary>
    OpenCommitment = 3,
}
