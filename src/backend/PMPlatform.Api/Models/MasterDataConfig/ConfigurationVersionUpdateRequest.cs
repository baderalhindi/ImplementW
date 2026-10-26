using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>A DRAFT version's editable representation in full (R-5): its change summary and its whole content.</summary>
public sealed record ConfigurationVersionUpdateRequest(NarrativeTextRequest? ChangeSummary, ConfigurationContentRequest? Content)
{
    internal List<FieldError> Validate(out ConfigurationVersionChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? changeSummary = ChangeSummary?.Validate("changeSummary", errors);
        ConfigurationContent? content = null;
        if (Content is null)
        {
            errors.Add(new FieldError("content", FieldError.Required));
        }
        else
        {
            errors.AddRange(Content.Validate(out content).Select(e => e with { Field = $"content.{e.Field}" }));
        }

        changes = errors.Count > 0 ? null : new ConfigurationVersionChanges(changeSummary, content!);
        return errors;
    }
}
