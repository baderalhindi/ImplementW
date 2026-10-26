namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// ERD <c>financial_commitment.status</c> and <c>kpi_target_version.status</c>: a version approved through WF-11. It is born
/// DRAFT, SUBMITTED to WF-11, and ends ACTIVE, RETURNED (resubmitted as the next revision), REJECTED or WITHDRAWN; an ACTIVE
/// version becomes SUPERSEDED when the next one activates. UNDER_REVIEW is in the ERD's value set but no edge reaches it: WF-11
/// gives the source no signal between start and outcome (financial-kpi.md F-9).
/// </summary>
public enum ApprovedVersionStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    Active = 5,
    Superseded = 6,
    Rejected = 7,
    Withdrawn = 8,
}
