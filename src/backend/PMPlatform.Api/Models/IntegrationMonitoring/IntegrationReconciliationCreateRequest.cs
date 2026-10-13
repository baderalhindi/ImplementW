using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;

namespace PMPlatform.Api.Models.IntegrationMonitoring;

/// <summary>FG-05 US-INT-OPS-047: reconcile an integration that supports it. The request's <c>Idempotency-Key</c> is the run's key.</summary>
public sealed record IntegrationReconciliationCreateRequest(Guid? IntegrationId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(IntegrationId, "integrationId", errors);
        return errors;
    }
}
