using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Api.Models.Schedule;

/// <summary>
/// A dependency between two leaf activities: FS, SS or FF (BR-SCH-024; SF is deferred) with a non-negative lag in working
/// days, 0 when absent (BR-SCH-025).
/// </summary>
public sealed record ScheduleDependencyRequest(Guid? PredecessorActivityId, Guid? SuccessorActivityId, string? DependencyType, int? LagDays)
{
    internal List<FieldError> Validate(out ScheduleDependencyDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(PredecessorActivityId, "predecessorActivityId", errors);
        RequestValidation.RequireId(SuccessorActivityId, "successorActivityId", errors);
        ScheduleDependencyType? type = DependencyType switch
        {
            "FS" => ScheduleDependencyType.Fs,
            "SS" => ScheduleDependencyType.Ss,
            "FF" => ScheduleDependencyType.Ff,
            _ => null,
        };
        if (type is null)
        {
            errors.Add(new FieldError("dependencyType", DependencyType is null ? FieldError.Required : FieldError.EnumValue));
        }

        if (LagDays is < 0 or > ScheduleActivityRequest.MaxDurationDays)
        {
            errors.Add(new FieldError("lagDays", FieldError.OutOfRange));
        }

        draft = errors.Count == 0 ? new ScheduleDependencyDraft(PredecessorActivityId!.Value, SuccessorActivityId!.Value, type!.Value, LagDays ?? 0) : null;
        return errors;
    }
}
