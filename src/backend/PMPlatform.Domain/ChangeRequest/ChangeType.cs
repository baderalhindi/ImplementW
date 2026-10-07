namespace PMPlatform.Domain.ChangeRequest;

/// <summary>ERD <c>change_request.change_type</c>: what the request changes. Fixed when the request is raised.</summary>
public enum ChangeType
{
    /// <summary>A governed scope boundary; states its scope impact.</summary>
    Scope = 1,

    /// <summary>The Approved Budget; states its cost impact.</summary>
    Cost = 2,

    /// <summary>The Approved Baseline; states its schedule impact.</summary>
    Schedule = 3,

    /// <summary>A contractual obligation: always band 3 (TASK-106).</summary>
    ContractualObligation = 4,

    /// <summary>The project's governance profile (TASK-105); names the profile asked for.</summary>
    GovernanceProfile = 5,
}
