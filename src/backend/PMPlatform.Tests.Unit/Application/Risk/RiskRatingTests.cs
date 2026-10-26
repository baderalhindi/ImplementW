using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.Risk;

/// <summary>
/// ADR-011 rated generically (OQ-006): the matrix version defines the levels, the dimensions and the cells; an assessment must fit
/// the version in force, the overall impact is the highest dimension level, and the cell of the probability and the overall impact
/// gives the rating.
/// </summary>
public sealed class RiskRatingTests
{
    private static readonly Guid Cost = Guid.NewGuid();
    private static readonly Guid Schedule = Guid.NewGuid();
    private static readonly Guid Reputation = Guid.NewGuid();

    [Fact]
    public void AnAssessmentOfEveryDimensionAtADefinedLevelFits() =>
        Assert.Empty(RiskRating.Check(Matrix(), Draft(3, (Cost, 2), (Schedule, 4), (Reputation, 1))));

    [Fact]
    public void EveryWayAnAssessmentMissesTheMatrixIsReported()
    {
        Guid unknown = Guid.NewGuid();
        IReadOnlyList<FieldIssue> issues = RiskRating.Check(Matrix(), Draft(5, (Cost, 2), (Cost, 3), (unknown, 1)));

        Assert.Equal(
            [
                new FieldIssue("impacts[1].impactDimensionItemId", FieldIssue.Duplicate),
                new FieldIssue("impacts[2].impactDimensionItemId", FieldIssue.NotAllowed),
                new FieldIssue("impacts", FieldIssue.Required),
            ],
            issues);
    }

    /// <summary>A version defines its own levels: a probability or impact level it lacks is refused, whatever the scale's range.</summary>
    [Fact]
    public void ALevelTheVersionDoesNotDefineIsRefused()
    {
        ConfigurationContent fourLevels = Matrix(probabilityLevels: 4, impactLevels: 4);

        Assert.Equal(
            [new FieldIssue("probabilityLevel", FieldIssue.NotAllowed), new FieldIssue("impacts[1].impactLevel", FieldIssue.NotAllowed)],
            RiskRating.Check(fourLevels, Draft(5, (Cost, 1), (Schedule, 5), (Reputation, 1))));
    }

    /// <summary>The highest dimension decides: one catastrophic dimension is not averaged away by mild ones.</summary>
    [Fact]
    public void TheOverallImpactIsTheHighestDimensionLevel()
    {
        Assert.Equal(5, RiskRating.OverallImpactOf(Draft(1, (Cost, 1), (Schedule, 5), (Reputation, 1)).Impacts));
        Assert.Equal(2, RiskRating.OverallImpactOf(Draft(1, (Cost, 2), (Schedule, 2), (Reputation, 1)).Impacts));
    }

    /// <summary>The rating is the version's cell; the same figures in another version can rate differently, which is why a record pins the version.</summary>
    [Fact]
    public void TheRatingIsTheCellOfTheVersionInForce()
    {
        RiskAssessmentDraft draft = Draft(3, (Cost, 1), (Schedule, 4), (Reputation, 2));
        short overall = RiskRating.OverallImpactOf(draft.Impacts);

        Assert.Equal("HIGH", Resolved(Matrix(highFrom: 12)).RequireRiskRating(draft.ProbabilityLevel, overall).Code);
        Assert.Equal("LOW", Resolved(Matrix(highFrom: 13)).RequireRiskRating(draft.ProbabilityLevel, overall).Code);
    }

    private static RiskAssessmentDraft Draft(short probability, params (Guid Dimension, short Level)[] impacts) =>
        new(probability, [.. impacts.Select(i => new RiskImpactInput(i.Dimension, i.Level, null))], null);

    private static ResolvedConfiguration Resolved(ConfigurationContent content) =>
        new(Guid.NewGuid(), "RISK_MATRIX", 1, DateTimeOffset.UtcNow.AddDays(-1), null, content);

    /// <summary>A complete matrix: <paramref name="probabilityLevels"/> probability levels, the three dimensions at <paramref name="impactLevels"/> levels, HIGH where p × i reaches <paramref name="highFrom"/>.</summary>
    private static ConfigurationContent Matrix(int probabilityLevels = 5, int impactLevels = 5, int highFrom = 12)
    {
        BilingualLabel label = new("مستوى", "Level");
        return new ConfigurationContent
        {
            ProbabilityLevels = [.. Enumerable.Range(1, probabilityLevels).Select(l => new ProbabilityLevelEntry((short)l, label, null, null))],
            ImpactLevels =
            [
                .. new[] { Cost, Schedule, Reputation }.SelectMany(d => Enumerable.Range(1, impactLevels).Select(l => new ImpactLevelEntry(d, (short)l, label, null, null, null))),
            ],
            RiskRatings = [new RiskRatingEntry("LOW", label, 1), new RiskRatingEntry("HIGH", label, 2)],
            RiskMatrixCells =
            [
                .. Enumerable.Range(1, 5).SelectMany(p => Enumerable.Range(1, 5).Select(i => new RiskMatrixCellEntry((short)p, (short)i, p * i >= highFrom ? "HIGH" : "LOW"))),
            ],
        };
    }
}
