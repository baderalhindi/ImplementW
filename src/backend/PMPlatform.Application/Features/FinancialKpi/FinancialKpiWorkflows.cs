using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// A version approved through WF-11 — a commitment, a KPI target (TASK-052). Born DRAFT; submitted, and resubmitted after a
/// return as the next revision; WF-11's outcome activates, returns, rejects or records the withdrawal; activating one supersedes
/// the ACTIVE one in the same transaction. Migration <c>TASK-052_GuardFinancialKpiHistory</c> refuses every other change.
/// </summary>
internal static class ApprovedVersionWorkflow
{
    public static IReadOnlySet<(ApprovedVersionStatus From, ApprovedVersionStatus To)> Transitions { get; } = new HashSet<(ApprovedVersionStatus, ApprovedVersionStatus)>
    {
        (ApprovedVersionStatus.Draft, ApprovedVersionStatus.Submitted),      // submit to WF-11
        (ApprovedVersionStatus.Returned, ApprovedVersionStatus.Submitted),   // resubmit as revision + 1
        (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Active),     // WF-11 APPROVED
        (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Returned),   // WF-11 RETURNED
        (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Rejected),   // WF-11 REJECTED
        (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Withdrawn),  // WF-11 WITHDRAWN by its requester
        (ApprovedVersionStatus.Active, ApprovedVersionStatus.Superseded),    // the next version activated
    };

    public static bool Allows(ApprovedVersionStatus from, ApprovedVersionStatus to) => Transitions.Contains((from, to));

    /// <summary>A version still on its way: while one exists, its project or assignment gets no other.</summary>
    public static bool IsOpen(ApprovedVersionStatus status) =>
        status is ApprovedVersionStatus.Draft or ApprovedVersionStatus.Submitted or ApprovedVersionStatus.UnderReview or ApprovedVersionStatus.Returned;

    /// <summary>Its figures may change: before submission, and after a return.</summary>
    public static bool IsEditable(ApprovedVersionStatus status) => status is ApprovedVersionStatus.Draft or ApprovedVersionStatus.Returned;

    /// <summary>Where WF-11's decision leaves a SUBMITTED version.</summary>
    public static ApprovedVersionStatus OutcomeOf(ApprovalOutcomeDecision decision) => decision switch
    {
        ApprovalOutcomeDecision.Approved => ApprovedVersionStatus.Active,
        ApprovalOutcomeDecision.Returned => ApprovedVersionStatus.Returned,
        ApprovalOutcomeDecision.Rejected => ApprovedVersionStatus.Rejected,
        ApprovalOutcomeDecision.Withdrawn => ApprovedVersionStatus.Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };
}

/// <summary>A period's financial update (TASK-052): AHDA's review and publication, as WF-02's progress (progress-update.md D-6).</summary>
internal static class FinancialUpdateWorkflow
{
    public static IReadOnlySet<(FinancialUpdateStatus From, FinancialUpdateStatus To)> Transitions { get; } = new HashSet<(FinancialUpdateStatus, FinancialUpdateStatus)>
    {
        (FinancialUpdateStatus.Draft, FinancialUpdateStatus.Submitted),         // submit
        (FinancialUpdateStatus.Submitted, FinancialUpdateStatus.UnderReview),   // start-review
        (FinancialUpdateStatus.UnderReview, FinancialUpdateStatus.Returned),    // return, opening revision + 1
        (FinancialUpdateStatus.UnderReview, FinancialUpdateStatus.Published),   // publish, writing the snapshot
    };

    public static bool Allows(FinancialUpdateStatus from, FinancialUpdateStatus to) => Transitions.Contains((from, to));
}

/// <summary>A KPI measurement (TASK-052): DRAFT → SUBMITTED → PUBLISHED, the ERD's value set.</summary>
internal static class KpiMeasurementWorkflow
{
    public static IReadOnlySet<(KpiMeasurementStatus From, KpiMeasurementStatus To)> Transitions { get; } = new HashSet<(KpiMeasurementStatus, KpiMeasurementStatus)>
    {
        (KpiMeasurementStatus.Draft, KpiMeasurementStatus.Submitted),
        (KpiMeasurementStatus.Submitted, KpiMeasurementStatus.Published),
    };

    public static bool Allows(KpiMeasurementStatus from, KpiMeasurementStatus to) => Transitions.Contains((from, to));
}

/// <summary>A KPI assignment: ACTIVE and SUSPENDED move both ways; RETIRED is final.</summary>
internal static class KpiAssignmentWorkflow
{
    public static IReadOnlySet<(KpiAssignmentStatus From, KpiAssignmentStatus To)> Transitions { get; } = new HashSet<(KpiAssignmentStatus, KpiAssignmentStatus)>
    {
        (KpiAssignmentStatus.Active, KpiAssignmentStatus.Suspended),
        (KpiAssignmentStatus.Suspended, KpiAssignmentStatus.Active),
        (KpiAssignmentStatus.Active, KpiAssignmentStatus.Retired),
        (KpiAssignmentStatus.Suspended, KpiAssignmentStatus.Retired),
    };

    public static bool Allows(KpiAssignmentStatus from, KpiAssignmentStatus to) => Transitions.Contains((from, to));
}
