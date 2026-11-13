using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Project;

/// <summary>The whole registration, as a draft's edit sends it, and why it changes after review (review round 7, item 4). Both are required.</summary>
public sealed record ProjectRegistrationEditRequest(ProjectRequest? Registration, NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(out ProjectDraft? draft, out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        draft = null;
        if (Registration is null)
        {
            errors.Add(new FieldError("registration", FieldError.Required));
        }
        else
        {
            errors.AddRange(Registration.Validate(out draft).Select(e => e with { Field = $"registration.{e.Field}" }));
        }

        if (Reason is null)
        {
            errors.Add(new FieldError("reason", FieldError.Required));
        }

        reason = Reason?.Validate("reason", errors);
        return errors;
    }
}
