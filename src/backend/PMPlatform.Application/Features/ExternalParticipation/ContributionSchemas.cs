using PMPlatform.Application.Features.ExternalParticipation.Contracts;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// A contribution type's typed schema (WF-13 §5.2, §7.1): the fields an entity may answer — nothing else is stored, and nothing else
/// reaches a source — what an accepted answer does, and to which kind of source record (BR-EXT-008, BR-EXT-047).
/// </summary>
internal sealed record ContributionSchema(
    string Code, ContributionApplicationMode Mode, string? TargetModule, string? TargetType, IReadOnlyList<ContributionFieldDefinition> Fields)
{
    public ContributionFieldDefinition? Field(string fieldCode) => Fields.FirstOrDefault(f => f.FieldCode == fieldCode);

    public bool HasTarget => TargetType is not null;

    /// <summary>Groups of fields of which at least one must be answered whole, beside the required fields.</summary>
    public IReadOnlyList<IReadOnlyList<string>>? RequiredOneOf { get; init; }
}

/// <summary>
/// The allowlist of typed schemas, keyed by the CONTRIBUTION_TYPE item code AHDA publishes for each (TASK-066). A contribution type whose
/// code is not here has no typed contract and is refused (EXT-ERR-015): there is no generic "submit anything" schema and no generic field
/// patch (WF-13 §5.4, BR-EXT-020). FG-04 enables each per participation mode; the fields and the source mapping are the platform's.
/// </summary>
internal static class ContributionSchemas
{
    /// <summary>An entity's report of a WF-04 task's actual percentage, applied to the task after AHDA's review (UPDATE_ALLOWED_SOURCE_FIELDS).</summary>
    public const string TaskProgress = "TASK_PROGRESS";

    /// <summary>Information AHDA asks an entity for, kept as accepted input and applied to no source (REFERENCE_ONLY).</summary>
    public const string ProjectInformation = "PROJECT_INFORMATION";

    /// <summary>The monthly update of a project (the 5 Nov 2026 review §2.1), written to the project when its reviewer accepts it.</summary>
    public const string MonthlyUpdate = "MONTHLY_UPDATE";

    public const string ActualProgressPercent = "actualProgressPercent";
    public const string ActualSpendPercent = "actualSpendPercent";
    public const string CurrentObstacle = "currentObstacle";
    public const string NextAction = "nextAction";
    public const string NextActionDueDate = "nextActionDueDate";
    public const string Note = "note";

    /// <summary>Review round 7, item 1: a project with a stage plan reports its current stage and that stage's % complete; the overall figure is weighed from them.</summary>
    public const string CurrentStageCode = "currentStageCode";
    public const string CurrentStagePercent = "currentStagePercent";

    public const string ActualPercentComplete = "actualPercentComplete";
    public const string ProgressNote = "progressNote";
    public const string Response = "response";
    public const string AsOfDate = "asOfDate";

    private static readonly Dictionary<string, ContributionSchema> Schemas = new(StringComparer.Ordinal)
    {
        [TaskProgress] = new(
            TaskProgress,
            ContributionApplicationMode.UpdateAllowedSourceFields,
            TaskProgressTarget.Module,
            TaskProgressTarget.Type,
            [
                new(ActualPercentComplete, ContributionFieldType.Number, Required: true, Minimum: 0m, Maximum: 100m),
                new(ProgressNote, ContributionFieldType.Narrative, Required: false, Minimum: null, Maximum: null),
            ]),
        [MonthlyUpdate] = new(
            MonthlyUpdate,
            ContributionApplicationMode.ApplyOnAccept,
            ProjectMonthlyTarget.Module,
            ProjectMonthlyTarget.Type,
            [
                new(ActualProgressPercent, ContributionFieldType.Number, Required: false, Minimum: 0m, Maximum: 100m),
                new(CurrentStageCode, ContributionFieldType.Code, Required: false, Minimum: null, Maximum: null),
                new(CurrentStagePercent, ContributionFieldType.Number, Required: false, Minimum: 0m, Maximum: 100m),
                new(ActualSpendPercent, ContributionFieldType.Number, Required: false, Minimum: 0m, Maximum: 100m),
                new(CurrentObstacle, ContributionFieldType.Narrative, Required: false, Minimum: null, Maximum: null),
                new(NextAction, ContributionFieldType.Narrative, Required: false, Minimum: null, Maximum: null),
                new(NextActionDueDate, ContributionFieldType.Date, Required: false, Minimum: null, Maximum: null),
                new(Note, ContributionFieldType.Narrative, Required: false, Minimum: null, Maximum: null),
            ])
        {
            // The overall % as before, or, for a project with a stage plan, its current stage and that stage's %.
            RequiredOneOf = [[ActualProgressPercent], [CurrentStageCode, CurrentStagePercent]],
        },
        [ProjectInformation] = new(
            ProjectInformation,
            ContributionApplicationMode.ReferenceOnly,
            null,
            null,
            [
                new(Response, ContributionFieldType.Narrative, Required: true, Minimum: null, Maximum: null),
                new(AsOfDate, ContributionFieldType.Date, Required: false, Minimum: null, Maximum: null),
            ]),
    };

    public static IReadOnlyCollection<ContributionSchema> All => Schemas.Values;

    public static ContributionSchema? Find(string code) => Schemas.GetValueOrDefault(code);

    public static ContributionSchema Of(string code) =>
        Find(code) ?? throw new InvalidOperationException($"A request names the schema {code}, which the platform does not define.");
}
