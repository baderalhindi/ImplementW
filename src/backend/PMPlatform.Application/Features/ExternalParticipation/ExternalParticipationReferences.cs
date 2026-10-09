using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// What a request may name and when its project admits one (WF-13 §5.3): the project's lifecycle state (Project, edge 18), the entity
/// (IdentityAccess, E-U1), the contribution type and its typed schema (MasterDataConfig, E-U2), the source record (its module, edge 19), the
/// responder and reviewer (the authorization engine), the due date, and the PARTICIPATION rule that enables the type for the project. Each
/// fails closed: nothing resolves to a default (EXT-CC-30).
/// </summary>
internal sealed class ExternalParticipationReferences(
    IOrganizationDirectory organizations, IMasterDataResolver masterData, IConfigurationResolver configuration, ContributionTargets targets, ExternalParticipationAccess access)
{
    /// <summary>
    /// WF-13 §11.1: a request is drafted and issued while its project is APPROVED_PLANNED, ACTIVE or SUSPENDED. Never before approval, and not
    /// once COMPLETED — the closeout contributions WF-13 allows then have no schema yet — or CLOSED (BR-EXT-038, BR-EXT-039).
    /// </summary>
    public static AdministrationError? ProjectRefused(ProjectFacts project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active or ProjectLifecycleState.Suspended
            ? null
            : AdministrationError.Rule(ExternalParticipationErrorCodes.ProjectNotEligible);
    }

    /// <summary>A due date, when set, is today or later on issue (the API's today, the UTC date).</summary>
    public static AdministrationError? DueDateRefused(DateOnly? dueDate, DateTimeOffset now) =>
        dueDate < DateOnly.FromDateTime(now.UtcDateTime)
            ? AdministrationError.Rule(ExternalParticipationErrorCodes.DueDateInvalid, new FieldIssue("dueDate", FieldIssue.NotAllowed))
            : null;

    public async Task<AdministrationError?> EntityRefusedAsync(Guid externalEntityId, CancellationToken cancellationToken) =>
        await organizations.IsActiveExternalEntityAsync(externalEntityId, cancellationToken).ConfigureAwait(false)
            ? null
            : AdministrationError.Rule(ExternalParticipationErrorCodes.EntityInactive, new FieldIssue("externalEntityId", FieldIssue.Inactive));

    /// <summary>The typed schema of a PUBLISHED CONTRIBUTION_TYPE item, by its code; refused when the platform defines none for it (EXT-ERR-015).</summary>
    public async Task<AdministrationResult<ContributionSchema>> SchemaAsync(Guid contributionTypeItemId, CancellationToken cancellationToken)
    {
        try
        {
            MasterDataItemReference item = await masterData.RequirePublishedItemAsync(MasterDataCatalogueCodes.ContributionType, contributionTypeItemId, cancellationToken)
                .ConfigureAwait(false);
            return ContributionSchemas.Find(item.Code) is { } schema
                ? schema
                : AdministrationError.Rule(ExternalParticipationErrorCodes.SchemaInvalid, new FieldIssue("contributionTypeItemId", FieldIssue.NotAllowed));
        }
        catch (ConfigurationMissingException)
        {
            return AdministrationError.Rule(ExternalParticipationErrorCodes.SchemaInvalid, new FieldIssue("contributionTypeItemId", FieldIssue.NotFound));
        }
    }

    /// <summary>A schema with a source names one record of the request's project; a reference-only schema names none.</summary>
    public async Task<AdministrationError?> TargetRefusedAsync(ContributionSchema schema, Guid? targetId, ProjectFacts project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(project);
        IExternalContributionTarget? target = targets.Of(schema);
        return (target, targetId) switch
        {
            (null, null) => null,
            (null, not null) => AdministrationError.Rule(ExternalParticipationErrorCodes.SourceInvalid, new FieldIssue("targetId", FieldIssue.NotAllowed)),
            (not null, null) => AdministrationError.Rule(ExternalParticipationErrorCodes.SourceInvalid, new FieldIssue("targetId", FieldIssue.Required)),
            (not null, { } id) => (await target.FindAsync(id, cancellationToken).ConfigureAwait(false))?.ProjectId == project.Id
                ? null
                : AdministrationError.Rule(ExternalParticipationErrorCodes.SourceInvalid, new FieldIssue("targetId", FieldIssue.NotFound)),
        };
    }

    /// <summary>The responder and the reviewer, each when named: eligible now (EXT-CC-03, EXT-CC-10).</summary>
    public async Task<AdministrationError?> PeopleRefusedAsync(
        ProjectFacts project, Guid externalEntityId, Guid? responsibleUserId, Guid? reviewerUserId, CancellationToken cancellationToken) =>
        responsibleUserId is { } responder && !await access.IsEligibleResponderAsync(responder, project, externalEntityId, cancellationToken).ConfigureAwait(false)
            ? AdministrationError.Rule(ExternalParticipationErrorCodes.ResponsibleUserIneligible, new FieldIssue("responsibleUserId", FieldIssue.NotAllowed))
            : reviewerUserId is { } reviewer && !await access.IsEligibleReviewerAsync(reviewer, project, externalEntityId, cancellationToken).ConfigureAwait(false)
                ? AdministrationError.Rule(ExternalParticipationErrorCodes.ReviewerIneligible, new FieldIssue("reviewerUserId", FieldIssue.NotAllowed))
                : null;

    /// <summary>
    /// ADR-013: the PARTICIPATION version in force enables the contribution type for the project's participation mode. A version without a rule
    /// for it fails closed (422 CONFIGURATION_MISSING); a disabled type is refused. Returns the version, which the issued request pins.
    /// </summary>
    public async Task<AdministrationResult<ResolvedConfiguration>> ParticipationAsync(
        ProjectFacts project, Guid contributionTypeItemId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ResolvedConfiguration participation = await configuration.ResolveAsync(ConfigurationFamilyCodes.Participation, now, cancellationToken).ConfigureAwait(false);
        return participation.IsContributionEnabled(project.ParticipationMode, contributionTypeItemId)
            ? participation
            : AdministrationError.Rule(ExternalParticipationErrorCodes.ContributionTypeNotEnabled, new FieldIssue("contributionTypeItemId", FieldIssue.NotAllowed));
    }
}
