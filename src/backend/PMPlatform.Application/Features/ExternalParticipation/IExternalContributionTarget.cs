using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// ADR-003 §8.2 edge 19's typed adapter: how WF-13 reaches one kind of source record (WF-13 §7.2). Each implementation reads the record
/// through the owning module's query and applies an accepted answer only through that module's typed command, which revalidates the
/// record's version and state and applies its own rules — WF-13 never writes another module's data (M-3). One implementation per allowlisted
/// <see cref="ContributionSchema.TargetType"/>; a schema naming a type with no adapter is a programming error.
/// </summary>
internal interface IExternalContributionTarget
{
    public string TargetModule { get; }

    public string TargetType { get; }

    /// <summary>The record as it is now: its project, row version, state and a label safe to show the entity; null when there is none.</summary>
    public Task<SourceRecordFacts?> FindAsync(Guid targetId, CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates the record and, when it is as the answer expects and its module's rules allow the change, stages the change in the caller's
    /// unit of work. Nothing is staged unless the result is <see cref="SourceApplicationStatus.Applied"/>.
    /// </summary>
    public Task<SourceApplicationResult> StageAsync(SourceApplicationCommand command, CancellationToken cancellationToken);
}

internal sealed record SourceRecordFacts(Guid Id, Guid ProjectId, long Version, string State, NarrativeText Label);

/// <summary>An accepted revision's values for one source record, the version it expects, and who applies it on which attempt.</summary>
internal sealed record SourceApplicationCommand(
    Guid TargetId,
    long ExpectedVersion,
    IReadOnlyDictionary<string, string> Values,
    Guid ActorId,
    Guid ExternalEntityId,
    Guid ExternalContributionId,
    int RevisionNo,
    Guid SourceApplicationId);

/// <summary>
/// The attempt's outcome: APPLIED; CONFLICT with the version found; or FAILED with the safe code of the refusal, <see cref="Terminal"/> when the
/// record can never take the change (WF-13 §7.3 SOURCE_MISSING, SOURCE_TERMINAL).
/// </summary>
internal sealed record SourceApplicationResult(SourceApplicationStatus Status, long? ActualVersion, string? FailureCode, bool Terminal)
{
    public static SourceApplicationResult Applied { get; } = new(SourceApplicationStatus.Applied, null, null, Terminal: false);

    public static SourceApplicationResult Conflict(long actualVersion) => new(SourceApplicationStatus.Conflict, actualVersion, null, Terminal: false);

    public static SourceApplicationResult Failed(string failureCode, bool terminal) => new(SourceApplicationStatus.Failed, null, failureCode, terminal);
}
