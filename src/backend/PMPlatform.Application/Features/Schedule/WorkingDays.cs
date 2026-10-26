namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// Working-day arithmetic of the schedule (the spec's §12.3): a duration counts its first day as day 1, and a variance is
/// the signed number of working days from one date to another. No working calendar is configured — the platform has no
/// calendar catalogue and the working week and holidays are TBC-SCH-001 — so every day is a working day
/// (schedule-baseline.md F-3). A calendar replaces this class's body, not its callers.
/// </summary>
internal static class WorkingDays
{
    /// <summary>The finish of <paramref name="durationDays"/> working days starting on <paramref name="start"/>.</summary>
    public static DateOnly FinishOf(DateOnly start, int durationDays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationDays, 1);
        return start.AddDays(durationDays - 1);
    }

    /// <summary>The start of <paramref name="durationDays"/> working days finishing on <paramref name="finish"/>.</summary>
    public static DateOnly StartOf(DateOnly finish, int durationDays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationDays, 1);
        return finish.AddDays(1 - durationDays);
    }

    /// <summary>The working day <paramref name="days"/> after <paramref name="date"/>; zero is the date itself.</summary>
    public static DateOnly Add(DateOnly date, int days) => date.AddDays(days);

    /// <summary>The working days from <paramref name="start"/> to <paramref name="finish"/>, both included.</summary>
    public static int Span(DateOnly start, DateOnly finish) => finish.DayNumber - start.DayNumber + 1;

    /// <summary>Signed working days from <paramref name="reference"/> to <paramref name="date"/>: positive is later (BR-SCH-032).</summary>
    public static int Variance(DateOnly reference, DateOnly date) => date.DayNumber - reference.DayNumber;
}
