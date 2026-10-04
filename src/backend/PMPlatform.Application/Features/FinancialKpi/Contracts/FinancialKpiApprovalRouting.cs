namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// How FinancialKpi's versions name themselves to WF-11 (M-8; ADR-003 §8.2 edge 26) and to DocumentManagement (edge 38): the
/// subject module and types, and the APPROVAL_AUTHORITY <c>subject_type_code</c>s AHDA configures the routes under (ERD
/// <c>approval_authority_rule.subject_type_code</c>: COMMITMENT, KPI_TARGET).
/// </summary>
public static class FinancialKpiApprovalRouting
{
    public const string SubjectModule = "FinancialKpi";

    public const string CommitmentType = "FinancialCommitment";

    public const string TargetVersionType = "KpiTargetVersion";

    public const string CommitmentRoutingKey = "COMMITMENT";

    public const string TargetRoutingKey = "KPI_TARGET";
}
