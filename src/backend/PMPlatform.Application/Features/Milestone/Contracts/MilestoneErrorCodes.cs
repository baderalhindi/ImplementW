namespace PMPlatform.Application.Features.Milestone.Contracts;

/// <summary>The Milestone module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class MilestoneErrorCodes
{
    /// <summary>422: an achievement is claimed, edited and submitted while the milestone's project is ACTIVE.</summary>
    public const string ProjectNotActive = "MILESTONE_PROJECT_NOT_ACTIVE";

    /// <summary>422: the milestone does not exist for the caller, or is CANCELLED in the schedule.</summary>
    public const string NotClaimable = "MILESTONE_NOT_CLAIMABLE";

    /// <summary>422: the claimed achievement date is after today: an achievement is claimed once it has happened.</summary>
    public const string ClaimedDateInvalid = "MILESTONE_CLAIMED_DATE_INVALID";

    /// <summary>409: the milestone already has an open revision, DRAFT or SUBMITTED.</summary>
    public const string AchievementOpen = "MILESTONE_ACHIEVEMENT_OPEN";

    /// <summary>409: only a DRAFT is edited, deleted or has its evidence changed; a submitted claim changes by WF-11's outcome only.</summary>
    public const string AchievementNotEditable = "MILESTONE_ACHIEVEMENT_NOT_EDITABLE";

    /// <summary>422: the revision lacks evidence EVIDENCE_POLICY makes mandatory for the milestone's category (PTBC-006).</summary>
    public const string EvidenceRequired = "MILESTONE_EVIDENCE_REQUIRED";
}
