namespace PMPlatform.Domain.Closure;

/// <summary>
/// ERD <c>readiness_check.check_code</c> (TASK-063): the criteria a completion or closure case is evaluated against (WF-10 §6.1, §6.2).
/// Each source module answers for its own records (P4); WF-10 records the answer and never changes the records.
/// </summary>
public enum ReadinessCheckCode
{
    /// <summary>No WF-11 run of the project is pending and no record of it is under review: no decision can land after the project moves.</summary>
    DecisionsSettled = 1,

    /// <summary>WF-04: every task is COMPLETED or CANCELLED.</summary>
    TasksDispositioned = 2,

    /// <summary>WF-03: no baseline candidate is being prepared.</summary>
    ScheduleReconciled = 3,

    /// <summary>WF-03 and WF-05: every milestone is ACHIEVED or CANCELLED, and no achievement claim is being prepared.</summary>
    MilestonesDispositioned = 4,

    /// <summary>WF-06: every risk is CLOSED or carries an ACTIVE acceptance (accepted residual risk).</summary>
    RisksDispositioned = 5,

    /// <summary>WF-07: every issue and challenge is RESOLVED or CLOSED.</summary>
    IssuesDispositioned = 6,

    /// <summary>WF-08: no change request is undecided or approved and not carried through, as the stage requires.</summary>
    ChangesDispositioned = 7,

    /// <summary>WF-09: no suspension or resumption request is open.</summary>
    SuspensionRequestsSettled = 8,

    /// <summary>WF-02: the project has published progress and no progress submission is unpublished.</summary>
    ProgressReported = 9,

    /// <summary>WF-14: no financial update, KPI measurement, budget version or KPI target version is unpublished or undecided.</summary>
    FinancialsSettled = 10,

    /// <summary>WF-10: every open post-project obligation has an owner and a due date (BR-CLO-023).</summary>
    ObligationsOwned = 11,

    /// <summary>WF-10: every post-project obligation is SATISFIED, WAIVED or CANCELLED — the closure policy.</summary>
    ObligationsSatisfied = 12,
}
