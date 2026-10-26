using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-002–005 and MOD-080 (TASK-031). Disabling writes the user's status and time and nothing else: no assignment is
/// ended and no record that names the user is touched, so ownership, assignments and decisions keep their author
/// (Appendix A.1, CTL-09). Access stops anyway, because sign-in and the engine both refuse a disabled user. Every change
/// is saved with its PRIVILEGED_ACTION audit event (TASK-033); email and mobile number are recorded as changed, never
/// copied.
/// </summary>
internal sealed partial class UserAdministrationService(
    IUserAdministrationRepository users,
    IExternalEntityRepository entities,
    AdministrationAccess access,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<UserAdministrationService> logger) : IUserAdministrationService
{
    public async Task<AdministrationResult<UserPage>> ListAsync(Guid actorId, UserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await access.CheckCollectionAsync(actorId, PermissionCatalogue.UserView, cancellationToken).ConfigureAwait(false) is { } denied
            ? denied
            : await users.ListAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<UserDetail>>> GetAsync(Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        Versioned<UserDetail>? user = await users.FindDetailAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? AdministrationError.NotFound
            : await access.CheckRecordAsync(actorId, PermissionCatalogue.UserView, AdministrationAccess.SubjectOf(user.Value), cancellationToken).ConfigureAwait(false) is { } denied ? denied
            : user;
    }

    public async Task<AdministrationResult<Versioned<UserDetail>>> CreateAsync(Guid actorId, UserDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (await access.CheckNewRecordAsync(actorId, PermissionCatalogue.UserManage, new() { ExternalEntityId = draft.ExternalEntityId }, cancellationToken)
                .ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        if (draft.UserType == UserType.Service)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ServicePrincipal, new FieldIssue("userType", FieldIssue.NotAllowed));
        }

        if (draft.UserType == UserType.Internal && draft.JobTitle is not null)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.DirectoryAuthoritative, new FieldIssue("jobTitle", FieldIssue.NotAllowed));
        }

        if (await ReferenceChecks.UnretiredEntityAsync(entities, draft.ExternalEntityId, "externalEntityId", cancellationToken).ConfigureAwait(false) is { } entityIssue)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, entityIssue);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        User user = new()
        {
            Id = Guid.CreateVersion7(now),
            UserType = draft.UserType,
            DirectorySubjectId = draft.DirectorySubjectId,
            Username = draft.Username,
            DisplayName = draft.DisplayName,
            Email = draft.Email,
            MobileNumber = draft.MobileNumber,
            JobTitle = draft.JobTitle,
            ExternalEntityId = draft.ExternalEntityId,
            PreferredLanguage = draft.PreferredLanguage,
            Status = UserStatus.Active,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        users.Add(user);
        audit.Stage(Entry(actorId, user, IdentityAccessAuditEvents.UserCreated,
        [
            AuditAttribute.Change("user_type", null, user.UserType),
            AuditAttribute.Change("username", null, user.Username),
            AuditAttribute.Change("display_name", null, user.DisplayName),
            AuditAttribute.Change("directory_subject_id", null, user.DirectorySubjectId),
            AuditAttribute.Change(IdentityAccessAuditAttributes.ExternalEntityId, null, user.ExternalEntityId),
            AuditAttribute.WithheldChange("email", null, user.Email),
            AuditAttribute.WithheldChange("mobile_number", null, user.MobileNumber),
            AuditAttribute.Change(IdentityAccessAuditAttributes.Status, null, user.Status),
        ]));

        if ((await users.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogUserChanged(logger, actorId, "created", user.Id);
        return await DetailAsync(user.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<UserDetail>>> UpdateAsync(
        Guid actorId, Guid userId, UserChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        User? user = await users.FindForUpdateAsync(userId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, user, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        // ADR-007: an internal user's job title is the directory's, copied at each sign-in.
        if (user!.UserType == UserType.Internal && changes.JobTitle != user.JobTitle)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.DirectoryAuthoritative, new FieldIssue("jobTitle", FieldIssue.NotAllowed));
        }

        // ADR-004: a new number is a stranger's until its holder confirms it.
        if (changes.MobileNumber != user.MobileNumber)
        {
            user.MobileVerifiedAt = null;
        }

        AuditAttribute?[] changed =
        [
            AuditAttribute.Change("username", user.Username, changes.Username),
            AuditAttribute.Change("display_name", user.DisplayName, changes.DisplayName),
            AuditAttribute.WithheldChange("email", user.Email, changes.Email),
            AuditAttribute.WithheldChange("mobile_number", user.MobileNumber, changes.MobileNumber),
            AuditAttribute.Change("preferred_language", user.PreferredLanguage, changes.PreferredLanguage),
            AuditAttribute.Change("directory_subject_id", user.DirectorySubjectId, changes.DirectorySubjectId),
            AuditAttribute.Change("job_title", user.JobTitle, changes.JobTitle),
        ];

        user.Username = changes.Username;
        user.DisplayName = changes.DisplayName;
        user.Email = changes.Email;
        user.MobileNumber = changes.MobileNumber;
        user.PreferredLanguage = changes.PreferredLanguage;
        user.DirectorySubjectId = changes.DirectorySubjectId;
        user.JobTitle = changes.JobTitle;
        return await SaveAsync(actorId, user, "updated", IdentityAccessAuditEvents.UserUpdated, changed, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<UserDetail>>> ActivateAsync(Guid actorId, Guid userId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        User? user = await users.FindForUpdateAsync(userId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, user, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (user!.Status != UserStatus.Disabled)
        {
            return AdministrationError.InvalidTransition;
        }

        user.Status = UserStatus.Active;
        user.DisabledAt = null;
        return await SaveAsync(actorId, user, "activated", IdentityAccessAuditEvents.UserActivated,
                [AuditAttribute.Change(IdentityAccessAuditAttributes.Status, UserStatus.Disabled, UserStatus.Active)], cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<UserDetail>>> DisableAsync(Guid actorId, Guid userId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        User? user = await users.FindForUpdateAsync(userId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, user, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        // An administrator who disabled themselves would lock the platform's administration out with them.
        if (user!.Id == actorId)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.SelfAdministration);
        }

        if (user.Status != UserStatus.Active)
        {
            return AdministrationError.InvalidTransition;
        }

        user.Status = UserStatus.Disabled;
        user.DisabledAt = timeProvider.GetUtcNow();
        return await SaveAsync(actorId, user, "disabled", IdentityAccessAuditEvents.UserDisabled,
                [AuditAttribute.Change(IdentityAccessAuditAttributes.Status, UserStatus.Active, UserStatus.Disabled)], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Found, within the caller's USER_MANAGE scope, and a person: a SERVICE principal is not administered here.</summary>
    private async Task<AdministrationError?> CheckWritableAsync(Guid actorId, User? user, CancellationToken cancellationToken) =>
        user is null ? AdministrationError.NotFound
        : await access.CheckRecordAsync(actorId, PermissionCatalogue.UserManage, AdministrationAccess.SubjectOf(user), cancellationToken).ConfigureAwait(false) is { } denied ? denied
        : user.UserType == UserType.Service ? AdministrationError.Rule(IdentityAccessErrorCodes.ServicePrincipal)
        : null;

    private async Task<AdministrationResult<Versioned<UserDetail>>> SaveAsync(
        Guid actorId, User user, string change, string eventType, AuditAttribute?[] attributes, CancellationToken cancellationToken)
    {
        user.UpdatedAt = timeProvider.GetUtcNow();
        user.UpdatedBy = actorId;
        audit.Stage(Entry(actorId, user, eventType, attributes));
        if ((await users.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogUserChanged(logger, actorId, change, user.Id);
        return await DetailAsync(user.Id, cancellationToken).ConfigureAwait(false);
    }

    private static AuditEntry Entry(Guid actorId, User user, string eventType, AuditAttribute?[] attributes) =>
        new(AuditEventClass.PrivilegedAction, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject("IdentityAccess", nameof(User), user.Id),
            ScopeExternalEntityId = user.ExternalEntityId,
            Attributes = [.. attributes.OfType<AuditAttribute>()],
        };

    private async Task<AdministrationResult<Versioned<UserDetail>>> DetailAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.FindDetailAsync(userId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException($"User {userId} was saved and cannot be read back.");

    // Ids only (CTL-27). The audit event of the change is staged with it (CTL-25).
    [LoggerMessage(Level = LogLevel.Information, Message = "User administration: {ActorId} {Change} user {UserId}.")]
    private static partial void LogUserChanged(ILogger logger, Guid actorId, string change, Guid userId);
}
