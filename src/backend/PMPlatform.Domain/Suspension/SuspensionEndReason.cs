namespace PMPlatform.Domain.Suspension;

/// <summary>Why a suspension period ended (TASK-063): the project resumed, or it was closed from SUSPENDED without resuming (WF-10 §9).</summary>
public enum SuspensionEndReason
{
    /// <summary>An effected resumption request ended it; the project is ACTIVE again.</summary>
    Resumed = 1,

    /// <summary>An effected closure case ended it; the project is CLOSED, terminated without completion.</summary>
    ProjectClosed = 2,
}
