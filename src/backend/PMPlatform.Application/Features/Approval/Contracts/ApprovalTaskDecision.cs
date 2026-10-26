namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>What an approver decides on a task (MOD-040–042).</summary>
public enum ApprovalTaskDecision
{
    Approve = 1,

    /// <summary>Ends the run: the subject is refused. Needs a reason.</summary>
    Reject = 2,

    /// <summary>Ends the run: the subject goes back to its author, who resubmits it as a new revision. Needs a reason.</summary>
    Return = 3,
}
