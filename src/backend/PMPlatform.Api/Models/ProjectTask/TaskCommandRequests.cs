using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.ProjectTask;

/// <summary>Why the task is blocked, in the language it was entered in.</summary>
public sealed record TaskBlockCommand(NarrativeTextRequest? Reason)
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

/// <summary>A leaf's actual percentage, 0–100 to four places (ADR-009).</summary>
public sealed record TaskProgressCommand(decimal? ActualPercentComplete)
{
    private const int Places = 4;

    internal List<FieldError> Validate(out decimal percent)
    {
        List<FieldError> errors = [];
        percent = ActualPercentComplete ?? 0m;
        if (ActualPercentComplete is null)
        {
            errors.Add(new FieldError("actualPercentComplete", FieldError.Required));
        }
        else if (percent is < 0 or > 100 || decimal.Round(percent, Places) != percent)
        {
            errors.Add(new FieldError("actualPercentComplete", FieldError.OutOfRange));
        }

        return errors;
    }
}
