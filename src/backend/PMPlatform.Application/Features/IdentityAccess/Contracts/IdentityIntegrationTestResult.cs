namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

public sealed record IdentityIntegrationTestResult(ConnectionTestResult Directory, ConnectionTestResult SingleSignOn);
