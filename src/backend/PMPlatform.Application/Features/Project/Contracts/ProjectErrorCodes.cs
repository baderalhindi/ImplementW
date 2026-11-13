namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>The Project module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ProjectErrorCodes
{
    /// <summary>
    /// 422: a classification, governance profile, region, city, department or entity that is unknown, not PUBLISHED or not
    /// active, or a city outside the region named with it (core-platform-schema N-1).
    /// </summary>
    public const string ReferenceInvalid = "PROJECT_REFERENCE_INVALID";

    /// <summary>422: an ENTITY_MANAGED project names no delivering entity (ADR-013).</summary>
    public const string ParticipationInvalid = "PROJECT_PARTICIPATION_INVALID";

    /// <summary>422: submitting a project that lacks a field review needs: its budget or planned dates.</summary>
    public const string Incomplete = "PROJECT_INCOMPLETE";

    /// <summary>
    /// 422: the named Project Manager is not an active R04 holder over the project's anchors, or is an external user of
    /// another entity (ADR-013).
    /// </summary>
    public const string ManagerInvalid = "PROJECT_MANAGER_INVALID";

    /// <summary>409: only a DRAFT or RETURNED project is edited, and only a DRAFT one deleted.</summary>
    public const string NotEditable = "PROJECT_NOT_EDITABLE";

    /// <summary>409: the draft is referenced by another record (a document, an access assignment) and cannot be deleted.</summary>
    public const string InUse = "PROJECT_IN_USE";

    /// <summary>
    /// 409: the project is CLOSED — terminal and read-only (WF-10 BR-CLO-020, TASK-063). Every module answers a write to a closed project's
    /// records with it; the project's own commands answer TERMINAL_STATE.
    /// </summary>
    public const string Closed = "PROJECT_CLOSED";

    /// <summary>409: another project already has this legacy reference, which matches a row on re-import (R3-3).</summary>
    public const string LegacyReferenceTaken = "PROJECT_LEGACY_REFERENCE_TAKEN";

    /// <summary>409: no PROJECT_STAGE version is published, so there are no stages to plan (review round 7, item 1).</summary>
    public const string StagesNotConfigured = "PROJECT_STAGES_NOT_CONFIGURED";

    /// <summary>
    /// 422: a stage plan that names a stage the version in force does not have, or twice, leaves one out, applies none, or dates a stage that applies
    /// outside the project's planned dates or not at all; or gives a starting % to a project that was not under way when it entered the platform.
    /// </summary>
    public const string StagePlanInvalid = "PROJECT_STAGE_PLAN_INVALID";

    /// <summary>409: the registration is under review, and AHDA reviews the stage plan with it: it changes again only if AHDA returns it.</summary>
    public const string StagePlanUnderReview = "PROJECT_STAGE_PLAN_UNDER_REVIEW";

    /// <summary>
    /// 422: the registration is approved, so changing the stage plan's dates or stages is a schedule change: it needs an applicable STAGE_PLAN_CHANGE
    /// authorisation of the plan's current revision, which an approved schedule change request in implementation issues.
    /// </summary>
    public const string StagePlanChangeRequired = "PROJECT_STAGE_PLAN_CHANGE_REQUIRED";

    /// <summary>409: the follow-up of a project not yet approved is not kept: RPMO follows up registered projects (R3-3).</summary>
    public const string FollowUpNotOpen = "PROJECT_FOLLOW_UP_NOT_OPEN";
}
