using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>Free text in the language it was entered in (ERD D-7; R-18).</summary>
public sealed record NarrativeTextRequest(string? Text, string? Language)
{
    public const int TextLength = 2000;

    internal NarrativeText? Validate(string field, List<FieldError> errors)
    {
        int before = errors.Count;
        RequestValidation.RequireText(Text, $"{field}.text", TextLength, errors);
        Language language = RequestValidation.Language(Language, $"{field}.language", fallback: null, errors);
        return errors.Count == before ? new NarrativeText(Text!, language) : null;
    }
}
