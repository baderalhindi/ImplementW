using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Milestone;

/// <summary>
/// A DRAFT's claim, as a whole (R-5): the date the milestone was achieved on and, optionally, what the claimant says of it. The
/// status, the accepted date and the review are set by the workflow, never sent; an unknown property is ignored.
/// </summary>
public sealed record MilestoneAchievementRequest(DateOnly? ClaimedAchievementDate, NarrativeTextRequest? Narrative)
{
    internal List<FieldError> Validate(out MilestoneAchievementChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? narrative = ValidateInputs(errors);
        changes = errors.Count == 0 ? new MilestoneAchievementChanges(ClaimedAchievementDate!.Value, narrative) : null;
        return errors;
    }

    internal NarrativeText? ValidateInputs(List<FieldError> errors)
    {
        if (ClaimedAchievementDate is null)
        {
            errors.Add(new FieldError("claimedAchievementDate", FieldError.Required));
        }

        return Narrative?.Validate("narrative", errors);
    }
}

/// <summary>
/// A new revision of a milestone's achievement claim: the shared milestone it claims, and the claim
/// <see cref="MilestoneAchievementRequest"/> describes. The same request opens a first claim, a claim after a return, and a
/// correction of an accepted achievement.
/// </summary>
public sealed record MilestoneAchievementCreateRequest(Guid? ProjectMilestoneId, DateOnly? ClaimedAchievementDate, NarrativeTextRequest? Narrative)
{
    internal List<FieldError> Validate(out MilestoneAchievementDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectMilestoneId, "projectMilestoneId", errors);
        NarrativeText? narrative = new MilestoneAchievementRequest(ClaimedAchievementDate, Narrative).ValidateInputs(errors);
        draft = errors.Count == 0 ? new MilestoneAchievementDraft(ProjectMilestoneId!.Value, ClaimedAchievementDate!.Value, narrative) : null;
        return errors;
    }
}

/// <summary>A document to attach as evidence: the document, the version of it to pin, and the EVIDENCE_TYPE item it is evidence of.</summary>
public sealed record MilestoneEvidenceRequest(Guid? DocumentId, Guid? DocumentVersionId, Guid? EvidenceTypeItemId)
{
    internal List<FieldError> Validate(out MilestoneEvidenceAttachment? attachment)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(DocumentId, "documentId", errors);
        RequestValidation.RequireId(DocumentVersionId, "documentVersionId", errors);
        RequestValidation.RequireId(EvidenceTypeItemId, "evidenceTypeItemId", errors);
        attachment = errors.Count == 0 ? new MilestoneEvidenceAttachment(DocumentId!.Value, DocumentVersionId!.Value, EvidenceTypeItemId!.Value) : null;
        return errors;
    }
}
