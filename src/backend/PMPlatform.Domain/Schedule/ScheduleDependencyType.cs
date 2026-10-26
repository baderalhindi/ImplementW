namespace PMPlatform.Domain.Schedule;

/// <summary>
/// The dependency types of the WF-03 MVP (the spec's BR-SCH-024, DCL-SCH-07): finish-to-start, start-to-start and
/// finish-to-finish. Start-to-finish is deferred, although the ERD lists it (schedule-baseline.md F-11).
/// </summary>
public enum ScheduleDependencyType
{
    Fs = 1,
    Ss = 2,
    Ff = 3,
}
