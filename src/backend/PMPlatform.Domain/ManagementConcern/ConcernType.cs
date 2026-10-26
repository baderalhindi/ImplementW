namespace PMPlatform.Domain.ManagementConcern;

/// <summary>ERD <c>management_concern.concern_type</c>: one aggregate for both (WF-07 ISS-GP-01). Fixed when the concern is raised.</summary>
public enum ConcernType
{
    /// <summary>A problem that has occurred or exists now and needs resolution.</summary>
    Issue = 1,

    /// <summary>A significant constraint or obstacle that needs coordinated attention without being a discrete incident.</summary>
    Challenge = 2,
}
