namespace PMPlatform.Application.Common.Authorization;

/// <summary>Which term of the Section 10.1 formula refused the request. Logged, never returned to the caller.</summary>
public enum AuthorizationDenial
{
    None = 0,

    /// <summary>The user is unknown or disabled, or their external entity is not active.</summary>
    InactivePrincipal = 1,

    /// <summary>No active assignment grants the permission.</summary>
    NotGranted = 2,

    /// <summary>The permission is granted, but no grant's data scope or project/entity relationship covers the record.</summary>
    OutOfScope = 3,

    /// <summary>The only grants covering the record are READ-ONLY and the permission changes data.</summary>
    ReadOnlyScope = 4,

    /// <summary>The record is classified above every covering grant's clearance (ADR-010).</summary>
    ClassificationExceeded = 5,

    /// <summary>The record's lifecycle state admits no change.</summary>
    StateLocked = 6,

    /// <summary>The caller holds no authority over the record's current workflow step.</summary>
    NotWorkflowActor = 7,
}
