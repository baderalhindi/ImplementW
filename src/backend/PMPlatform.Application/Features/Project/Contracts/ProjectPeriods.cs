namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// The years a project's planned period touches, by which the Period filter (the 2 Nov 2026 review §1) finds it: a project planned from
/// 2025 to 2027 is in 2025, 2026 and 2027. A project with one planned date is in that year; one with none is in no period.
/// </summary>
public static class ProjectPeriods
{
    /// <summary>A span longer than this is a data error, not a project: only its first years are counted.</summary>
    private const int MaxYears = 30;

    public static IEnumerable<int> YearsOf(DateOnly? start, DateOnly? end)
    {
        if ((start ?? end) is not { } first || (end ?? start) is not { } last)
        {
            return [];
        }

        (int from, int to) = first.Year <= last.Year ? (first.Year, last.Year) : (last.Year, first.Year);
        return Enumerable.Range(from, Math.Min(to - from + 1, MaxYears));
    }

    /// <summary>Whether the planned period touches <paramref name="year"/>.</summary>
    public static bool Touches(DateOnly? start, DateOnly? end, int year) => YearsOf(start, end).Contains(year);
}
