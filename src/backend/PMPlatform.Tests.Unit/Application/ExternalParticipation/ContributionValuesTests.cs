using PMPlatform.Application.Features.ExternalParticipation;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.ExternalParticipation;

/// <summary>
/// The typed schemas (WF-13 EXT-CC-06, BR-EXT-008, BR-EXT-020): an answer holds the schema's fields and nothing else, each of its type and
/// range, kept in one canonical form — so a value outside the allowlist never reaches storage, review or a source.
/// </summary>
public sealed class ContributionValuesTests
{
    private static readonly ContributionSchema TaskProgress = ContributionSchemas.Of(ContributionSchemas.TaskProgress);
    private static readonly ContributionSchema Information = ContributionSchemas.Of(ContributionSchemas.ProjectInformation);

    /// <summary>A field the schema does not define — a generic patch of the source's other fields, say — is refused, not ignored.</summary>
    [Fact]
    public void AFieldOutsideTheSchemaIsRefused()
    {
        AdministrationResult<IReadOnlyList<ContributionFieldValue>> result = ContributionValues.Normalize(TaskProgress,
        [
            new(ContributionSchemas.ActualPercentComplete, "40", null),
            new("plannedFinishDate", "2026-12-31", null),
        ]);

        Assert.Equal(ExternalParticipationErrorCodes.FieldNotAllowed, result.Error!.Code);
        Assert.Equal([new FieldIssue("fields[1].fieldCode", FieldIssue.NotAllowed)], result.Error.Fields);
    }

    [Fact]
    public void AFieldGivenTwiceIsRefused() =>
        Assert.Equal(
            [new FieldIssue("fields[1].fieldCode", FieldIssue.Duplicate)],
            ContributionValues.Normalize(TaskProgress, [new(ContributionSchemas.ActualPercentComplete, "40", null), new(ContributionSchemas.ActualPercentComplete, "50", null)]).Error!.Fields);

    [Theory]
    [InlineData("-0.0001")]
    [InlineData("100.0001")]
    [InlineData("12.34567")]
    [InlineData("forty")]
    [InlineData("1e2")]
    public void APercentageOutsideItsTypeOrRangeIsRefused(string value) =>
        Assert.Equal(ExternalParticipationErrorCodes.ValidationFailed, ContributionValues.Normalize(TaskProgress, [new(ContributionSchemas.ActualPercentComplete, value, null)]).Error!.Code);

    /// <summary>A narrative carries its entry language (ADR-012); a number or a date carries none.</summary>
    [Fact]
    public void OnlyANarrativeCarriesALanguage()
    {
        Assert.Equal(ExternalParticipationErrorCodes.ValidationFailed,
            ContributionValues.Normalize(TaskProgress, [new(ContributionSchemas.ProgressNote, "On site since Monday.", null)]).Error!.Code);
        Assert.Equal(ExternalParticipationErrorCodes.ValidationFailed,
            ContributionValues.Normalize(TaskProgress, [new(ContributionSchemas.ActualPercentComplete, "40", Language.En)]).Error!.Code);
        Assert.True(ContributionValues.Normalize(TaskProgress, [new(ContributionSchemas.ProgressNote, "بدأ العمل يوم الإثنين.", Language.Ar)]).Succeeded);
    }

    [Theory]
    [InlineData("040.50", "40.5")]
    [InlineData("100", "100")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("33.3300", "33.33")]
    public void ANumberIsKeptInCanonicalForm(string given, string stored) =>
        Assert.Equal(stored, ContributionValues.Normalize(TaskProgress, [new(ContributionSchemas.ActualPercentComplete, given, null)]).Value!.Single().Value);

    [Theory]
    [InlineData("2026-10-09", true)]
    [InlineData("09/10/2026", false)]
    [InlineData("2026-02-30", false)]
    public void ADateIsAnIsoCalendarDate(string value, bool accepted) =>
        Assert.Equal(accepted, ContributionValues.Normalize(Information, [new(ContributionSchemas.AsOfDate, value, null)]).Succeeded);

    /// <summary>A draft may be partial; a submission names every required field (EXT-ERR-023).</summary>
    [Fact]
    public void ASubmissionNamesEveryRequiredField()
    {
        Assert.Equal(
            [new FieldIssue(ContributionSchemas.ActualPercentComplete, FieldIssue.Required)],
            ContributionValues.MissingRequired(TaskProgress, [ContributionSchemas.ProgressNote])!.Fields);
        Assert.Null(ContributionValues.MissingRequired(TaskProgress, [ContributionSchemas.ActualPercentComplete]));
        Assert.Equal(ExternalParticipationErrorCodes.RequiredItemMissing, ContributionValues.MissingRequired(Information, [])!.Code);
    }

    /// <summary>
    /// The allowlist: a schema with a source names an adapter's type and applies by UPDATE_ALLOWED_SOURCE_FIELDS; a reference-only schema
    /// names none. The task's percentage is the WF-04 field ADR-009 bounds 0–100.
    /// </summary>
    [Fact]
    public void EverySchemaIsTypedAndOnlyTheTaskProgressSchemaReachesASource()
    {
        Assert.Equal([ContributionSchemas.ProjectInformation, ContributionSchemas.TaskProgress], ContributionSchemas.All.Select(s => s.Code).Order(StringComparer.Ordinal));
        Assert.All(ContributionSchemas.All, s => Assert.Equal(s.HasTarget, s.Mode == ContributionApplicationMode.UpdateAllowedSourceFields));
        Assert.Equal((TaskProgressTarget.Module, TaskProgressTarget.Type), (TaskProgress.TargetModule, TaskProgress.TargetType));
        Assert.Equal((0m, 100m, true), (TaskProgress.Field(ContributionSchemas.ActualPercentComplete)!.Minimum, TaskProgress.Field(ContributionSchemas.ActualPercentComplete)!.Maximum,
            TaskProgress.Field(ContributionSchemas.ActualPercentComplete)!.Required));
        Assert.Null(ContributionSchemas.Find("ANY_FIELD"));
    }
}
