using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// Requests to add a value an import's workbook names and the platform's lists lack (the 2 Nov 2026 review §3). The request is a note to the
/// administrators, not the value: whoever adds it does so on the master data or organization screen, as any value is added, then marks the
/// request added; the importer maps the name to it and validates again.
/// </summary>
internal sealed class ProjectImportValueRequestService(
    IProjectImportRepository repository,
    IAuthorizationEngine engine,
    IOutbox outbox,
    TimeProvider timeProvider) : IProjectImportValueRequestService
{
    private const int SourceTextLength = 200;

    public async Task<AdministrationResult<ProjectImportValueRequestDetail>> RequestAsync(
        Guid callerId, ProjectImportDimension dimension, string sourceText, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        if (!await HoldsAsync(callerId, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Forbidden;
        }

        string text = sourceText.Trim();
        if (text.Length is 0 or > SourceTextLength || dimension is ProjectImportDimension.Status)
        {
            return AdministrationError.Rule(ProjectImportErrorCodes.ValueRequestInvalid, new FieldIssue("sourceText", FieldIssue.NotAllowed));
        }

        string code = ProjectImportReferences.DimensionCode(dimension);
        if (await repository.FindOpenValueRequestAsync(code, text, cancellationToken).ConfigureAwait(false) is { } open)
        {
            return DetailOf(open);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectImportValueRequest request = new()
        {
            Id = Guid.CreateVersion7(now),
            Dimension = code,
            SourceText = text,
            Status = ProjectImportValueRequestStatus.Open,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(request);
        outbox.Stage(ImportNotifications.ValueRequested(
            request.Id, callerId, "/admin/master-data?requests=open",
            [new NotificationParameter("dimension", code), new NotificationParameter("sourceText", text)],
            now));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) ? DetailOf(request) : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<ProjectImportValueRequestPage>> ListAsync(
        Guid callerId, ProjectImportValueRequestStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await HoldsAsync(callerId, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false)
            && !await MayResolveAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Forbidden;
        }

        (IReadOnlyList<ProjectImportValueRequest> items, int total) = await repository.ListValueRequestsAsync(status, page.Page, page.PageSize, cancellationToken).ConfigureAwait(false);
        return new ProjectImportValueRequestPage([.. items.Select(DetailOf)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProjectImportValueRequestDetail>>> ResolveAsync(
        Guid callerId, Guid requestId, ProjectImportValueRequestStatus status, NarrativeText? note, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (!await MayResolveAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Forbidden;
        }

        ProjectImportValueRequest? request = await repository.FindValueRequestAsync(requestId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return AdministrationError.NotFound;
        }

        if (request.Status != ProjectImportValueRequestStatus.Open || status == ProjectImportValueRequestStatus.Open)
        {
            return AdministrationError.Conflict(ProjectImportErrorCodes.InvalidState);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        request.Status = status;
        request.ResolvedByUserId = callerId;
        request.ResolvedAt = now;
        request.ResolutionNote = note;
        request.UpdatedAt = now;
        request.UpdatedBy = callerId;
        outbox.Stage(ImportNotifications.ValueResolved(
            request.Id, callerId, request.CreatedBy, await DepartmentOfAsync(request.CreatedBy, cancellationToken).ConfigureAwait(false), "/projects/import",
            [
                new NotificationParameter("dimension", request.Dimension),
                new NotificationParameter("sourceText", request.SourceText),
                new NotificationParameter("outcome", status == ProjectImportValueRequestStatus.Added ? "ADDED" : "DECLINED"),
            ],
            now));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false)
            ? new Versioned<ProjectImportValueRequestDetail>(DetailOf(request), repository.RowVersionOf(request))
            : AdministrationError.PreconditionFailed;
    }

    /// <summary>The administrators who add values: master data (lists) or the organization (departments, entities).</summary>
    private async Task<bool> MayResolveAsync(Guid callerId, CancellationToken cancellationToken) =>
        await HoldsAsync(callerId, PermissionCatalogue.MasterDataManage, cancellationToken).ConfigureAwait(false)
        || await HoldsAsync(callerId, PermissionCatalogue.OrganizationManage, cancellationToken).ConfigureAwait(false);

    private async Task<bool> HoldsAsync(Guid callerId, string permission, CancellationToken cancellationToken) =>
        !(await engine.GetRecordScopeAsync(callerId, permission, cancellationToken).ConfigureAwait(false)).IsEmpty;

    /// <summary>The department the requester imports for, which their import notification is routed over.</summary>
    private async Task<Guid?> DepartmentOfAsync(Guid userId, CancellationToken cancellationToken)
    {
        RecordScope scope = await engine.GetRecordScopeAsync(userId, PermissionCatalogue.ProjectImport, cancellationToken).ConfigureAwait(false);
        return scope.Clauses.Count > 0 ? scope.Clauses[0].DepartmentId : null;
    }

    private static ProjectImportValueRequestDetail DetailOf(ProjectImportValueRequest r) => new(
        r.Id, Enum.Parse<ProjectImportDimension>(r.Dimension.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true), r.SourceText, r.Status,
        r.CreatedBy, r.CreatedAt, r.ResolvedByUserId, r.ResolvedAt, r.ResolutionNote);
}
