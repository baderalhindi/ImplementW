using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.IntegrationMonitoring;

/// <summary>FG-05 §7.5, §13.3: resolving or dismissing an alert takes the operator's note, in the language it was entered in.</summary>
public sealed record OperationalAlertNoteCommand(NarrativeTextRequest? Note)
{
    internal List<FieldError> Validate(out NarrativeText? note)
    {
        List<FieldError> errors = [];
        note = Note?.Validate("note", errors);
        if (Note is null)
        {
            errors.Add(new FieldError("note", FieldError.Required));
        }

        return errors;
    }
}
