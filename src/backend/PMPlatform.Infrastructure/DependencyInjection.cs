using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Application.Features.Progress;
using PMPlatform.Application.Features.Project;
using PMPlatform.Application.Features.Schedule;
using PMPlatform.Infrastructure.Approval;
using PMPlatform.Infrastructure.Audit;
using PMPlatform.Infrastructure.DocumentManagement;
using PMPlatform.Infrastructure.Identity;
using PMPlatform.Infrastructure.Notifications;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Infrastructure.Persistence.Approval;
using PMPlatform.Infrastructure.Persistence.AuditActivity;
using PMPlatform.Infrastructure.Persistence.DocumentManagement;
using PMPlatform.Infrastructure.Persistence.Progress;
using PMPlatform.Infrastructure.Persistence.Project;
using PMPlatform.Infrastructure.Persistence.Schedule;
using PMPlatform.Infrastructure.Persistence.Authorization;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using PMPlatform.Infrastructure.Persistence.MasterDataConfig;
using PMPlatform.Infrastructure.Persistence.Messaging;
using PMPlatform.Infrastructure.Persistence.Notifications;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure;

/// <summary>
/// Registers the implementations of the Application and Domain interfaces (L-3). Persistence (TASK-024),
/// identity adapters (TASK-028) and notification channels (TASK-039) are added here as they are built.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHealthChecks().AddCheck<PostgreSqlHealthCheck>("postgresql");

        // Resolved when a context is created, not at start-up, so the API still starts and reports Unhealthy
        // without a database, as it did before TASK-024.
        services.AddDbContext<PMPlatformDbContext>(options => options.UsePlatformDatabase(RequiredConnectionString(configuration)));

        // TASK-028: the directory, SSO and session-token adapters, and the IdentityAccess repository sign-in reads.
        services.AddIdentityIntegration(configuration);
        services.AddScoped<IUserAccessRepository, UserAccessRepository>();

        // TASK-030: the grants and classifications the authorization engine evaluates.
        services.AddScoped<IAuthorizationRepository, AuthorizationRepository>();

        // TASK-031: FG-03 administration. The mobile verifier is a placeholder until the SMS provider (TASK-103) replaces it.
        services.AddScoped<IUserAdministrationRepository, UserAdministrationRepository>();
        services.AddScoped<IAccessRelationshipRepository, AccessRelationshipRepository>();
        services.AddScoped<IRoleAdministrationRepository, RoleAdministrationRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IExternalEntityRepository, ExternalEntityRepository>();
        services.TryAddSingleton<IMobileNumberVerifier, UnconfiguredMobileNumberVerifier>();

        // TASK-034: FG-04 master data and configuration.
        services.AddScoped<IMasterDataRepository, MasterDataRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();

        services.AddAuditForwarding(configuration);
        services.AddOutbox(configuration);

        // TASK-035: WF-11 approvals, and the pass that escalates overdue tasks and expires delegations.
        services.AddScoped<IApprovalRepository, ApprovalRepository>();
        services.AddOptions<ApprovalMaintenanceOptions>()
            .Bind(configuration.GetSection(ApprovalMaintenanceOptions.Section))
            .Validate(options => options.PollInterval > TimeSpan.Zero && options.BatchSize > 0, $"{ApprovalMaintenanceOptions.Section}: PollInterval and BatchSize must be positive.")
            .ValidateOnStart();
        services.AddHostedService<ApprovalMaintenanceWorker>();

        services.AddDocumentManagement(configuration);

        // TASK-041: WF-01's project schema.
        services.AddScoped<IProjectRepository, ProjectRepository>();

        // TASK-044: WF-02's progress schema.
        services.AddScoped<IProgressRepository, ProgressRepository>();

        // TASK-046: WF-03's schedule schema.
        services.AddScoped<IScheduleRepository, ScheduleRepository>();

        services.AddNotifications(configuration);

        return services;
    }

    /// <summary>
    /// TASK-039: WF-15. The e-mail channel is the Exchange relay when <c>EXCHANGE_SMTP_HOST</c> is set; a host with an unusable
    /// port, sender or credential pair stops the API at start-up without logging a value. No SMS provider is selected, so the
    /// gateway is the one that is not configured (TASK-103 replaces it). Links in e-mail and SMS are built on
    /// <c>APP_BASE_URL</c>, which must be an absolute http(s) origin when set.
    /// </summary>
    private static void AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IRoleHolderDirectory, RoleHolderDirectory>();

        services.AddOptions<NotificationDeliveryPolicy>()
            .Bind(configuration.GetSection(NotificationDeliveryPolicy.Section))
            .Configure<IConfiguration>((policy, source) => policy.AppBaseUrl = AppBaseUrlOf(source[AppBaseUrlKey]))
            .Validate(
                policy => policy.MaxAttempts > 0 && policy.RetryBaseDelay > TimeSpan.Zero && policy.SmsMaxSegments > 0 && policy.SmsDeepLinkAllowance >= 0,
                $"{NotificationDeliveryPolicy.Section}: MaxAttempts, RetryBaseDelay and SmsMaxSegments must be positive.")
            .Validate(_ => AppBaseUrlOf(configuration[AppBaseUrlKey]) is not null || string.IsNullOrWhiteSpace(configuration[AppBaseUrlKey]),
                $"{AppBaseUrlKey} is not an absolute http(s) URL without a query or fragment.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<NotificationDeliveryPolicy>>().Value);

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.Section))
            .Configure<IConfiguration>((options, source) =>
            {
                options.Host = source[SmtpOptions.HostKey];
                options.PortText = source[SmtpOptions.PortKey];
                options.User = source[ApplicationSecrets.ExchangeSmtpUser];
                options.Password = source[ApplicationSecrets.ExchangeSmtpPassword];
            })
            .Validate(options => options.IsValid(),
                $"{SmtpOptions.HostKey} is set, but {SmtpOptions.PortKey}, {SmtpOptions.Section}:FromAddress, or the {ApplicationSecrets.ExchangeSmtpUser}/{ApplicationSecrets.ExchangeSmtpPassword} pair is not usable.")
            .ValidateOnStart();
        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        services.TryAddSingleton<ISmsGateway, UnconfiguredSmsGateway>();

        services.AddOptions<NotificationWorkerOptions>()
            .Bind(configuration.GetSection(NotificationWorkerOptions.Section))
            .Validate(options => options.PollInterval > TimeSpan.Zero && options.BatchSize > 0, $"{NotificationWorkerOptions.Section}: PollInterval and BatchSize must be positive.")
            .ValidateOnStart();
        services.AddHostedService<NotificationWorker>();
    }

    /// <summary>The application's public origin (Environment and Secrets sheet: Public, DEV/SIT/UAT/PROD).</summary>
    private const string AppBaseUrlKey = "APP_BASE_URL";

    private static Uri? AppBaseUrlOf(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment)
            ? url
            : null;

    /// <summary>
    /// TASK-037: WF-12. The store is <c>DOCUMENT_STORAGE_CONNECTION_STRING</c>'s; a value in neither accepted form stops the
    /// API at start-up. No malware-scanning provider is selected, so the scanner is the one that scans nothing and every
    /// version stays SCAN_PENDING (fail closed); the provider's adapter replaces it.
    /// </summary>
    private static void AddDocumentManagement(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        services.AddOptions<DocumentUploadPolicy>()
            .Bind(configuration.GetSection(DocumentUploadPolicy.Section))
            .Validate(policy => policy.MaxFileSizeBytes > 0 && policy.AllowedContentTypes.Count > 0,
                $"{DocumentUploadPolicy.Section}: MaxFileSizeBytes must be positive and AllowedContentTypes must name at least one media type.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<DocumentUploadPolicy>>().Value);

        services.AddOptions<DocumentStorageOptions>()
            .Configure<IConfiguration>((options, source) => options.ConnectionString = source[ApplicationSecrets.DocumentStorageConnectionString])
            .Validate(options => options.Location is not null,
                $"{ApplicationSecrets.DocumentStorageConnectionString} is neither gs://<bucket>[/<prefix>] nor file:///<absolute path>.")
            .ValidateOnStart();
        services.AddSingleton<IDocumentStorage>(provider => provider.GetRequiredService<IOptions<DocumentStorageOptions>>().Value.Location switch
        {
            { Kind: DocumentStorageKind.CloudStorage } location => new CloudStorageDocumentStorage(location.Root, location.Prefix),
            { Kind: DocumentStorageKind.FileSystem } location => new FileSystemDocumentStorage(location.Root),
            _ => new UnconfiguredDocumentStorage(),
        });
        services.TryAddSingleton<IMalwareScanner, UnconfiguredMalwareScanner>();

        services.AddOptions<DocumentScanOptions>()
            .Bind(configuration.GetSection(DocumentScanOptions.Section))
            .Validate(options => options.PollInterval > TimeSpan.Zero && options.BatchSize > 0, $"{DocumentScanOptions.Section}: PollInterval and BatchSize must be positive.")
            .ValidateOnStart();
        services.AddHostedService<DocumentScanWorker>();
    }

    /// <summary>
    /// TASK-035: the transactional outbox (event-conventions EV-6) — producers stage into the request's context, and a
    /// worker delivers each DOMAIN_EVENT after commit to its one consumer.
    /// </summary>
    private static void AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IOutbox, Outbox>();
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.Section))
            .Validate(
                options => options.PollInterval > TimeSpan.Zero && options.BatchSize > 0 && options.RetryBaseDelay > TimeSpan.Zero,
                $"{OutboxOptions.Section}: PollInterval, BatchSize and RetryBaseDelay must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IOutboxDispatcher, OutboxDispatcher>();
        services.AddHostedService<OutboxDispatchWorker>();
    }

    /// <summary>
    /// TASK-033: the audit store and SIEM forwarding. The forwarded subset (PTBC-029) is configuration; an environment whose
    /// subset leaves out authentication, or names a class that does not exist, does not start (CTL-26).
    /// </summary>
    private static void AddAuditForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IAuditForwardingRepository, AuditForwardingRepository>();

        services.AddOptions<SiemForwardingPolicy>()
            .Bind(configuration.GetSection(SiemForwardingPolicy.Section))
            .Validate(
                policy => policy.ForwardedClasses.Contains(SiemForwardingPolicy.MinimumForwardedClass, StringComparer.Ordinal),
                $"{SiemForwardingPolicy.Section}:ForwardedClasses must include {SiemForwardingPolicy.MinimumForwardedClass} (CTL-26).")
            .Validate(policy => !policy.UnknownClasses().Any(), $"{SiemForwardingPolicy.Section}:ForwardedClasses names a class that is not an audit class.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SiemForwardingPolicy>>().Value);

        services.AddOptions<SiemOptions>()
            .Bind(configuration.GetSection(SiemOptions.Section))
            .Validate(options => options.PollInterval > TimeSpan.Zero && options.BatchSize > 0, $"{SiemOptions.Section}: PollInterval and BatchSize must be positive.")
            .ValidateOnStart();
        services.AddHttpClient(HttpSiemClient.HttpClientName, (provider, client) =>
            client.Timeout = provider.GetRequiredService<IOptions<SiemOptions>>().Value.Timeout);
        services.AddSingleton<ISiemClient, HttpSiemClient>();
        services.AddHostedService<SiemForwardingWorker>();
    }

    private static string RequiredConnectionString(IConfiguration configuration)
    {
        string? connectionString = configuration[ApplicationSecrets.DatabaseConnectionString];
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"{ApplicationSecrets.DatabaseConnectionString} is not configured.")
            : connectionString;
    }
}
