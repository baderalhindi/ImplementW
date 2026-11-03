using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Project;

/// <summary>
/// A project's whole follow-up (R-5; the 2 Nov 2026 review, R3-3): what is not sent is cleared. Percentages are 0 to 100 and optional —
/// blank is "not available", never zero. <c>note</c>, when sent, is added as a dated follow-up note.
/// </summary>
public sealed record ProjectFollowUpRequest(
    NarrativeTextRequest? CurrentObstacle = null,
    NarrativeTextRequest? NextAction = null,
    string? NextActionOwner = null,
    DateOnly? NextActionDueDate = null,
    decimal? DeclaredPlannedPercent = null,
    decimal? DeclaredActualPercent = null,
    decimal? DeclaredPlannedSpendPercent = null,
    decimal? DeclaredActualSpendPercent = null,
    DateOnly? DeclaredAsOf = null,
    bool? EntityUpdateCompliant = null,
    NarrativeTextRequest? Note = null)
{
    private const int OwnerLength = 200;

    internal List<FieldError> Validate(out ProjectFollowUpChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? obstacle = CurrentObstacle?.Validate("currentObstacle", errors);
        NarrativeText? nextAction = NextAction?.Validate("nextAction", errors);
        NarrativeText? note = Note?.Validate("note", errors);
        RequestValidation.Optional(NextActionOwner, "nextActionOwner", OwnerLength, errors);
        Percent(DeclaredPlannedPercent, "declaredPlannedPercent", errors);
        Percent(DeclaredActualPercent, "declaredActualPercent", errors);
        Percent(DeclaredPlannedSpendPercent, "declaredPlannedSpendPercent", errors);
        Percent(DeclaredActualSpendPercent, "declaredActualSpendPercent", errors);
        changes = errors.Count == 0
            ? new ProjectFollowUpChanges(
                obstacle, nextAction, NextActionOwner?.Trim(), NextActionDueDate, DeclaredPlannedPercent, DeclaredActualPercent, DeclaredPlannedSpendPercent,
                DeclaredActualSpendPercent, DeclaredAsOf, EntityUpdateCompliant, note)
            : null;
        return errors;
    }

    private static void Percent(decimal? value, string field, List<FieldError> errors)
    {
        if (value is < 0 or > 100 || (value is { } v && decimal.Round(v, 4) != v))
        {
            errors.Add(new FieldError(field, FieldError.OutOfRange));
        }
    }
}
