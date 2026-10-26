using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Progress;

/// <summary>Returning a submission says why.</summary>
public sealed record ProgressReturnCommand(NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        if (Reason is null)
        {
            errors.Add(new FieldError("reason", FieldError.Required));
        }

        reason = Reason?.Validate("reason", errors);
        return errors;
    }
}
