namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>The Schedule module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ScheduleErrorCodes
{
    /// <summary>422: a schedule is planned while its project is APPROVED_PLANNED or ACTIVE only (the spec's SCH-CC-03).</summary>
    public const string ProjectNotEligible = "SCHEDULE_PROJECT_NOT_ELIGIBLE";

    /// <summary>422: the project's schedule has not been initialized.</summary>
    public const string NotInitialized = "SCHEDULE_NOT_INITIALIZED";

    /// <summary>409: the project already has its schedule.</summary>
    public const string Exists = "SCHEDULE_EXISTS";

    /// <summary>409: a baseline candidate of the project is with WF-11, so what it was submitted from is frozen; or the activity is cancelled.</summary>
    public const string NotEditable = "SCHEDULE_NOT_EDITABLE";

    /// <summary>409: another activity of the schedule has the WBS code.</summary>
    public const string WbsCodeExists = "SCHEDULE_WBS_CODE_EXISTS";

    /// <summary>422: the parent is not a live activity of the schedule, or is a dependency endpoint and so cannot become a summary.</summary>
    public const string HierarchyInvalid = "SCHEDULE_HIERARCHY_INVALID";

    /// <summary>422: the move would make an activity its own ancestor (VAL-SCH-003).</summary>
    public const string HierarchyCircular = "SCHEDULE_HIERARCHY_CIRCULAR";

    /// <summary>409: the activity cannot be cancelled while it has live children or dependencies.</summary>
    public const string ActivityInUse = "SCHEDULE_ACTIVITY_IN_USE";

    /// <summary>422: a forecast is entered on a live leaf activity only; a summary's is rolled up.</summary>
    public const string ForecastInvalid = "SCHEDULE_FORECAST_INVALID";

    /// <summary>422: the dependency's ends are not two distinct live leaf activities of one schedule (VAL-SCH-007).</summary>
    public const string DependencyInvalid = "SCHEDULE_DEPENDENCY_INVALID";

    /// <summary>422: the dependency would close a cycle in the dependency network (VAL-SCH-008).</summary>
    public const string DependencyCircular = "SCHEDULE_DEPENDENCY_CIRCULAR";

    /// <summary>409: the two activities are already linked in that direction.</summary>
    public const string DependencyExists = "SCHEDULE_DEPENDENCY_EXISTS";

    /// <summary>
    /// 422: the project has no ACTIVE baseline — to forecast against, or, for Project's activation command, to give planned
    /// progress (ADR-009); a Declared Baseline counts for a legacy-intake project (ADR-014).
    /// </summary>
    public const string ActiveBaselineRequired = "SCHEDULE_ACTIVE_BASELINE_REQUIRED";

    /// <summary>409: the project already has a baseline candidate on its way.</summary>
    public const string BaselineCandidateExists = "SCHEDULE_BASELINE_CANDIDATE_EXISTS";

    /// <summary>422: the schedule has no live leaf activity to baseline (the spec's BASELINE_VALIDATION_FAILED).</summary>
    public const string BaselineEmpty = "SCHEDULE_BASELINE_EMPTY";

    /// <summary>409: only a DRAFT candidate is deleted.</summary>
    public const string BaselineNotEditable = "SCHEDULE_BASELINE_NOT_EDITABLE";

    /// <summary>422: a rebaseline needs an applicable approved WF-08 change authorisation (BR-SCH-034).</summary>
    public const string ChangeAuthorizationRequired = "SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED";

    /// <summary>409: the database refused a second ACTIVE baseline of the project (api-conventions R-27's example).</summary>
    public const string SingleActiveBaseline = "SCHEDULE_SINGLE_ACTIVE_BASELINE";
}
