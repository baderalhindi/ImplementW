using System.Globalization;
using System.Text.Json;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// The risk register as the Portfolio dashboard's designer reads it (the 20 Nov 2026 record sources): the projects the caller's <c>RISK_VIEW</c> scope reaches
/// (the engine's record scope, matched on the same project anchors <see cref="RiskAccess"/> decides one risk by), then their risks in one batch, each
/// represented as the register represents it (<see cref="RiskViews"/>: the current assessment's recorded rating, the open treatment actions, the issues raised).
/// Edge 3 (Risk → Project): Project declares the reader, so Project needs no reference to Risk.
/// </summary>
internal sealed class PortfolioRiskReader(
    IRiskRepository repository, IProjectFactsReader projects, IAuthorizationEngine engine, RiskViews views, IConfigurationResolver configuration,
    IMasterDataResolver masterData, IDisplayNameService displayNames) : IPortfolioRiskReader
{
    private const string True = "true";
    private const string False = "false";

    public async Task<IReadOnlyList<PortfolioFieldDescriptor>> FieldsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        (IReadOnlyList<string> ratings, IReadOnlyList<string> probabilities, IReadOnlyList<string> impacts) =
            await MatrixCodesAsync(now, cancellationToken).ConfigureAwait(false);
        return
        [
            Field(PortfolioRiskFields.Risk, PortfolioFieldType.Count, aggregations: [PortfolioFieldAggregation.Count]),
            Field(PortfolioRiskFields.Title, PortfolioFieldType.Text),
            Field(PortfolioRiskFields.Project, PortfolioFieldType.Item),
            Field(PortfolioRiskFields.Status, PortfolioFieldType.Code, [.. Enum.GetValues<RiskStatus>().Select(Code)]),
            Field(PortfolioRiskFields.Category, PortfolioFieldType.Item),
            Field(PortfolioRiskFields.Owner, PortfolioFieldType.Item),
            Field(PortfolioRiskFields.Rating, PortfolioFieldType.Code, ratings),
            Field(PortfolioRiskFields.Probability, PortfolioFieldType.Code, probabilities),
            Field(PortfolioRiskFields.Impact, PortfolioFieldType.Code, impacts),
            Field(PortfolioRiskFields.Score, PortfolioFieldType.Count),
            Field(PortfolioRiskFields.IdentifiedDate, PortfolioFieldType.Date),
            Field(PortfolioRiskFields.NextReviewDate, PortfolioFieldType.Date),
            Field(PortfolioRiskFields.ClosedDate, PortfolioFieldType.Date),
            Field(PortfolioRiskFields.ReviewOverdue, PortfolioFieldType.Boolean),
            Field(PortfolioRiskFields.Materialised, PortfolioFieldType.Boolean),
            Field(PortfolioRiskFields.OpenActions, PortfolioFieldType.Count),
            Field(PortfolioRiskFields.Reopened, PortfolioFieldType.Count),
        ];
    }

    public async Task<PortfolioRecords> ReadAsync(Guid callerId, IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.RiskView, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty || projectIds.Count == 0)
        {
            return PortfolioRecords.Empty;
        }

        HashSet<Guid> asked = [.. projectIds];
        Dictionary<Guid, ProjectFacts> reached = (await projects.ListReachedAsync(scope, cancellationToken).ConfigureAwait(false))
            .Where(p => asked.Contains(p.Id)).ToDictionary(p => p.Id);
        if (reached.Count == 0)
        {
            return PortfolioRecords.Empty;
        }

        IReadOnlyList<RiskEntity> risks = await repository.ListRisksAsync([.. reached.Keys], cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RiskDetail> details = await views.DetailsAsync(risks, cancellationToken).ConfigureAwait(false);

        Dictionary<string, BilingualLabel> labels = new(StringComparer.Ordinal);
        foreach (RiskDetail risk in details)
        {
            labels[Id(risk.Id)] = Same(risk.Title.Text);
            ProjectFacts project = reached[risk.ProjectId];
            labels[Id(project.Id)] = Same(project.Title.Text);
        }

        if (details.Count > 0)
        {
            foreach (MasterDataItemReference item in await masterData.ListPublishedItemsAsync(MasterDataCatalogueCodes.RiskCategory, cancellationToken).ConfigureAwait(false))
            {
                labels[Id(item.Id)] = item.Label;
            }
        }

        foreach (Guid[] owners in details.Select(r => r.OwnerUserId).OfType<Guid>().Distinct().Chunk(DisplayNameQuery.MaxIds))
        {
            DisplayNames names = await displayNames.ListAsync(callerId, new DisplayNameQuery(owners), cancellationToken).ConfigureAwait(false);
            foreach (PersonName person in names.Users)
            {
                labels[Id(person.Id)] = Same(person.DisplayName);
            }
        }

        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        return new PortfolioRecords([.. details.Select(r => new PortfolioRecordRow(r.Id, r.ProjectId, ValuesOf(r, today)))], labels);
    }

    /// <summary>A risk's value of each risk field, as <see cref="PortfolioRecordRow.Values"/> writes them; a value not set is left out.</summary>
    internal static Dictionary<string, string?> ValuesOf(RiskDetail risk, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(risk);
        RiskAssessmentSummary? assessment = risk.CurrentAssessment;
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PortfolioRiskFields.Risk] = "1",
            [PortfolioRiskFields.Title] = Id(risk.Id),
            [PortfolioRiskFields.Project] = Id(risk.ProjectId),
            [PortfolioRiskFields.Status] = Code(risk.Status),
            [PortfolioRiskFields.Category] = Id(risk.RiskCategoryItemId),
            [PortfolioRiskFields.Owner] = risk.OwnerUserId is { } owner ? Id(owner) : null,
            [PortfolioRiskFields.Rating] = assessment?.Rating.Code,
            [PortfolioRiskFields.Probability] = assessment is null ? null : Number(assessment.ProbabilityLevel),
            [PortfolioRiskFields.Impact] = assessment is null ? null : Number(assessment.OverallImpactLevel),
            [PortfolioRiskFields.Score] = assessment is null ? null : Number(assessment.ProbabilityLevel * assessment.OverallImpactLevel),
            [PortfolioRiskFields.IdentifiedDate] = Date(risk.IdentifiedDate),
            [PortfolioRiskFields.NextReviewDate] = risk.NextReviewDate is { } review ? Date(review) : null,
            [PortfolioRiskFields.ClosedDate] = risk.ClosedAt is { } closed ? Date(DateOnly.FromDateTime(closed.UtcDateTime)) : null,
            [PortfolioRiskFields.ReviewOverdue] = risk.Status != RiskStatus.Closed && risk.NextReviewDate < today ? True : False,
            [PortfolioRiskFields.Materialised] = risk.MaterialisedAt is null ? False : True,
            [PortfolioRiskFields.OpenActions] = Number(risk.OpenTreatmentActions.Count),
            [PortfolioRiskFields.Reopened] = Number(risk.ReopenedCount),
        };
    }

    /// <summary>
    /// The RISK_MATRIX version in force's rating codes (in its order), probability levels and overall impact levels; none while no version is in force, so the
    /// fields are still offered and a filter on them simply has no value to tick.
    /// </summary>
    private async Task<(IReadOnlyList<string> Ratings, IReadOnlyList<string> Probabilities, IReadOnlyList<string> Impacts)> MatrixCodesAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            ResolvedConfiguration matrix = await configuration.ResolveAsync(ConfigurationFamilyCodes.RiskMatrix, now, cancellationToken).ConfigureAwait(false);
            return (
                [.. matrix.Content.RiskRatings.OrderBy(r => r.SortOrder).Select(r => r.Code)],
                [.. matrix.Content.ProbabilityLevels.Select(l => l.Level).Distinct().Order().Select(l => Number(l))],
                [.. matrix.Content.ImpactLevels.Select(l => l.Level).Distinct().Order().Select(l => Number(l))]);
        }
        catch (ConfigurationMissingException)
        {
            return ([], [], []);
        }
    }

    private static PortfolioFieldDescriptor Field(
        string key, PortfolioFieldType type, IReadOnlyList<string>? values = null, IReadOnlyList<PortfolioFieldAggregation>? aggregations = null) =>
        new(key, PortfolioRiskFields.Source, PortfolioRiskFields.SourceDomain, key[(PortfolioRiskFields.Source.Length + 1)..], type, IsSensitive: false,
            values ?? [], aggregations ?? PortfolioFieldDescriptor.AggregationsOf(type));

    private static BilingualLabel Same(string text) => new(text, text);

    private static string Id(Guid id) => id.ToString("D");

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>An enum value as the API writes it: <c>Identified</c> as <c>IDENTIFIED</c>.</summary>
    private static string Code<T>(T value)
        where T : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());
}
