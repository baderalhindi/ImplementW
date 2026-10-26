using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Tests.Unit.Application.Suspension;

/// <summary>
/// WF-09 §5.2 (TASK-062): a suspension is of an ACTIVE project and a resumption of a SUSPENDED one; suspending a project that is suspended
/// already is the acceptance criterion's conflict, 409, not a wrong state; and a request names dates review can act on.
/// </summary>
public sealed class SuspensionEligibilityTests
{
    private static readonly DateOnly Today = new(2027, 3, 10);

    public static TheoryData<SuspensionRequestType, ProjectLifecycleState> EveryTypeAndState()
    {
        TheoryData<SuspensionRequestType, ProjectLifecycleState> data = [];
        foreach (SuspensionRequestType type in Enum.GetValues<SuspensionRequestType>())
        {
            foreach (ProjectLifecycleState state in Enum.GetValues<ProjectLifecycleState>())
            {
                data.Add(type, state);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndState))]
    public void ASuspensionIsOfAnActiveProjectAndAResumptionOfASuspendedOne(SuspensionRequestType type, ProjectLifecycleState state)
    {
        AdministrationError? refused = SuspensionEligibility.ProjectRefused(type, Project(state));

        (AdministrationErrorKind?, string?) expected = (type, state) switch
        {
            (SuspensionRequestType.Suspend, ProjectLifecycleState.Active) or (SuspensionRequestType.Resume, ProjectLifecycleState.Suspended) => (null, null),
            (SuspensionRequestType.Suspend, ProjectLifecycleState.Suspended) => (AdministrationErrorKind.Conflict, SuspensionErrorCodes.AlreadyExists),
            _ => (AdministrationErrorKind.RuleViolated, SuspensionErrorCodes.ProjectNotEligible),
        };
        Assert.Equal(expected, (refused?.Kind, refused?.Code));
    }

    [Fact]
    public void ASecondOpenRequestOfATypeIsTheConflictOfThatType()
    {
        Assert.Equal(SuspensionErrorCodes.AlreadyExists, SuspensionEligibility.DuplicateOf(SuspensionRequestType.Suspend).Code);
        Assert.Equal(SuspensionErrorCodes.ResumptionAlreadyExists, SuspensionEligibility.DuplicateOf(SuspensionRequestType.Resume).Code);
    }

    [Fact]
    public void APlannedResumptionDateBelongsToASuspensionAfterItsEffectiveDate()
    {
        Assert.Null(SuspensionEligibility.FieldsRefused(SuspensionRequestType.Suspend, Fields(Today, Today.AddDays(30))));
        Assert.Null(SuspensionEligibility.FieldsRefused(SuspensionRequestType.Suspend, Fields(null, Today)));
        Assert.Null(SuspensionEligibility.FieldsRefused(SuspensionRequestType.Resume, Fields(Today, null)));

        Assert.Equal(
            (SuspensionErrorCodes.DateInvalid, "plannedResumptionDate", FieldIssue.BeforeStart),
            Issue(SuspensionEligibility.FieldsRefused(SuspensionRequestType.Suspend, Fields(Today, Today))));
        Assert.Equal(
            (SuspensionErrorCodes.DateInvalid, "plannedResumptionDate", FieldIssue.NotAllowed),
            Issue(SuspensionEligibility.FieldsRefused(SuspensionRequestType.Resume, Fields(Today, Today.AddDays(1)))));
    }

    /// <summary>Review needs the effective date, from today on: a request never takes effect in the past.</summary>
    [Fact]
    public void ASubmissionNamesAnEffectiveDateFromTodayOn()
    {
        Assert.Null(SuspensionEligibility.SubmissionRefused(Request(Today, null), Today));
        Assert.Null(SuspensionEligibility.SubmissionRefused(Request(Today.AddDays(14), Today.AddDays(90)), Today));

        Assert.Equal(
            (SuspensionErrorCodes.Incomplete, "requestedEffectiveDate", FieldIssue.Required),
            Issue(SuspensionEligibility.SubmissionRefused(Request(null, null), Today)));
        Assert.Equal(
            (SuspensionErrorCodes.DateInvalid, "requestedEffectiveDate", FieldIssue.NotAllowed),
            Issue(SuspensionEligibility.SubmissionRefused(Request(Today.AddDays(-1), null), Today)));
        Assert.Equal(
            (SuspensionErrorCodes.DateInvalid, "plannedResumptionDate", FieldIssue.BeforeStart),
            Issue(SuspensionEligibility.SubmissionRefused(Request(Today.AddDays(5), Today.AddDays(5)), Today)));
    }

    private static (string?, string, string) Issue(AdministrationError? error)
    {
        Assert.NotNull(error);
        FieldIssue issue = Assert.Single(error.Fields);
        return (error.Code, issue.Field, issue.Code);
    }

    private static SuspensionRequestChanges Fields(DateOnly? effective, DateOnly? plannedResumption) =>
        new(new NarrativeText("Funding withheld pending the board's review.", Language.En), effective, plannedResumption);

    private static SuspensionRequest Request(DateOnly? effective, DateOnly? plannedResumption) =>
        new()
        {
            Reason = new NarrativeText("Funding withheld pending the board's review.", Language.En),
            RequestType = SuspensionRequestType.Suspend,
            Status = SuspensionRequestStatus.Draft,
            RequestedEffectiveDate = effective,
            PlannedResumptionDate = plannedResumption,
        };

    private static ProjectFacts Project(ProjectLifecycleState state) =>
        new(Guid.NewGuid(), "PRJ-000001", Guid.NewGuid(), null, Guid.NewGuid(), state, Guid.NewGuid(), null, null, ParticipationMode.AhdaManaged, new NarrativeText("Project", Language.En));
}
