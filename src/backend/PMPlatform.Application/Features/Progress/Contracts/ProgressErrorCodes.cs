namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>The Progress module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ProgressErrorCodes
{
    /// <summary>422: progress is reported on an ACTIVE project only.</summary>
    public const string ProjectNotActive = "PROGRESS_PROJECT_NOT_ACTIVE";

    /// <summary>
    /// 422: there is no work breakdown with planned durations to derive actual progress from (ADR-009). Progress is never
    /// typed in at project level instead.
    /// </summary>
    public const string RollupUnavailable = "PROGRESS_ROLLUP_UNAVAILABLE";

    /// <summary>409: every period that has begun is published, or is being reported in a submission already open.</summary>
    public const string NothingToReport = "PROGRESS_NOTHING_TO_REPORT";

    /// <summary>409: the period already has a revision in progress.</summary>
    public const string SubmissionExists = "PROGRESS_SUBMISSION_EXISTS";

    /// <summary>409: only a DRAFT is edited.</summary>
    public const string NotEditable = "PROGRESS_NOT_EDITABLE";

    /// <summary>422: project stages are configured and the project has no stage plan: the plan is set first (review round 7, item 1).</summary>
    public const string StagePlanRequired = "PROGRESS_STAGE_PLAN_REQUIRED";

    /// <summary>
    /// 422: a stage's entry breaks a rule: a stage the period does not have or that does not apply, a % the schedule measures, a % lower than the last
    /// published period's without a reason, a started stage without its actual start, a complete one without its finish, a note longer than a line.
    /// </summary>
    public const string StageEntryInvalid = "PROGRESS_STAGE_ENTRY_INVALID";
}
