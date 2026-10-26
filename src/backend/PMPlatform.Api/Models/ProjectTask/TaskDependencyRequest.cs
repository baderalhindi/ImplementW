using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Api.Models.ProjectTask;

/// <summary>
/// A blocking dependency between two leaf tasks: FS or SS holds back the successor's start until the predecessor is completed
/// or has started, FF or SF its completion.
/// </summary>
public sealed record TaskDependencyRequest(Guid? PredecessorTaskId, Guid? SuccessorTaskId, string? DependencyType)
{
    internal List<FieldError> Validate(out TaskDependencyDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(PredecessorTaskId, "predecessorTaskId", errors);
        RequestValidation.RequireId(SuccessorTaskId, "successorTaskId", errors);
        TaskDependencyType? type = DependencyType switch
        {
            "FS" => TaskDependencyType.Fs,
            "SS" => TaskDependencyType.Ss,
            "FF" => TaskDependencyType.Ff,
            "SF" => TaskDependencyType.Sf,
            _ => null,
        };
        if (type is null)
        {
            errors.Add(new FieldError("dependencyType", DependencyType is null ? FieldError.Required : FieldError.EnumValue));
        }

        draft = errors.Count == 0 ? new TaskDependencyDraft(PredecessorTaskId!.Value, SuccessorTaskId!.Value, type!.Value) : null;
        return errors;
    }
}
