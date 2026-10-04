namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// Which of the two views a financial representation is (api-conventions R-20(c), M-12): the CURRENT/LIVE position, computed on
/// read from the figures as they stand, or the PUBLISHED/OFFICIAL snapshot, fixed at publication. They are never merged.
/// </summary>
public enum SemanticState
{
    CurrentLive = 1,
    PublishedOfficial = 2,
}
