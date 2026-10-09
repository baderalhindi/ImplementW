using System.Reflection;
using System.Text.Json;
using PMPlatform.Application.Features.ExternalParticipation;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Tests.Unit.Application.ExternalParticipation;

/// <summary>
/// The external least-disclosure projection (WF-13 §8.2, EXT-CC-05, EXT-P-12). Every property of a representation is classified, visible to
/// the entity or AHDA's only — so a property added later is visible externally only once it is classified — and the external projection
/// withholds every AHDA-only one and names it, as the API then omits it (R-20).
/// </summary>
public sealed class ExternalProjectionTests
{
    /// <summary>What the entity sees of a request: its own project's reference, the purpose and schema, the source label, the instructions, its responder, the dates.</summary>
    private static readonly string[] RequestExternalFields =
    [
        "id", "projectId", "formalProjectId", "externalEntityId", "origin", "contributionTypeItemId", "contributionSchemaCode", "applicationMode",
        "targetModule", "targetType", "targetId", "targetLabel", "responseFields", "instructions", "responsibleUserId", "dueDate", "dueCondition",
        "status", "issuedAt", "cancelledAt", "cancellationReason", "closedAt", "createdAt", "updatedAt", "projection", "maskedFields",
    ];

    /// <summary>What the entity sees of a revision: its values, who of the entity gave them, its status and dates, and the reason it reads.</summary>
    private static readonly string[] ContributionExternalFields =
    [
        "id", "externalUpdateRequestId", "projectId", "externalEntityId", "revisionNo", "previousRevisionId", "status", "contributorUserId", "fields",
        "submittedAt", "reviewedAt", "reviewReason", "createdAt", "updatedAt", "projection", "maskedFields",
    ];

    [Fact]
    public void EveryRequestFieldIsClassified() =>
        Assert.Equal<IEnumerable<string>>(
            Properties<ExternalUpdateRequestDetail>(),
            RequestExternalFields.Concat(ExternalParticipationViews.RequestInternalOnlyFields).Order(StringComparer.Ordinal));

    [Fact]
    public void EveryContributionFieldIsClassified() =>
        Assert.Equal<IEnumerable<string>>(
            Properties<ExternalContributionDetail>(),
            ContributionExternalFields.Concat(ExternalParticipationViews.ContributionInternalOnlyFields).Order(StringComparer.Ordinal));

    /// <summary>The reviewer, who issued it, who wrote it and the configuration version are withheld from the entity, and named.</summary>
    [Fact]
    public void TheExternalRequestWithholdsEveryInternalField()
    {
        ExternalUpdateRequestDetail request = Request();
        ExternalUpdateRequestDetail projected = ExternalParticipationViews.Project(request);

        Assert.Equal(ParticipationAudience.External, projected.Projection);
        Assert.Equal(ExternalParticipationViews.RequestInternalOnlyFields, projected.MaskedFields);
        Assert.All(ExternalParticipationViews.RequestInternalOnlyFields, field => Assert.Null(ValueOf(projected, field)));
        Assert.Equal((request.ResponsibleUserId, request.Instructions), (projected.ResponsibleUserId, projected.Instructions));
    }

    /// <summary>EXT-P-12: the internal review note, the reviewer and the source version are withheld; the reason for a return is the entity's to read.</summary>
    [Fact]
    public void TheExternalRevisionWithholdsTheInternalNoteAndTheReviewer()
    {
        ExternalContributionDetail contribution = Contribution();
        ExternalContributionDetail projected = ExternalParticipationViews.Project(contribution);

        Assert.Equal(ExternalParticipationViews.ContributionInternalOnlyFields, projected.MaskedFields);
        Assert.All(ExternalParticipationViews.ContributionInternalOnlyFields, field => Assert.Null(ValueOf(projected, field)));
        Assert.Equal(contribution.ReviewReason, projected.ReviewReason);
        Assert.Equal(contribution.Fields, projected.Fields);
    }

    private static string[] Properties<T>() =>
        [.. typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).Order(StringComparer.Ordinal)];

    private static object? ValueOf(object record, string field) =>
        record.GetType().GetProperties().Single(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name) == field).GetValue(record);

    private static ExternalUpdateRequestDetail Request() =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), "PRJ-000001", Guid.NewGuid(), ExternalRequestOrigin.AhdaIssued, Guid.NewGuid(), ContributionSchemas.TaskProgress,
            ContributionApplicationMode.UpdateAllowedSourceFields, TaskProgressTarget.Module, TaskProgressTarget.Type, Guid.NewGuid(), new NarrativeText("Excavation", Language.En),
            [], new NarrativeText("Report the excavation's progress.", Language.En), Guid.NewGuid(), Guid.NewGuid(), null, ResponseDueCondition.NotApplicable,
            ExternalUpdateRequestStatus.Issued, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, null, null, DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            DateTimeOffset.UnixEpoch, Guid.NewGuid(), ParticipationAudience.Internal, []);

    private static ExternalContributionDetail Contribution() =>
        new(
            Guid.Parse("00000000-0000-4000-8000-000000000001"), Guid.Empty, Guid.Empty, Guid.Empty, 1, null, ExternalContributionStatus.Returned, Guid.Empty,
            [new ContributionFieldValue(ContributionSchemas.ActualPercentComplete, "40", null)], DateTimeOffset.UnixEpoch, 7, "IN_PROGRESS", Guid.NewGuid(),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, new NarrativeText("Attach the site report.", Language.En),
            new NarrativeText("Figure looks high against last week's walk-through.", Language.En), DateTimeOffset.UnixEpoch, Guid.NewGuid(), DateTimeOffset.UnixEpoch,
            Guid.NewGuid(), ParticipationAudience.Internal, []);
}
