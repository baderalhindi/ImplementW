using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>A new DRAFT version of a family, empty or copied from <see cref="BasedOnVersionId"/>.</summary>
public sealed record ConfigurationVersionCreateRequest(Guid? FamilyId, Guid? BasedOnVersionId, NarrativeTextRequest? ChangeSummary)
{
    internal List<FieldError> Validate(out ConfigurationVersionDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(FamilyId, "familyId", errors);
        NarrativeText? changeSummary = ChangeSummary?.Validate("changeSummary", errors);
        draft = errors.Count > 0 ? null : new ConfigurationVersionDraft(FamilyId!.Value, BasedOnVersionId, changeSummary);
        return errors;
    }
}
