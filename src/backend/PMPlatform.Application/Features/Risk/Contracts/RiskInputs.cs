using PMPlatform.Domain.Common;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>A new risk of the project, IDENTIFIED. Its rating comes only from an assessment, never from here.</summary>
public sealed record RiskDraft(
    Guid ProjectId,
    NarrativeText Title,
    NarrativeText Description,
    Guid RiskCategoryItemId,
    Guid? OwnerUserId,
    DateOnly IdentifiedDate,
    DateOnly? NextReviewDate);

/// <summary>A risk's register fields, as a whole (R-5). Its project, status and rating change only through the commands.</summary>
public sealed record RiskChanges(
    NarrativeText Title,
    NarrativeText Description,
    Guid RiskCategoryItemId,
    Guid? OwnerUserId,
    DateOnly IdentifiedDate,
    DateOnly? NextReviewDate);

/// <summary>
/// An assessment: a probability level and one impact level for each dimension of the RISK_MATRIX version in force (ADR-011).
/// The overall impact and the rating are computed by the server and are never input.
/// </summary>
public sealed record RiskAssessmentDraft(short ProbabilityLevel, IReadOnlyList<RiskImpactInput> Impacts, NarrativeText? Rationale);

public sealed record RiskImpactInput(Guid ImpactDimensionItemId, short ImpactLevel, NarrativeText? Rationale);

/// <summary>An acceptance until <see cref="ExpiresOn"/>, the first day it no longer holds.</summary>
public sealed record RiskAcceptanceDraft(DateOnly ExpiresOn, NarrativeText Rationale);

/// <summary>
/// The issue to raise from the risk. Its title and description default to the risk's; category and priority are WF-07's
/// fields, which it validates.
/// </summary>
public sealed record RiskMaterialisationDraft(NarrativeText? Title, NarrativeText? Description, Guid CategoryItemId, Guid PriorityItemId);

public sealed record RiskTreatmentActionDraft(
    Guid RiskId, NarrativeText Title, NarrativeText? Description, RiskTreatmentActionType ActionType, Guid? OwnerUserId, DateOnly? DueDate);

/// <summary>An action's plan, as a whole (R-5). Its risk never changes; its status only through the commands.</summary>
public sealed record RiskTreatmentActionChanges(
    NarrativeText Title, NarrativeText? Description, RiskTreatmentActionType ActionType, Guid? OwnerUserId, DateOnly? DueDate);

/// <summary>A project's risk register, filtered: by any of <see cref="Statuses"/>, by owner and by next review date, both ends included.</summary>
public sealed record RiskQuery(
    Guid ProjectId, IReadOnlyCollection<RiskStatus> Statuses, Guid? OwnerUserId, DateOnly? NextReviewDateFrom, DateOnly? NextReviewDateTo);
