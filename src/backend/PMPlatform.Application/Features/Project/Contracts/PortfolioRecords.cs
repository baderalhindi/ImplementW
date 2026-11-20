using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// The risk fields the designer offers (the 20 Nov 2026 record sources, dashboard-designer-record-sources.md): keys <c>RISK.&lt;CODE&gt;</c>, one row per risk.
/// Project declares them and the Risk module reads them (<see cref="IPortfolioRiskReader"/>), since Project may not reference Risk (ADR-003 §8.2 edge 3 runs
/// Risk → Project).
/// </summary>
public static class PortfolioRiskFields
{
    /// <summary>The projection code every risk field carries, and the start of its key.</summary>
    public const string Source = "RISK";

    /// <summary>The domain the risk register belongs to.</summary>
    public const string SourceDomain = "WF-06";

    /// <summary>One per risk (<c>1</c>): its count is the number of risks.</summary>
    public const string Risk = "RISK.RISK";

    /// <summary>The risk's title: the cell's value is the risk's id, its label the title.</summary>
    public const string Title = "RISK.TITLE";

    /// <summary>The risk's project: the project's id, labelled by its title.</summary>
    public const string Project = "RISK.PROJECT";

    public const string Status = "RISK.STATUS";

    /// <summary>The RISK_CATEGORY item's id, labelled by its name.</summary>
    public const string Category = "RISK.CATEGORY";

    /// <summary>The owner's user id, labelled by their display name.</summary>
    public const string Owner = "RISK.OWNER";

    /// <summary>The rating code the current assessment recorded against its pinned matrix version.</summary>
    public const string Rating = "RISK.RATING";

    /// <summary>The current assessment's probability level (<c>1</c>…).</summary>
    public const string Probability = "RISK.PROBABILITY";

    /// <summary>The current assessment's overall impact level (<c>1</c>…).</summary>
    public const string Impact = "RISK.IMPACT";

    /// <summary>The current assessment's probability level times its overall impact level.</summary>
    public const string Score = "RISK.SCORE";

    public const string IdentifiedDate = "RISK.IDENTIFIED_DATE";

    public const string NextReviewDate = "RISK.NEXT_REVIEW_DATE";

    /// <summary>The (UTC) date the risk was closed.</summary>
    public const string ClosedDate = "RISK.CLOSED_DATE";

    /// <summary>Its next review date is past and it is not closed.</summary>
    public const string ReviewOverdue = "RISK.REVIEW_OVERDUE";

    /// <summary>It was raised as an issue (WF-07).</summary>
    public const string Materialised = "RISK.MATERIALISED";

    /// <summary>Its PLANNED and IN_PROGRESS treatment actions.</summary>
    public const string OpenActions = "RISK.OPEN_ACTIONS";

    /// <summary>The times it was reopened.</summary>
    public const string Reopened = "RISK.REOPENED";
}

/// <summary>
/// One record of a record source (the 20 Nov 2026 record sources): its id, its project and its value of each field key, the source's exact string as
/// <see cref="PortfolioFieldCell.Value"/> is (a code, a number, <c>true</c>/<c>false</c>, a <c>yyyy-MM-dd</c> date, or an id an <c>ITEM</c> or <c>TEXT</c> field
/// names); a key absent or null is not set.
/// </summary>
public sealed record PortfolioRecordRow(Guid Id, Guid ProjectId, IReadOnlyDictionary<string, string?> Values);

/// <summary>A record source's rows the caller may see, and the labels of the ids its <c>ITEM</c> and <c>TEXT</c> fields name, keyed by the id as written.</summary>
public sealed record PortfolioRecords(IReadOnlyList<PortfolioRecordRow> Rows, IReadOnlyDictionary<string, BilingualLabel> Labels)
{
    public static PortfolioRecords Empty { get; } = new([], new Dictionary<string, BilingualLabel>(StringComparer.Ordinal));
}

/// <summary>
/// The risk register as the designer reads it (the 20 Nov 2026 record sources): the risk fields (<see cref="PortfolioRiskFields"/>) and the risks of the
/// projects given that the caller may see under <c>RISK_VIEW</c>. Project declares it and the Risk module implements it over its own register, so each risk is
/// read under the Risk module's authorization, never by a raw query.
/// </summary>
public interface IPortfolioRiskReader
{
    /// <summary>The risk fields, in the designer's order; a CODE field's values are its codes (the ratings and levels of the RISK_MATRIX version in force).</summary>
    public Task<IReadOnlyList<PortfolioFieldDescriptor>> FieldsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// The risks, CLOSED ones included, of each project given whose risks the caller may see under <c>RISK_VIEW</c>, read in one batch; a project the caller
    /// may not see contributes none.
    /// </summary>
    public Task<PortfolioRecords> ReadAsync(Guid callerId, IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken);
}
