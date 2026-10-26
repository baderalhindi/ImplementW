namespace PMPlatform.Application.Features.Approval;

/// <summary>Why someone may not decide a task now. Recorded on the refusal's audit event, never returned to the caller.</summary>
internal enum ApprovalRefusal
{
    None = 0,

    /// <summary>ADR-013: an external user holds no approval authority of any kind, directly or by delegation.</summary>
    ExternalUser = 1,

    /// <summary>The account is unknown or not active.</summary>
    InactiveUser = 2,

    /// <summary>No one approves their own request, by their own authority or a delegator's.</summary>
    Requester = 3,

    /// <summary>Neither the caller nor any delegator of theirs holds the task's role with a scope covering the run.</summary>
    NoAuthority = 4,

    /// <summary>Only the requester escalates or withdraws a run.</summary>
    NotRequester = 5,
}
