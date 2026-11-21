using System.Globalization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Project;

/// <summary>One stage of the plan: its PROJECT_STAGE code, whether it applies, and while it does its planned dates and, optionally, its starting %.</summary>
public sealed record ProjectStagePlanEntryRequest(string? StageCode, bool? Applies, DateOnly? PlannedStartDate = null, DateOnly? PlannedEndDate = null, decimal? StartingPercent = null);

/// <summary>
/// A project's whole stage plan (review round 7, item 1): every stage of the version in force. Once the registration is approved,
/// <c>changeAuthorizationId</c> names the STAGE_PLAN_CHANGE authorisation an approved schedule change issued, and <c>reason</c> says why.
/// </summary>
public sealed record ProjectStagePlanRequest(IReadOnlyList<ProjectStagePlanEntryRequest>? Stages, Guid? ChangeAuthorizationId = null, NarrativeTextRequest? Reason = null)
{
    private const int MaxStages = 50;

    internal List<FieldError> Validate(out ProjectStagePlanChanges? changes)
    {
        List<FieldError> errors = [];
        if (Stages is null || Stages.Count == 0)
        {
            errors.Add(new FieldError("stages", FieldError.Required));
        }
        else if (Stages.Count > MaxStages)
        {
            errors.Add(new FieldError("stages", FieldError.OutOfRange));
        }
        else
        {
            for (int i = 0; i < Stages.Count; i++)
            {
                string path = $"stages[{i.ToString(CultureInfo.InvariantCulture)}]";
                ProjectStagePlanEntryRequest? stage = Stages[i];
                if (stage is null)
                {
                    errors.Add(new FieldError(path, FieldError.Required));
                    continue;
                }

                RequestValidation.Code(stage.StageCode, $"{path}.stageCode", errors);
                if (stage.Applies is null)
                {
                    errors.Add(new FieldError($"{path}.applies", FieldError.Required));
                }

                if (stage.StartingPercent is < 0 or > 100 || (stage.StartingPercent is { } p && decimal.Round(p, 2) != p))
                {
                    errors.Add(new FieldError($"{path}.startingPercent", FieldError.OutOfRange));
                }
            }
        }

        NarrativeText? reason = Reason?.Validate("reason", errors);
        changes = errors.Count == 0
            ? new ProjectStagePlanChanges(
                [.. Stages!.Select(s => new ProjectStagePlanEntryChange(s.StageCode!.Trim(), s.Applies!.Value, s.PlannedStartDate, s.PlannedEndDate, s.StartingPercent))],
                ChangeAuthorizationId,
                reason)
            : null;
        return errors;
    }
}
