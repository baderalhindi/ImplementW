using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Approval;

/// <summary>MOD-040–042 and MOD-045: the reason for a decision or an escalation, in the language it was entered in.</summary>
public sealed record ApprovalDecisionCommand(NarrativeTextRequest? Reason)
{
    /// <param name="reasonRequired">Rejecting and returning need a reason (TASK-036); approving and escalating do not.</param>
    /// <param name="reason">The reason, when one was given and is valid.</param>
    internal List<FieldError> Validate(bool reasonRequired, out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        reason = Reason?.Validate("reason", errors);
        if (Reason is null && reasonRequired)
        {
            errors.Add(new FieldError("reason", FieldError.Required));
        }

        return errors;
    }
}
