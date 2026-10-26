namespace PMPlatform.Application.Common.DataImport;

/// <summary>How large a migration workbook may be. A larger load is split, which also bounds each import's one transaction.</summary>
public static class ImportLimits
{
    public const int MaxRows = 5_000;

    /// <summary>The upload itself: a 5,000-row workbook of text is a fraction of this.</summary>
    public const long MaxFileBytes = 10L * 1024 * 1024;
}
