using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>How a visual of the Portfolio dashboard is drawn (the 17 Nov 2026 dashboard designer).</summary>
public enum PortfolioVisualType
{
    /// <summary>One figure, with a second one under it when given.</summary>
    Card = 1,

    /// <summary>Horizontal bars, one per group.</summary>
    Bar = 2,

    /// <summary>Vertical columns, one per group, side by side per measure.</summary>
    Column = 3,

    Pie = 4,

    Donut = 5,

    /// <summary>A table: a row per group, a column per measure.</summary>
    Table = 6,

    /// <summary>One percentage on a half dial.</summary>
    Gauge = 7,
}

/// <summary>What a visual counts or averages over the projects of each group: the figures of the dashboard's headline cards (the 5 Nov 2026 review §3).</summary>
public enum PortfolioMeasure
{
    Projects = 1,

    /// <summary>Projects ACTIVE.</summary>
    InProgress = 2,

    /// <summary>The sum of the registration budgets, in SAR; withheld without the amounts permission.</summary>
    ValueSar = 3,

    /// <summary>The simple average of the measured projects' planned percent.</summary>
    AveragePlanned = 4,

    AverageActual = 5,

    /// <summary>Average actual less average planned, in points.</summary>
    GapPoints = 6,

    NeedingFollowUp = 7,

    /// <summary>Projects whose next action is past its due date.</summary>
    OverdueActions = 8,
}

/// <summary>What the projects of a visual are grouped by: a project attribute, or none for one figure over them all.</summary>
public enum PortfolioGroupBy
{
    None = 1,
    Portfolio = 2,
    Initiative = 3,
    Sector = 4,
    Department = 5,
    Entity = 6,
    Status = 7,
    Stage = 8,
    OnPlan = 9,
    Region = 10,
    City = 11,
    Priority = 12,
}

/// <summary>
/// One visual of the Portfolio dashboard as its designer placed it, the same for everyone who opens the dashboard (the 17 Nov 2026 dashboard
/// designer: the Portfolio Manager changes it, with no approval). It names a type, measures and a grouping from fixed lists, never an expression,
/// and takes a place on the twelve-column grid. With no visual saved, the dashboard shows the standard design.
/// </summary>
public sealed class PortfolioVisual : AuditedEntity
{
    /// <summary>The reading order, from 1.</summary>
    public short SortOrder { get; set; }

    public required string TitleAr { get; set; }

    public required string TitleEn { get; set; }

    public PortfolioVisualType VisualType { get; set; }

    /// <summary>One to three <see cref="PortfolioMeasure"/> codes, in the order they are drawn.</summary>
    public List<string> Measures { get; set; } = [];

    public PortfolioGroupBy GroupBy { get; set; }

    /// <summary>The first grid column, 0 to 11, counted from the reading start (the right in Arabic).</summary>
    public short LayoutColumn { get; set; }

    /// <summary>The first grid row, from 0.</summary>
    public short LayoutRow { get; set; }

    /// <summary>Columns taken, 1 to 12.</summary>
    public short LayoutWidth { get; set; }

    /// <summary>Rows taken, 1 to 12.</summary>
    public short LayoutHeight { get; set; }
}
