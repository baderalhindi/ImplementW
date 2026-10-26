namespace PMPlatform.Domain.ChangeRequest;

/// <summary>ERD <c>change_authorization.authorization_scope</c>: the one kind of change an authorisation permits.</summary>
public enum ChangeAuthorizationScope
{
    /// <summary>Replace the ACTIVE Approved Baseline (WF-03, ADR-003 §8.2 edge 11).</summary>
    Rebaseline = 1,

    /// <summary>Replace the ACTIVE Approved Budget version (WF-14, edge 12).</summary>
    CommitmentChange = 2,

    /// <summary>Change the project's governance profile (TASK-105). No module applies it yet.</summary>
    ProfileChange = 3,

    /// <summary>Change the project's scope. No module applies it yet.</summary>
    ScopeChange = 4,
}
