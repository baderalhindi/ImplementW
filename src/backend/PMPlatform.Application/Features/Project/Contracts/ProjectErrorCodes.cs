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
}
