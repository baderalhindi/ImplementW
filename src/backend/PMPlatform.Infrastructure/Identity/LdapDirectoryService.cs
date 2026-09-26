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
/// person found; their password is used for that one bind and nothing else. No directory group is read: platform roles
/// are assigned in the platform (ADR-007).
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
            using LdapConnection ldap = await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            (string Dn, DirectoryEntry Entry)? found = await FindAsync(
                ldap, connection, LdapFilter.Equality(Options.UsernameAttribute, username), cancellationToken).ConfigureAwait(false);
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
            using LdapConnection ldap = await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            (string Dn, DirectoryEntry Entry)? found = await FindAsync(ldap, connection, filter, cancellationToken).ConfigureAwait(false);
            return found is null ? DirectoryResult.Failed(AuthenticationFailure.Rejected) : DirectoryResult.Found(found.Value.Entry);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "lookup", Describe(exception));
            return DirectoryResult.Failed(AuthenticationFailure.ProviderUnavailable);
        }
    }

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
        string[] attributes = [Options.SubjectAttribute, Options.JobTitleAttribute, Options.DepartmentAttribute, Options.ManagerAttribute];
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
            managerSubject));
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
    /// The exception's type and LDAP result code only. A directory's own message can carry a DN, and so a username;
    /// it is not logged (CTL-27).
    /// </summary>
    private static string Describe(Exception exception) => exception is LdapException ldap
        ? $"{nameof(LdapException)} {ldap.ResultCode} ({LdapException.ResultCodeToString(ldap.ResultCode, CultureInfo.InvariantCulture)})"
        : exception.GetType().Name;

    [LoggerMessage(Level = LogLevel.Error, Message = "Directory unavailable during {Operation}: {Reason}.")]
    private static partial void LogUnavailable(ILogger logger, string operation, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The directory rejected the service account bind (AD_BIND_DN / AD_BIND_PASSWORD).")]
    private static partial void LogServiceBindRejected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A directory search matched {Count} or more entries; sign-in needs exactly one.")]
    private static partial void LogAmbiguous(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A directory entry has no {Attribute} value and cannot be linked to a platform user.")]
    private static partial void LogNoSubject(ILogger logger, string attribute);
}
