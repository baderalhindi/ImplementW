using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// AHDA's directory over LDAP (TASK-028). A sign-in is a search with the service account followed by a bind as the
/// person found; their password is used for that one bind and nothing else. A person changing their own password does
/// so under that same bind. No directory group is read: platform roles are assigned in the platform (ADR-007).
/// </summary>
internal sealed partial class LdapDirectoryService(
    IConfiguration configuration,
    IOptions<DirectoryOptions> options,
    TimeProvider timeProvider,
    ILogger<LdapDirectoryService> logger) : IDirectoryService
{
    public const string TransportSecurityRequired = "TRANSPORT_SECURITY_REQUIRED";

    private DirectoryOptions Options => options.Value;

    public bool IsConfigured => Connection() is not null;

    public bool IsPasswordResetEnabled => Options.AllowPasswordReset && IsConfigured;

    public async Task<DirectoryResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        DirectoryConnection? connection = Connection();
        if (connection is null)
        {
            return DirectoryResult.Failed(AuthenticationFailure.NotConfigured);
        }

        // An empty password is an unauthenticated bind (RFC 4513 §5.1.2), which succeeds on most directories.
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return DirectoryResult.Failed(AuthenticationFailure.Rejected);
        }

        try
        {
            (LdapConnection opened, (string Dn, DirectoryEntry Entry)? found) = await OpenAndFindAsync(
                connection, LdapFilter.Equality(Options.UsernameAttribute, username), "sign-in", cancellationToken).ConfigureAwait(false);
            using LdapConnection ldap = opened;
            if (found is null)
            {
                return DirectoryResult.Failed(AuthenticationFailure.Rejected);
            }

            try
            {
                await ldap.BindAsync(found.Value.Dn, password, cancellationToken).ConfigureAwait(false);
            }
            catch (LdapException exception) when (exception.ResultCode == LdapException.InvalidCredentials)
            {
                // Wrong password, locked, disabled or expired account: the directory's reason stays in the directory.
                return DirectoryResult.Failed(AuthenticationFailure.Rejected);
            }

            return DirectoryResult.Found(found.Value.Entry);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "sign-in", Describe(exception));
            return DirectoryResult.Failed(AuthenticationFailure.ProviderUnavailable);
        }
    }

    public async Task<DirectoryResult> FindBySubjectAsync(string subjectId, CancellationToken cancellationToken)
    {
        DirectoryConnection? connection = Connection();
        if (connection is null)
        {
            return DirectoryResult.Failed(AuthenticationFailure.NotConfigured);
        }

        if (SubjectFilter(subjectId) is not { } filter)
        {
            return DirectoryResult.Failed(AuthenticationFailure.Rejected);
        }

        try
        {
            (LdapConnection opened, (string Dn, DirectoryEntry Entry)? found) = await OpenAndFindAsync(connection, filter, "lookup", cancellationToken).ConfigureAwait(false);
            using LdapConnection ldap = opened;
            return found is null ? DirectoryResult.Failed(AuthenticationFailure.Rejected) : DirectoryResult.Found(found.Value.Entry);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "lookup", Describe(exception));
            return DirectoryResult.Failed(AuthenticationFailure.ProviderUnavailable);
        }
    }

    public async Task<DirectoryPasswordChange> ChangePasswordAsync(string subjectId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        // An empty current password is an unauthenticated bind, which would prove nothing (RFC 4513 §5.1.2).
        return string.IsNullOrEmpty(currentPassword)
            ? DirectoryPasswordChange.CurrentPasswordRejected
            : await WritePasswordAsync("password change", subjectId, async (ldap, dn) =>
        {
            try
            {
                await ldap.BindAsync(dn, currentPassword, cancellationToken).ConfigureAwait(false);
            }
            catch (LdapException exception) when (exception.ResultCode == LdapException.InvalidCredentials)
            {
                return DirectoryPasswordChange.CurrentPasswordRejected;
            }

            // Bound as the person, so the directory treats this as their own change, not an administrator's reset.
            if (Options.PasswordChangeProtocol == DirectoryPasswordProtocol.PasswordModify)
            {
                await SendPasswordModifyAsync(ldap, DirectoryPasswordEncoding.PasswordModifyRequest(null, currentPassword, newPassword), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ldap.ModifyAsync(
                    dn,
                    [
                        new LdapModification(LdapModification.Delete, ActiveDirectoryPassword(currentPassword)),
                        new LdapModification(LdapModification.Add, ActiveDirectoryPassword(newPassword)),
                    ],
                    cancellationToken).ConfigureAwait(false);
            }

            return DirectoryPasswordChange.Changed;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DirectoryPasswordChange> ResetPasswordAsync(string subjectId, string newPassword, CancellationToken cancellationToken)
    {
        return !IsPasswordResetEnabled
            ? DirectoryPasswordChange.NotPermitted
            : await WritePasswordAsync("password reset", subjectId, async (ldap, dn) =>
        {
            if (Options.PasswordChangeProtocol == DirectoryPasswordProtocol.PasswordModify)
            {
                await SendPasswordModifyAsync(ldap, DirectoryPasswordEncoding.PasswordModifyRequest(dn, null, newPassword), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ldap.ModifyAsync(dn, [new LdapModification(LdapModification.Replace, ActiveDirectoryPassword(newPassword))], cancellationToken).ConfigureAwait(false);
            }

            return DirectoryPasswordChange.Changed;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finds the person with the service account, runs <paramref name="write"/> on that connection, and turns the directory's answer into an outcome.</summary>
    private async Task<DirectoryPasswordChange> WritePasswordAsync(
        string operation, string subjectId, Func<LdapConnection, string, Task<DirectoryPasswordChange>> write, CancellationToken cancellationToken)
    {
        if (Connection() is not { } connection || SubjectFilter(subjectId) is not { } filter)
        {
            return DirectoryPasswordChange.NotPermitted;
        }

        try
        {
            (LdapConnection opened, (string Dn, DirectoryEntry Entry)? found) = await OpenAndFindAsync(connection, filter, operation, cancellationToken).ConfigureAwait(false);
            using LdapConnection ldap = opened;
            return found is null ? DirectoryPasswordChange.NotPermitted : await write(ldap, found.Value.Dn).ConfigureAwait(false);
        }
        catch (LdapException exception) when (exception.ResultCode == LdapException.ConstraintViolation)
        {
            return DirectoryPasswordChange.NewPasswordRejected;
        }
        catch (LdapException exception) when (exception.ResultCode is LdapException.InsufficientAccessRights or LdapException.UnwillingToPerform)
        {
            LogPasswordWriteRefused(logger, operation, Describe(exception));
            return DirectoryPasswordChange.NotPermitted;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, operation, Describe(exception));
            return DirectoryPasswordChange.Unavailable;
        }
    }

    private static async Task SendPasswordModifyAsync(LdapConnection ldap, byte[] request, CancellationToken cancellationToken) =>
        await ldap.ExtendedOperationAsync(new LdapExtendedOperation(DirectoryPasswordEncoding.PasswordModifyOid, request), cancellationToken).ConfigureAwait(false);

    private static LdapAttribute ActiveDirectoryPassword(string password) =>
        new(DirectoryPasswordEncoding.ActiveDirectoryAttribute, DirectoryPasswordEncoding.ActiveDirectoryValue(password));

    public DirectoryIntegrationStatus Describe() => new(
        IsConfigured,
        DirectoryConnection.IsUrlSet(configuration),
        !string.IsNullOrWhiteSpace(configuration[Secrets.ApplicationSecrets.DirectoryBindDn]),
        !string.IsNullOrEmpty(configuration[Secrets.ApplicationSecrets.DirectoryBindPassword]),
        DirectoryConnection.IsUrlSecure(configuration),
        Options.UsernameAttribute,
        Options.SubjectAttribute,
        Options.JobTitleAttribute,
        Options.DepartmentAttribute,
        Options.ManagerAttribute);

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        DirectoryConnection? connection = DirectoryConnection.Read(configuration);
        if (connection is null)
        {
            return Result(ConnectionTestOutcome.NotConfigured, failureCode: null);
        }

        if (!connection.UseTransportSecurity && !Options.AllowUnencryptedConnection)
        {
            return Result(ConnectionTestOutcome.Failed, TransportSecurityRequired);
        }

        try
        {
            using LdapConnection ldap = await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            await ldap.ReadAsync(connection.SearchBase, [Options.UsernameAttribute], cancellationToken).ConfigureAwait(false);
            return Result(ConnectionTestOutcome.Succeeded, failureCode: null);
        }
        catch (LdapException exception) when (exception.ResultCode == LdapException.InvalidCredentials)
        {
            return Result(ConnectionTestOutcome.Failed, ConnectionTestResult.BindRejected);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "connection test", Describe(exception));
            return Result(ConnectionTestOutcome.Failed, ConnectionTestResult.Unreachable);
        }
    }

    /// <summary>The connection, unless it is incomplete or would send passwords in clear text without being allowed to.</summary>
    private DirectoryConnection? Connection() =>
        DirectoryConnection.Read(configuration) is { } connection && (connection.UseTransportSecurity || Options.AllowUnencryptedConnection)
            ? connection
            : null;

    /// <summary>
    /// Connects as the service account and finds the single entry matching <paramref name="filter"/>, on a connection the caller then owns.
    /// A connection the network loses before any answer — refused, reset or closed while the service account binds or searches — is opened
    /// once more: nothing of the person's has been sent yet, so the second attempt repeats only the service account's read. One stray lost
    /// connection then no longer answers a sign-in with 503. A second loss, a timeout or a refused service account is the directory's answer.
    /// </summary>
    private async Task<(LdapConnection Ldap, (string Dn, DirectoryEntry Entry)? Found)> OpenAndFindAsync(
        DirectoryConnection connection, string filter, string operation, CancellationToken cancellationToken)
    {
        try
        {
            return await TryOpenAndFindAsync(connection, filter, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsConnectionLost(exception))
        {
            LogConnectionLost(logger, operation, Describe(exception));
            return await TryOpenAndFindAsync(connection, filter, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(LdapConnection Ldap, (string Dn, DirectoryEntry Entry)? Found)> TryOpenAndFindAsync(
        DirectoryConnection connection, string filter, CancellationToken cancellationToken)
    {
        LdapConnection ldap = await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            return (ldap, await FindAsync(ldap, connection, filter, cancellationToken).ConfigureAwait(false));
        }
        catch
        {
            ldap.Dispose();
            throw;
        }
    }

    /// <summary>Connects and binds as the service account.</summary>
    private async Task<LdapConnection> OpenAsync(DirectoryConnection connection, CancellationToken cancellationToken)
    {
        LdapConnectionOptions connectionOptions = new();
        if (connection.UseTransportSecurity)
        {
            connectionOptions.UseSsl();
        }

        int timeout = (int)Options.Timeout.TotalMilliseconds;
        LdapConnection ldap = new(connectionOptions) { ConnectionTimeout = timeout };
        try
        {
            ldap.Constraints = new LdapConstraints { TimeLimit = timeout };
            using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(Options.Timeout);
            await ldap.ConnectAsync(connection.Host, connection.Port, bounded.Token).ConfigureAwait(false);
            try
            {
                await ldap.BindAsync(connection.BindDn, connection.BindPassword, bounded.Token).ConfigureAwait(false);
            }
            catch (LdapException exception) when (exception.ResultCode == LdapException.InvalidCredentials)
            {
                // The service account itself is refused: the integration is broken, not the person's sign-in.
                LogServiceBindRejected(logger);
                throw;
            }

            return ldap;
        }
        catch
        {
            ldap.Dispose();
            throw;
        }
    }

    /// <summary>The single entry matching <paramref name="filter"/> under the search base; none or several is null.</summary>
    private async Task<(string Dn, DirectoryEntry Entry)?> FindAsync(
        LdapConnection ldap, DirectoryConnection connection, string filter, CancellationToken cancellationToken)
    {
        string[] attributes = [Options.SubjectAttribute, Options.JobTitleAttribute, Options.DepartmentAttribute, Options.ManagerAttribute, Options.MailAttribute];
        List<LdapEntry> entries = await SearchAsync(ldap, connection.SearchBase, LdapConnection.ScopeSub, filter, attributes, cancellationToken).ConfigureAwait(false);
        if (entries.Count != 1)
        {
            if (entries.Count > 1)
            {
                LogAmbiguous(logger, entries.Count);
            }

            return null;
        }

        LdapEntry entry = entries[0];
        string? subject = SubjectOf(entry);
        if (subject is null)
        {
            LogNoSubject(logger, Options.SubjectAttribute);
            return null;
        }

        string? managerSubject = StringValue(entry, Options.ManagerAttribute) is { } managerDn
            ? await ReadSubjectAsync(ldap, managerDn, cancellationToken).ConfigureAwait(false)
            : null;

        return (entry.Dn, new DirectoryEntry(
            subject,
            StringValue(entry, Options.JobTitleAttribute),
            StringValue(entry, Options.DepartmentAttribute),
            managerSubject,
            StringValue(entry, Options.MailAttribute)));
    }

    private async Task<string?> ReadSubjectAsync(LdapConnection ldap, string dn, CancellationToken cancellationToken)
    {
        List<LdapEntry> entries = await SearchAsync(ldap, dn, LdapConnection.ScopeBase, "(objectClass=*)", [Options.SubjectAttribute], cancellationToken)
            .ConfigureAwait(false);
        return entries.Count == 1 ? SubjectOf(entries[0]) : null;
    }

    /// <summary>
    /// Collects the entries of a search, at most two (one is the answer; two means ambiguous). Continuation references,
    /// which Active Directory returns for its application partitions, are skipped rather than chased.
    /// </summary>
    private static async Task<List<LdapEntry>> SearchAsync(
        LdapConnection ldap, string searchBase, int scope, string filter, string[] attributes, CancellationToken cancellationToken)
    {
        LdapSearchConstraints constraints = new(ldap.SearchConstraints) { MaxResults = 2, ReferralFollowing = false };
        List<LdapEntry> entries = [];
        try
        {
            ILdapSearchResults results = await ldap.SearchAsync(searchBase, scope, filter, attributes, false, constraints, cancellationToken)
                .ConfigureAwait(false);
            while (await results.HasMoreAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    entries.Add(await results.NextAsync(cancellationToken).ConfigureAwait(false));
                }
                catch (LdapReferralException)
                {
                    // A continuation reference, not an entry.
                }
            }
        }
        catch (LdapException exception) when (exception.ResultCode is LdapException.NoSuchObject or LdapException.SizeLimitExceeded)
        {
            // No such base (e.g. a manager DN that no longer exists): no entries. More than two matches: the server
            // reports the limit after sending the two, which are already collected.
        }

        return entries;
    }

    private string? SubjectOf(LdapEntry entry)
    {
        LdapAttribute? attribute = entry.GetOrDefault(Options.SubjectAttribute, null);
        return Options.IsBinarySubject
            ? attribute?.ByteValue is { Length: 16 } bytes ? new Guid(bytes).ToString() : null
            : attribute?.StringValue is { Length: > 0 } value ? value : null;
    }

    private string? SubjectFilter(string subjectId)
    {
        // objectGUID is matched on its 16 bytes, in the byte order Guid.ToByteArray gives, which is the order AD stores.
        return Options.IsBinarySubject
            ? Guid.TryParse(subjectId, out Guid guid) ? LdapFilter.Equality(Options.SubjectAttribute, guid.ToByteArray()) : null
            : string.IsNullOrWhiteSpace(subjectId) ? null : LdapFilter.Equality(Options.SubjectAttribute, subjectId);
    }

    private static string? StringValue(LdapEntry entry, string attribute) =>
        entry.GetOrDefault(attribute, null)?.StringValue is { Length: > 0 } value ? value : null;

    private ConnectionTestResult Result(ConnectionTestOutcome outcome, string? failureCode) => new(outcome, timeProvider.GetUtcNow(), failureCode);

    private static bool IsUnavailable(Exception exception) =>
        exception is LdapException or SocketException or IOException or TimeoutException or OperationCanceledException;

    /// <summary>
    /// The connection itself failed: refused, reset, or closed by the other end (Novell's ConnectError, also raised when the connection drops
    /// while an answer is awaited, and ServerDown). Not a timeout, which a second attempt would only repeat, nor any answer of the directory's.
    /// </summary>
    private static bool IsConnectionLost(Exception exception) =>
        exception is LdapException { ResultCode: LdapException.ConnectError or LdapException.ServerDown } or SocketException or IOException;

    /// <summary>
    /// The exception's type and LDAP result code, and the type of what lies under it, only. A directory's own message can carry a DN, and so a username;
    /// it is not logged (CTL-27).
    /// </summary>
    private static string Describe(Exception exception) => exception is LdapException ldap
        ? $"{nameof(LdapException)} {ldap.ResultCode} ({LdapException.ResultCodeToString(ldap.ResultCode, CultureInfo.InvariantCulture)}){Cause(ldap.InnerException)}"
        : exception.GetType().Name;

    /// <summary>What lies under a connection error, by type and socket error code only: the inner message names the host.</summary>
    private static string Cause(Exception? inner) => inner switch
    {
        null => string.Empty,
        SocketException socket => $" <- {nameof(SocketException)} {socket.SocketErrorCode}",
        { InnerException: SocketException socket } => $" <- {inner.GetType().Name} <- {nameof(SocketException)} {socket.SocketErrorCode}",
        _ => $" <- {inner.GetType().Name}",
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Directory unavailable during {Operation}: {Reason}.")]
    private static partial void LogUnavailable(ILogger logger, string operation, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory connection lost during {Operation}: {Reason}; connecting once more.")]
    private static partial void LogConnectionLost(ILogger logger, string operation, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The directory refused a {Operation}: {Reason}.")]
    private static partial void LogPasswordWriteRefused(ILogger logger, string operation, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The directory rejected the service account bind (AD_BIND_DN / AD_BIND_PASSWORD).")]
    private static partial void LogServiceBindRejected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A directory search matched {Count} or more entries; sign-in needs exactly one.")]
    private static partial void LogAmbiguous(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A directory entry has no {Attribute} value and cannot be linked to a platform user.")]
    private static partial void LogNoSubject(ILogger logger, string attribute);
}
