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
