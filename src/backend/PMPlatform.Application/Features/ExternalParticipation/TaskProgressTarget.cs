using System.Text.Json;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The TASK_PROGRESS schema's source: a WF-04 task's actual percentage, the one allowlisted field, through WF-04's
/// <see cref="ITaskProgressContributions"/> (edge 19). The task's row version is the version token: any change to the task since the answer
/// was given is a conflict, never overwritten (WF-13 EXT-P-09).
/// </summary>
internal sealed class TaskProgressTarget(ITaskProgressContributions tasks) : IExternalContributionTarget
{
    public const string Module = "ProjectTask";
    public const string Type = "ProjectTask";

    public string TargetModule => Module;

    public string TargetType => Type;

    public async Task<SourceRecordFacts?> FindAsync(Guid targetId, CancellationToken cancellationToken) =>
        await tasks.FindAsync(targetId, cancellationToken).ConfigureAwait(false) is { } task
            ? new SourceRecordFacts(task.TaskId, task.ProjectId, task.Version, JsonNamingPolicy.SnakeCaseUpper.ConvertName(task.Status.ToString()), task.Title)
            : null;

    public async Task<SourceApplicationResult> StageAsync(SourceApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        TaskProgressApplication application = await tasks.StageAsync(
                new TaskProgressContribution(
                    command.TargetId,
                    checked((uint)command.ExpectedVersion),
                    ContributionValues.NumberOf(command.Values[ContributionSchemas.ActualPercentComplete]),
                    command.ActorId,
                    new ExternalContributionLineage(command.ExternalEntityId, command.ExternalContributionId, command.RevisionNo, command.SourceApplicationId)),
                cancellationToken)
            .ConfigureAwait(false);

        return application.Outcome switch
        {
            TaskProgressApplicationOutcome.Staged => SourceApplicationResult.Applied,
            TaskProgressApplicationOutcome.VersionChanged => SourceApplicationResult.Conflict(application.CurrentVersion!.Value),
            TaskProgressApplicationOutcome.TaskNotFound => SourceApplicationResult.Failed(ExternalParticipationErrorCodes.SourceRecordNotFound, terminal: true),
            TaskProgressApplicationOutcome.TaskCancelled => SourceApplicationResult.Failed(ExternalParticipationErrorCodes.SourceRecordTerminal, terminal: true),
            TaskProgressApplicationOutcome.ProjectNotEligible => SourceApplicationResult.Failed(ExternalParticipationErrorCodes.ProjectNotEligible, terminal: false),
            TaskProgressApplicationOutcome.ProgressNotEnterable => SourceApplicationResult.Failed(ExternalParticipationErrorCodes.SourceRecordStateInvalid, terminal: false),
            var outcome => throw new ArgumentOutOfRangeException(nameof(command), outcome, "Unknown task progress outcome."),
        };
    }
}
