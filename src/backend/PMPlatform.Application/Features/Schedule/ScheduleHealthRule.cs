using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// Schedule Health (the spec's §9.17, DCL-SCH-17): the project's finish variance against the configured thresholds — late
/// by the AMBER threshold or more is AMBER, by the RED threshold or more is RED, anything less GREEN. Without a variance
/// (no ACTIVE baseline, or nothing forecast) or without thresholds it is UNKNOWN: a missing input is never a colour.
/// </summary>
internal static class ScheduleHealthRule
{
    public static ScheduleHealth Rate(int? finishVarianceDays, ScheduleHealthThresholds? thresholds) =>
        (finishVarianceDays, thresholds) switch
        {
            (null, _) or (_, null) => ScheduleHealth.Unknown,
            ({ } late, { } t) when late >= t.RedFinishVarianceDays => ScheduleHealth.Red,
            ({ } late, { } t) when late >= t.AmberFinishVarianceDays => ScheduleHealth.Amber,
            _ => ScheduleHealth.Green,
        };
}

/// <summary>Working days of finish variance from which the schedule is AMBER and RED; 0 &lt; AMBER ≤ RED.</summary>
internal sealed record ScheduleHealthThresholds(int AmberFinishVarianceDays, int RedFinishVarianceDays);
