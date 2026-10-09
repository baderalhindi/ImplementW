using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The way into WF-13's records every operation shares: the request and its project's facts (edge 18), the caller's audience, the
/// authorization check on the request's own anchors, and the save that commits. A DRAFT request does not exist for an external caller
/// (WF-13 §4.1), and a request the caller may not see is one that does not exist (R-47). Requesting, reviewing and applying are AHDA's: an
/// external caller is refused them whatever they hold.
/// </summary>
internal sealed class ExternalParticipationGate(IExternalParticipationRepository repository, IProjectFactsReader projects, ExternalParticipationAccess access)
{
    private static readonly HashSet<string> AhdaOnly = new(StringComparer.Ordinal)
    {
        PermissionCatalogue.ExternalRequestManage, PermissionCatalogue.ExternalContributionReview, PermissionCatalogue.ExternalContributionApply,
    };

    public async Task<ParticipationAudience> AudienceAsync(Guid callerId, CancellationToken cancellationToken) =>
        await access.IsInternalAsync(callerId, cancellationToken).ConfigureAwait(false) ? ParticipationAudience.Internal : ParticipationAudience.External;

    /// <summary>The project a new request names, if the caller may manage requests of it addressed to <paramref name="externalEntityId"/>.</summary>
    public async Task<(ProjectFacts? Project, AdministrationError? Error)> ReachProjectAsync(Guid callerId, Guid projectId, Guid externalEntityId, CancellationToken cancellationToken)
    {
        if (await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not { } project)
        {
            return (null, AdministrationError.NotFound);
        }

        AdministrationError? refused = await access.CheckInternalAsync(
                callerId, PermissionCatalogue.ExternalRequestManage, project, ExternalParticipationAccess.SubjectOf(project, externalEntityId, null, null),
                reason => ExternalParticipationAudit.AuthorityRefused(callerId, projectId, externalEntityId, null, PermissionCatalogue.ExternalRequestManage, reason),
                cancellationToken)
            .ConfigureAwait(false);
        return (project, refused);
    }

    /// <summary>The request, tracked, and its project, if the caller holds <paramref name="permissionCode"/> on it as <paramref name="actor"/>.</summary>
    public async Task<LoadedRequest> LoadRequestAsync(
        Guid callerId, string permissionCode, Guid requestId, uint? expectedVersion, ParticipationActor actor, CancellationToken cancellationToken)
    {
        ExternalUpdateRequest? request = await repository.FindRequestAsync(requestId, expectedVersion, cancellationToken).ConfigureAwait(false);
        (ProjectFacts? project, ParticipationAudience audience, AdministrationError? refused) =
            await CheckAsync(callerId, permissionCode, request, actor, cancellationToken).ConfigureAwait(false);
        return new LoadedRequest(project, request, audience, refused);
    }

    /// <summary>The revision, tracked, with its request, tracked, and project, if the caller holds <paramref name="permissionCode"/> on the request as <paramref name="actor"/>.</summary>
    public async Task<LoadedContribution> LoadContributionAsync(
        Guid callerId, string permissionCode, Guid contributionId, uint? expectedVersion, ParticipationActor actor, CancellationToken cancellationToken)
    {
        ExternalContribution? contribution = await repository.FindContributionAsync(contributionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ExternalUpdateRequest? request = contribution is null
            ? null
            : await repository.FindRequestAsync(contribution.ExternalUpdateRequestId, null, cancellationToken).ConfigureAwait(false);
        (ProjectFacts? project, ParticipationAudience audience, AdministrationError? refused) =
            await CheckAsync(callerId, permissionCode, request, actor, cancellationToken).ConfigureAwait(false);
        return new LoadedContribution(project, request, contribution, audience, refused);
    }

    /// <summary>The request and its project, if the caller may see it; for a collection, so nothing is recorded (R-3).</summary>
    public async Task<(ExternalUpdateRequest Request, ProjectFacts Project, ParticipationAudience Audience)?> ViewableRequestAsync(
        Guid callerId, Guid requestId, CancellationToken cancellationToken)
    {
        ExternalUpdateRequest? request = await repository.FindRequestAsync(requestId, null, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = request is null ? null : await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return null;
        }

        ParticipationAudience audience = await AudienceAsync(callerId, cancellationToken).ConfigureAwait(false);
        return (audience == ParticipationAudience.Internal || request!.Status != ExternalUpdateRequestStatus.Draft)
               && await access.CanViewAsync(callerId, ExternalParticipationAccess.SubjectOf(project, request!), cancellationToken).ConfigureAwait(false)
            ? (request!, project, audience)
            : null;
    }

    /// <summary>Saves and, when saved, commits; otherwise nothing was written.</summary>
    public async Task<ExternalParticipationSaveOutcome> SaveAsync(IExternalParticipationWork work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ExternalParticipationSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved == ExternalParticipationSaveOutcome.Saved)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    /// <summary>Stamps a changed row with who changed it and when.</summary>
    public static void Touch(Domain.Common.AuditedEntity row, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.UpdatedAt = now;
        row.UpdatedBy = actorId;
    }

    private async Task<(ProjectFacts? Project, ParticipationAudience Audience, AdministrationError? Refused)> CheckAsync(
        Guid callerId, string permissionCode, ExternalUpdateRequest? request, ParticipationActor actor, CancellationToken cancellationToken)
    {
        ProjectFacts? project = request is null ? null : await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        ParticipationAudience audience = await AudienceAsync(callerId, cancellationToken).ConfigureAwait(false);
        if (project is null || (audience == ParticipationAudience.External && request!.Status == ExternalUpdateRequestStatus.Draft))
        {
            return (null, audience, AdministrationError.NotFound);
        }

        IReadOnlyCollection<Guid>? actors = actor switch
        {
            ParticipationActor.Responder => [.. new[] { request!.ResponsibleUserId }.OfType<Guid>()],
            ParticipationActor.Reviewer => [.. new[] { request!.ReviewerUserId }.OfType<Guid>()],
            ParticipationActor.Holder => null,
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, "Unknown actor."),
        };
        AuthorizationSubject subject = ExternalParticipationAccess.SubjectOf(project, request!, actors);
        AdministrationError? refused = AhdaOnly.Contains(permissionCode)
            ? await access.CheckInternalAsync(
                    callerId, permissionCode, project, subject,
                    reason => ExternalParticipationAudit.AuthorityRefused(callerId, project.Id, request!.ExternalEntityId, request.Id, permissionCode, reason),
                    cancellationToken)
                .ConfigureAwait(false)
            : await access.CheckAsync(callerId, permissionCode, project, subject, cancellationToken).ConfigureAwait(false);
        return (project, audience, refused);
    }
}

/// <summary>Who an operation is the act of: anyone holding the permission, the request's named responder, or its assigned reviewer.</summary>
internal enum ParticipationActor
{
    Holder = 1,
    Responder = 2,
    Reviewer = 3,
}

internal sealed record LoadedRequest(ProjectFacts? Project, ExternalUpdateRequest? Request, ParticipationAudience Audience, AdministrationError? Error);

internal sealed record LoadedContribution(
    ProjectFacts? Project, ExternalUpdateRequest? Request, ExternalContribution? Contribution, ParticipationAudience Audience, AdministrationError? Error);
