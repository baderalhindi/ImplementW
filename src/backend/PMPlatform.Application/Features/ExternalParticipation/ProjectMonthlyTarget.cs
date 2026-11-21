using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>The monthly update's source: its project. A reviewer's acceptance is the latest word, so the version an answer saw is not compared.</summary>
internal sealed class ProjectMonthlyTarget(IProjectMonthlyUpdates projects) : IExternalContributionTarget
{
    public const string Module = "Project";

    public const string Type = "Project";

    public string TargetModule => Module;

    public string TargetType => Type;

    public async Task<SourceRecordFacts?> FindAsync(Guid targetId, CancellationToken cancellationToken) =>
        await projects.ReadAsync(targetId, cancellationToken).ConfigureAwait(false) is { } project
            ? new SourceRecordFacts(project.Id, project.Id, 1, System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(project.Status.ToString()), project.Title)
            : null;

    public async Task<SourceApplicationResult> StageAsync(SourceApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        IReadOnlyDictionary<string, string> values = command.Values;
        ProjectMonthlyUpdateValues update = new(
            values.TryGetValue(ContributionSchemas.ActualProgressPercent, out string? overall) ? ContributionValues.NumberOf(overall) : null,
            values.TryGetValue(ContributionSchemas.ActualSpendPercent, out string? spend) ? ContributionValues.NumberOf(spend) : null,
            Narrative(command, ContributionSchemas.CurrentObstacle),
            Narrative(command, ContributionSchemas.NextAction),
            values.TryGetValue(ContributionSchemas.NextActionDueDate, out string? due) ? DateOnly.ParseExact(due, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : null,
            Narrative(command, ContributionSchemas.Note),
            values.GetValueOrDefault(ContributionSchemas.CurrentStageCode),
            values.TryGetValue(ContributionSchemas.CurrentStagePercent, out string? stage) ? ContributionValues.NumberOf(stage) : null);
        return await projects.StageAsync(command.TargetId, update, command.ActorId, cancellationToken).ConfigureAwait(false)
            ? SourceApplicationResult.Applied
            : SourceApplicationResult.Failed(ExternalParticipationErrorCodes.SourceRecordTerminal, terminal: true);
    }

    private static NarrativeText? Narrative(SourceApplicationCommand command, string field) =>
        command.Values.TryGetValue(field, out string? text) && !string.IsNullOrWhiteSpace(text)
            ? new NarrativeText(text, command.Languages?.GetValueOrDefault(field, Language.Ar) ?? Language.Ar)
            : null;
}
