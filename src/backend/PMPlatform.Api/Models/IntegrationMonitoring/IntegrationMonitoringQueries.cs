using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Api.Models.IntegrationMonitoring;

/// <summary>Shape validation of FG-05's query strings (T-3: shape here, meaning in the module).</summary>
internal static class IntegrationMonitoringQueries
{
    private const int CodeLength = 100;
    private const int KeyLength = 200;

    public static InvocationQuery Invocations(
        Guid? integrationId, string? status, string? operationCode, Guid? correlationId, string? idempotencyKey, string? sourceReference,
        DateTimeOffset? startedFrom, DateTimeOffset? startedTo, List<FieldError> errors)
    {
        RequestValidation.Optional(operationCode, "operationCode", CodeLength, errors);
        RequestValidation.Optional(idempotencyKey, "idempotencyKey", KeyLength, errors);
        RequestValidation.Optional(sourceReference, "sourceReference", KeyLength, errors);
        Window(startedFrom, startedTo, "startedTo", errors);
        return new InvocationQuery
        {
            IntegrationId = integrationId,
            Statuses = QueryParameters.EnumSet<InvocationStatus>(status, "status", errors),
            OperationCode = operationCode,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            SourceReference = sourceReference,
            StartedFrom = startedFrom,
            StartedTo = startedTo,
        };
    }

    public static SyncRunQuery SyncRuns(Guid? integrationId, string? runType, string? status, Guid? correlationId, List<FieldError> errors) =>
        new()
        {
            IntegrationId = integrationId,
            RunTypes = QueryParameters.EnumSet<SyncRunType>(runType, "runType", errors),
            Statuses = QueryParameters.EnumSet<SyncRunStatus>(status, "status", errors),
            CorrelationId = correlationId,
        };

    public static OperationalAlertQuery Alerts(
        Guid? integrationId, string? status, string? alertType, DateTimeOffset? raisedFrom, DateTimeOffset? raisedTo, List<FieldError> errors)
    {
        Window(raisedFrom, raisedTo, "raisedTo", errors);
        return new OperationalAlertQuery
        {
            IntegrationId = integrationId,
            Statuses = QueryParameters.EnumSet<OperationalAlertStatus>(status, "status", errors),
            AlertTypes = QueryParameters.EnumSet<OperationalAlertType>(alertType, "alertType", errors),
            RaisedFrom = raisedFrom,
            RaisedTo = raisedTo,
        };
    }

    /// <summary>A window's end, when given with its start, follows it.</summary>
    public static void Window(DateTimeOffset? start, DateTimeOffset? end, string endField, List<FieldError> errors)
    {
        if (start is { } from && end is { } to && to <= from)
        {
            errors.Add(new FieldError(endField, FieldError.DateBeforeStart));
        }
    }
}
